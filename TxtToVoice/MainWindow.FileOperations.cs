using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TxtToVoice.Models;
using TxtToVoice.Services;

namespace TxtToVoice
{
    public partial class MainWindow
    {
        // ----------------------------------------------------------------
        // フィールド（ファイル・段落ナビ専用）
        // ----------------------------------------------------------------

        private List<(int Start, int Length)> _paragraphs = new();
        private int _currentParagraphIdx = -1;
        private List<(string Title, int CharOffset)> _sections = new();
        private bool _suppressSectionSelection;

        // ----------------------------------------------------------------
        // ファイルメニュー
        // ----------------------------------------------------------------

        private void MenuOpenFile_Click(object sender, RoutedEventArgs e) => OpenFile();
        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)  => OpenFile();
        private void MenuExit_Click(object sender, RoutedEventArgs e)     => Close();

        private void BtnInsertTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.TemplateManagerDialog(PathConfig.TemplatesPath) { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;

            string content = dlg.Result;

            // プレースホルダ {name} が含まれる場合は置換ダイアログを表示
            var placeholders = ExtractPlaceholders(content);
            if (placeholders.Count > 0)
            {
                var phDlg = new Dialogs.PlaceholderDialog(placeholders) { Owner = this };
                if (phDlg.ShowDialog() != true) return;
                content = phDlg.Apply(content);
            }

            TxtInput.SelectedText = content;
            TxtInput.Focus();
            SetStatus("テンプレートを挿入しました。");
        }

        /// <summary>{name} 形式のプレースホルダ名を出現順・重複なしで返す。</summary>
        private static List<string> ExtractPlaceholders(string text)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>();
            foreach (Match m in Regex.Matches(text, @"\{([^}]+)\}"))
            {
                string name = m.Groups[1].Value;
                if (seen.Add(name)) result.Add(name);
            }
            return result;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
                return;

            try
            {
                LoadFileIntoInput(files[0]);
                Logger.Info($"ドラッグ&ドロップでファイル読み込み: {files[0]}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"ファイルの読み込みに失敗しました。\n\n{ex.Message}",
                    "読み込みエラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Logger.Error($"D&Dファイル読み込みエラー: {files[0]} / {ex.Message}");
            }
        }

        private void OpenFile()
        {
            var dlg = new OpenFileDialog
            {
                Title      = "テキストファイルを開く",
                Filter     = "テキストファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*",
                DefaultExt = "txt"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                LoadFileIntoInput(dlg.FileName);
                Logger.Info($"ファイル読み込み: {dlg.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"ファイルの読み込みに失敗しました。\n\n{ex.Message}",
                    "読み込みエラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Logger.Error($"ファイル読み込みエラー: {dlg.FileName} / {ex.Message}");
            }
        }

        /// <summary>指定パスのテキストを TxtInput に読み込み、最近使ったファイルに追加する。</summary>
        private void LoadFileIntoInput(string path)
        {
            if (!ConfirmDiscardText()) return;
            TxtInput.Text = ReadTextFileWithFallback(path);
            SetStatus($"ファイルを読み込みました: {Path.GetFileName(path)}");
            AddRecentFile(path);
        }

        /// <summary>
        /// TxtInput に内容がある場合、破棄確認ダイアログを表示する (#108)。
        /// 空の場合または Yes 選択時に true を返す。
        /// </summary>
        private bool ConfirmDiscardText()
        {
            if (string.IsNullOrEmpty(TxtInput.Text)) return true;
            return MessageBox.Show(
                "現在の入力テキストを破棄して、新しいファイルを読み込みますか？",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        private const int MaxRecentFiles = 5;

        private void AddRecentFile(string path)
        {
            // 保存ポリシーが無効の場合はリストを更新しない
            if (!_saveRecentFiles) return;

            _recentFiles.Remove(path);
            _recentFiles.Insert(0, path);
            if (_recentFiles.Count > MaxRecentFiles)
                _recentFiles.RemoveRange(MaxRecentFiles, _recentFiles.Count - MaxRecentFiles);
            SaveCurrentSettings();
            UpdateRecentFilesMenu();
        }

        /// <summary>
        /// BOM（UTF-8 / UTF-16 LE / UTF-16 BE）を自動検出し、BOM なしは UTF-8 → Shift_JIS
        /// の順でフォールバックしてテキストを読み込む。
        ///
        /// 判定順:
        ///   1. BOM あり — StreamReader が自動検出（UTF-8 BOM / UTF-16 LE BOM / UTF-16 BE BOM）
        ///   2. BOM なし — UTF-8 として厳密に解釈（不正バイト列で DecoderFallbackException）
        ///   3.          — Shift_JIS へフォールバック
        ///
        /// ※ File.ReadAllText は内部で detectEncodingFromByteOrderMarks: true の StreamReader を
        ///   使用するため、UTF-16 BOM ファイルも正しく読み込まれる。
        /// </summary>
        private static string ReadTextFileWithFallback(string path)
        {
            try
            {
                return File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
            }
            catch (DecoderFallbackException) { }
            // IOException / UnauthorizedAccessException はここで伝播させる

            return File.ReadAllText(path, Encoding.GetEncoding("shift_jis"));
        }

        // ----------------------------------------------------------------
        // テキスト操作
        // ----------------------------------------------------------------

        private void BtnClearText_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtInput.Text)) return;
            var result = MessageBox.Show(
                "入力テキストをすべて消去しますか？",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                TxtInput.Clear();
                TxtPreview.Clear();
                SetStatus("テキストをクリアしました。");
            }
        }

        private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtCharCount.Text = $"{TxtInput.Text.Length:N0} 文字";
            UpdateEstimatedTime();
            UpdateParagraphs();

            UpdateSections();

            if (ChkAutoPreview.IsChecked != true) return;
            _autoPreviewCts?.Cancel();
            _autoPreviewCts?.Dispose();
            _autoPreviewCts = new CancellationTokenSource();
            var token = _autoPreviewCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500, token);
                    Dispatcher.Invoke(ApplyAndPreview);
                }
                catch (OperationCanceledException) { }
            }, token);
        }

        // ----------------------------------------------------------------
        // 段落ナビゲーション (#106)
        // ----------------------------------------------------------------

        /// <summary>
        /// TxtInput の内容を行（"\n" 区切り）に分割し、空行を除いて _paragraphs を構築する。
        /// テキスト変更のたびに呼び出す。
        /// </summary>
        private void UpdateParagraphs()
        {
            _paragraphs.Clear();
            _currentParagraphIdx = -1;
            string text = TxtInput.Text;

            int pos = 0;
            while (pos <= text.Length)
            {
                int nlIdx = text.IndexOf('\n', pos);
                int lineEnd = nlIdx >= 0 ? nlIdx : text.Length;

                // \r\n 対応: 末尾 \r を除いた実コンテンツ長
                int contentEnd = lineEnd;
                if (contentEnd > pos && text[contentEnd - 1] == '\r') contentEnd--;

                if (contentEnd > pos && !string.IsNullOrWhiteSpace(text.Substring(pos, contentEnd - pos)))
                    _paragraphs.Add((pos, contentEnd - pos));

                if (nlIdx < 0) break;
                pos = nlIdx + 1;
            }

            int count = _paragraphs.Count;
            TxtParaNav.Text        = count > 0 ? $"0/{count}" : "";
            BtnParaPrev.IsEnabled  = false;
            BtnParaNext.IsEnabled  = count > 0;
        }

        // ----------------------------------------------------------------
        // セクションナビゲーション (#113)
        // ----------------------------------------------------------------

        // 見出し候補: 行頭が ■/◆/●/▶ または 第N章/節、または 【...】 で始まる行
        private static readonly Regex SectionHeadPattern = new(
            @"^\s*(■|◆|●|▶|第\d+[章節部]|【[^】]+】)",
            RegexOptions.Compiled);

        private void UpdateSections()
        {
            _sections.Clear();
            string text = TxtInput.Text;
            int pos = 0;
            while (pos <= text.Length)
            {
                int nlIdx   = text.IndexOf('\n', pos);
                int lineEnd = nlIdx >= 0 ? nlIdx : text.Length;
                int contentEnd = lineEnd;
                if (contentEnd > pos && text[contentEnd - 1] == '\r') contentEnd--;

                if (contentEnd > pos)
                {
                    string line = text.Substring(pos, contentEnd - pos);
                    if (SectionHeadPattern.IsMatch(line))
                    {
                        string title = line.Length > 40 ? line[..37] + "…" : line;
                        _sections.Add((title.Trim(), pos));
                    }
                }
                if (nlIdx < 0) break;
                pos = nlIdx + 1;
            }

            _suppressSectionSelection = true;
            CmbSection.Items.Clear();
            foreach (var (title, _) in _sections)
                CmbSection.Items.Add(title);
            PnlSectionNav.Visibility = _sections.Count > 0
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            _suppressSectionSelection = false;
        }

        private void CmbSection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSectionSelection) return;
            int idx = CmbSection.SelectedIndex;
            if (idx < 0 || idx >= _sections.Count) return;

            int offset = _sections[idx].CharOffset;
            TxtInput.Select(offset, 0);
            TxtInput.ScrollToLine(TxtInput.GetLineIndexFromCharacterIndex(offset));
            TxtInput.Focus();
        }

        private void BtnParaNext_Click(object sender, RoutedEventArgs e)
        {
            if (_paragraphs.Count == 0) return;
            int next = _currentParagraphIdx + 1;
            if (next >= _paragraphs.Count) return;
            NavigateToParagraph(next);
        }

        private void BtnParaPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_paragraphs.Count == 0) return;
            int prev = _currentParagraphIdx - 1;
            if (prev < 0) return;
            NavigateToParagraph(prev);
        }

        private void NavigateToParagraph(int idx)
        {
            if (idx < 0 || idx >= _paragraphs.Count) return;
            _currentParagraphIdx = idx;
            var (start, len) = _paragraphs[idx];

            TxtInput.Focus();
            TxtInput.Select(start, len);
            int lineIdx = TxtInput.GetLineIndexFromCharacterIndex(start);
            if (lineIdx >= 0) TxtInput.ScrollToLine(lineIdx);

            int count = _paragraphs.Count;
            TxtParaNav.Text       = $"{idx + 1}/{count}";
            BtnParaPrev.IsEnabled = idx > 0;
            BtnParaNext.IsEnabled = idx < count - 1;
        }

        // ----------------------------------------------------------------
        // 最近使ったファイルメニュー
        // ----------------------------------------------------------------

        /// <summary>_recentFiles の内容を「最近使ったファイル」サブメニューに反映する。
        /// _saveRecentFiles が false のときはメニューを非表示にする。</summary>
        internal void UpdateRecentFilesMenu()
        {
            bool visible = _saveRecentFiles;
            MenuRecentFiles.Visibility     = visible ? Visibility.Visible : Visibility.Collapsed;
            SepAfterRecentFiles.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            if (!visible) return;

            MenuRecentFiles.Items.Clear();

            if (_recentFiles.Count == 0)
            {
                MenuRecentFiles.Items.Add(new MenuItem { Header = "（なし）", IsEnabled = false });
                return;
            }

            for (int i = 0; i < _recentFiles.Count; i++)
            {
                string path = _recentFiles[i];
                var item = new MenuItem
                {
                    Header  = $"_{i + 1}  {Path.GetFileName(path)}",
                    ToolTip = path
                };
                item.Click += (_, _) => OpenRecentFile(path);
                MenuRecentFiles.Items.Add(item);
            }

            MenuRecentFiles.Items.Add(new Separator());
            var clearItem = new MenuItem { Header = "リストをクリア(_C)" };
            clearItem.Click += (_, _) =>
            {
                _recentFiles.Clear();
                SaveCurrentSettings();
                UpdateRecentFilesMenu();
            };
            MenuRecentFiles.Items.Add(clearItem);
        }

        private void OpenRecentFile(string path)
        {
            try
            {
                LoadFileIntoInput(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"ファイルを開けませんでした。\n移動・削除された可能性があります。\n\n{path}\n\n{ex.Message}",
                    "読み込みエラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Logger.Error($"最近使ったファイルの読み込みエラー: {path} / {ex.Message}");
                _recentFiles.Remove(path);
                SaveCurrentSettings();
                UpdateRecentFilesMenu();
            }
        }
    }
}
