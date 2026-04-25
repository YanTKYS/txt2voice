using System;
using System.IO;
using System.Text;
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
        // ファイルメニュー
        // ----------------------------------------------------------------

        private void MenuOpenFile_Click(object sender, RoutedEventArgs e) => OpenFile();
        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)  => OpenFile();
        private void MenuExit_Click(object sender, RoutedEventArgs e)     => Close();

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
            TxtInput.Text = ReadTextFileWithFallback(path);
            SetStatus($"ファイルを読み込みました: {Path.GetFileName(path)}");
            AddRecentFile(path);
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
