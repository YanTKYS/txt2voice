using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using TxtToVoice.Dialogs;
using TxtToVoice.Models;
using TxtToVoice.Services;

namespace TxtToVoice
{
    public partial class MainWindow
    {
        // ----------------------------------------------------------------
        // 辞書 Undo（1段）
        // ----------------------------------------------------------------

        private List<Models.DictionaryEntry>? _dictUndoSnapshot = null;

        private void SaveDictUndoSnapshot()
        {
            _dictUndoSnapshot = _dictService.Entries.Select(e => e.Clone()).ToList();
            BtnDictUndo.IsEnabled = true;
        }

        private void ClearDictUndoSnapshot()
        {
            _dictUndoSnapshot = null;
            BtnDictUndo.IsEnabled = false;
        }

        private void BtnDictUndo_Click(object sender, RoutedEventArgs e)
        {
            if (_dictUndoSnapshot == null) return;
            _dictService.ReplaceAll(_dictUndoSnapshot);
            ClearDictUndoSnapshot();
            SaveDictionaryAndRefresh();
            SetStatus("辞書の変更を元に戻しました。");
        }

        // ----------------------------------------------------------------
        // 辞書の読み込み・一覧更新
        // ----------------------------------------------------------------

        private void LoadDictionary()
        {
            try
            {
                _dictService.Load();

                if (_dictService.Entries.Count == 0 && File.Exists(SampleDictionaryPath))
                {
                    int added = _dictService.LoadSampleDictionary(SampleDictionaryPath);
                    if (added > 0)
                    {
                        _dictService.Save();
                        SetStatus($"サンプル辞書 {added} 件を自動読み込みしました。");
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                MessageBox.Show(
                    $"辞書ファイルの読み込みに失敗しました。\n\n{ex.Message}",
                    "辞書読み込みエラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Logger.Error($"辞書読み込み失敗: {ex.Message}");
            }
            finally
            {
                RefreshDictionaryList();
            }
        }

        private void RefreshDictionaryList()
        {
            _entries.Clear();
            foreach (var e in _dictService.Entries)
                _entries.Add(e);

            var view = CollectionViewSource.GetDefaultView(_entries);
            view.Filter = FilterDictEntry;
            UpdateDictCount(view);
        }

        private bool FilterDictEntry(object obj)
        {
            if (obj is not DictionaryEntry e) return false;
            string filter = TxtDictFilter?.Text?.Trim() ?? string.Empty;
            if (filter.Length == 0) return true;
            return e.Display.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                || e.Reading.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                || e.Remarks.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
        }

        private void UpdateDictCount(ICollectionView? view = null)
        {
            view ??= CollectionViewSource.GetDefaultView(_entries);
            string filter = TxtDictFilter?.Text?.Trim() ?? string.Empty;
            if (filter.Length == 0)
            {
                TxtDictCount.Text = $"辞書: {_entries.Count} 件";
            }
            else
            {
                int visible = view.Cast<object>().Count();
                TxtDictCount.Text = $"辞書: {visible} / {_entries.Count} 件（フィルター中）";
            }
        }

        private void TxtDictFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            var view = CollectionViewSource.GetDefaultView(_entries);
            view.Refresh();
            UpdateDictCount(view);
        }

        private void BtnDictFilterClear_Click(object sender, RoutedEventArgs e)
        {
            TxtDictFilter.Clear();
        }

        // ----------------------------------------------------------------
        // プレビュー
        // ----------------------------------------------------------------

        private List<(int Index, int Length)> _previewMatches = new();
        private int _previewMatchCurrentIndex = -1;

        private void BtnApplyDictionary_Click(object sender, RoutedEventArgs e) => ApplyAndPreview();

        private void BtnCopyPreview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtPreview.Text)) return;
            Clipboard.SetText(TxtPreview.Text);
            SetStatus("プレビューをクリップボードにコピーしました。");
        }

        private void ApplyAndPreview()
        {
            string input = TxtInput.Text;
            if (string.IsNullOrWhiteSpace(input))
            {
                SetStatus("プレビューするテキストがありません。");
                return;
            }

            TxtPreview.Text = _annotatedPreview
                ? _dictService.ApplyDictionaryWithAnnotation(input)
                : _dictService.ApplyDictionary(input);

            // 比較モード: 左ペインを元テキストで同期 (#123)
            if (_compareMode) TxtCompareLeft.Text = input;

            UpdatePreviewMatchCount();
            SetStatus("辞書を適用してプレビューを更新しました。");
        }

        private void PreviewMode_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.IsChecked != true) return;
            _annotatedPreview = RbPreviewAnnotated.IsChecked == true;
            if (_dictService is null) return;
            if (!string.IsNullOrEmpty(TxtInput.Text))
                ApplyAndPreview();
            else
                UpdatePreviewMatchCount();
        }

        private void UpdatePreviewMatchCount()
        {
            string text = TxtPreview.Text;
            _previewMatches.Clear();
            _previewMatchCurrentIndex = -1;

            if (_annotatedPreview && !string.IsNullOrEmpty(text))
            {
                foreach (Match m in Regex.Matches(text, @"【[^】]*】"))
                    _previewMatches.Add((m.Index, m.Length));
            }

            int count = _previewMatches.Count;
            TxtPreviewMatchCount.Text = count == 0 ? "" : $"変換 {count} 件";
            BtnPreviewPrev.IsEnabled  = count > 0;
            BtnPreviewNext.IsEnabled  = count > 0;
        }

        private void BtnPreviewNext_Click(object sender, RoutedEventArgs e)
        {
            if (_previewMatches.Count == 0) return;
            _previewMatchCurrentIndex = (_previewMatchCurrentIndex + 1) % _previewMatches.Count;
            JumpToPreviewMatch(_previewMatchCurrentIndex);
        }

        private void BtnPreviewPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_previewMatches.Count == 0) return;
            _previewMatchCurrentIndex = (_previewMatchCurrentIndex - 1 + _previewMatches.Count) % _previewMatches.Count;
            JumpToPreviewMatch(_previewMatchCurrentIndex);
        }

        private void JumpToPreviewMatch(int idx)
        {
            var (start, len) = _previewMatches[idx];
            TxtPreview.Focus();
            TxtPreview.Select(start, len);
            int lineIdx = TxtPreview.GetLineIndexFromCharacterIndex(start);
            if (lineIdx >= 0) TxtPreview.ScrollToLine(lineIdx);
            TxtPreviewMatchCount.Text = $"変換 {idx + 1} / {_previewMatches.Count} 件";
        }

        // ----------------------------------------------------------------
        // 辞書 CRUD
        // ----------------------------------------------------------------

        private void BtnAddEntry_Click(object sender, RoutedEventArgs e)
        {
            Action<string>? speakAction = _speechService.IsAvailable ? _speechService.SpeakAsync : null;
            var dlg = new DictionaryEntryDialog(speakAction: speakAction) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                _dictService.AddEntry(dlg.Result);
                ClearDictUndoSnapshot();
                SaveDictionaryAndRefresh();
                SetStatus($"辞書に追加しました: 「{dlg.Result.Display}」→「{dlg.Result.Reading}」");
            }
        }

        private void DgDictionary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int count = DgDictionary.SelectedItems.Count;
            BtnEditEntry.IsEnabled        = count == 1;
            BtnDeleteEntry.IsEnabled      = count >= 1;
            BtnBatchPriority.IsEnabled    = count >= 1;
            bool singleNoFilter = count == 1 && string.IsNullOrEmpty(TxtDictFilter.Text);
            BtnMoveUp.IsEnabled   = singleNoFilter && GetSelectedEntryIndex() > 0;
            BtnMoveDown.IsEnabled = singleNoFilter && GetSelectedEntryIndex() < _dictService.Entries.Count - 1;
        }

        private void BtnEditEntry_Click(object sender, RoutedEventArgs e) => EditSelectedEntry();

        private void DgDictionary_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // インライン編集中の場合はキャンセルしてからダイアログを開く
            DgDictionary.CancelEdit();
            EditSelectedEntry();
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = GetSelectedEntryIndex();
            if (idx <= 0 || !string.IsNullOrEmpty(TxtDictFilter.Text)) return;
            _dictService.MoveEntry(idx, idx - 1);
            SaveDictionaryAndRefresh();
            DgDictionary.SelectedIndex = idx - 1;
            DgDictionary.ScrollIntoView(DgDictionary.SelectedItem);
            SetStatus("辞書エントリを上に移動しました。");
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = GetSelectedEntryIndex();
            if (idx < 0 || idx >= _dictService.Entries.Count - 1 || !string.IsNullOrEmpty(TxtDictFilter.Text)) return;
            _dictService.MoveEntry(idx, idx + 1);
            SaveDictionaryAndRefresh();
            DgDictionary.SelectedIndex = idx + 1;
            DgDictionary.ScrollIntoView(DgDictionary.SelectedItem);
            SetStatus("辞書エントリを下に移動しました。");
        }

        /// <summary>
        /// DataGrid の SelectedItem から、フィルタに関わらず _dictService.Entries 内の実インデックスを返す。
        /// 未選択または見つからない場合は -1。
        /// </summary>
        private int GetSelectedEntryIndex()
        {
            if (DgDictionary.SelectedItem is not Models.DictionaryEntry selected) return -1;
            for (int i = 0; i < _dictService.Entries.Count; i++)
                if (ReferenceEquals(_dictService.Entries[i], selected)) return i;
            return -1;
        }

        private void EditSelectedEntry()
        {
            int idx = GetSelectedEntryIndex();
            if (idx < 0) return;

            var entry = _dictService.Entries[idx];
            Action<string>? speakAction = _speechService.IsAvailable ? _speechService.SpeakAsync : null;
            var dlg   = new DictionaryEntryDialog(entry.Clone(), speakAction) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                _dictService.UpdateEntry(idx, dlg.Result);
                ClearDictUndoSnapshot();
                SaveDictionaryAndRefresh();
                DgDictionary.SelectedItem = dlg.Result;
                DgDictionary.ScrollIntoView(DgDictionary.SelectedItem);
                SetStatus($"辞書を更新しました: 「{dlg.Result.Display}」→「{dlg.Result.Reading}」");
            }
        }

        private void BtnDeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            var selected = DgDictionary.SelectedItems.Cast<Models.DictionaryEntry>().ToList();
            if (selected.Count == 0) return;

            int hitCount = _dictService.CountOccurrences(TxtInput.Text, selected);
            string hitInfo = hitCount > 0 ? $"\n\n現在の原稿でこれらのエントリは {hitCount} 件マッチしています。" : "";
            string msg = selected.Count == 1
                ? $"「{selected[0].Display}」→「{selected[0].Reading}」を辞書から削除しますか？{hitInfo}"
                : $"選択した {selected.Count} 件のエントリを辞書から削除しますか？{hitInfo}";

            if (MessageBox.Show(msg, "削除確認", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            SaveDictUndoSnapshot();
            // 後ろのインデックスから削除して前方インデックスがずれないようにする
            var indices = selected
                .Select(entry => { int i = -1; for (int k = 0; k < _dictService.Entries.Count; k++) if (ReferenceEquals(_dictService.Entries[k], entry)) { i = k; break; } return i; })
                .Where(i => i >= 0)
                .OrderByDescending(i => i);

            int firstIdx = -1;
            foreach (int i in indices)
            {
                if (firstIdx < 0 || i < firstIdx) firstIdx = i;
                _dictService.RemoveEntry(i);
            }

            SaveDictionaryAndRefresh();
            if (DgDictionary.Items.Count > 0)
                DgDictionary.SelectedIndex = Math.Min(firstIdx, DgDictionary.Items.Count - 1);
            SetStatus($"辞書から {selected.Count} 件削除しました。");
        }

        private void BtnBatchPriority_Click(object sender, RoutedEventArgs e)
        {
            var selected = DgDictionary.SelectedItems.Cast<Models.DictionaryEntry>().ToList();
            if (selected.Count == 0) return;

            var dlg = new InputDialog("優先度一括変更", "優先度を入力してください（数値が大きいほど優先）:", "0") { Owner = this };
            if (dlg.ShowDialog() != true) return;

            if (!int.TryParse(dlg.Result?.Trim(), out int newPriority))
            {
                MessageBox.Show("優先度には整数を入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int hitCount = _dictService.CountOccurrences(TxtInput.Text, selected);
            string hitInfo = hitCount > 0 ? $"\n（現在の原稿で {hitCount} 件マッチ）" : "";
            string confirmMsg = $"選択した {selected.Count} 件の優先度を {newPriority} に変更しますか？{hitInfo}";
            if (MessageBox.Show(confirmMsg, "優先度一括変更", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            SaveDictUndoSnapshot();
            foreach (var entry in selected)
                entry.Priority = newPriority;

            _dictService.Invalidate();
            SaveDictionaryAndRefresh();
            SetStatus($"{selected.Count} 件の優先度を {newPriority} に変更しました。");
        }

        private void DgDictionary_KeyDown(object sender, KeyEventArgs e)
        {
            bool ctrlShift = Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift);

            // セル編集中（TextBox がフォーカス）は Ins / Del / 移動ショートカットを無視する
            if (e.OriginalSource is System.Windows.Controls.TextBox) return;

            switch (e.Key)
            {
                case Key.Insert: BtnAddEntry_Click(sender, e);    e.Handled = true; break;
                case Key.Delete: BtnDeleteEntry_Click(sender, e); e.Handled = true; break;
                case Key.Up   when ctrlShift: BtnMoveUp_Click(sender, e);   e.Handled = true; break;
                case Key.Down when ctrlShift: BtnMoveDown_Click(sender, e); e.Handled = true; break;
            }
        }

        // ----------------------------------------------------------------
        // 辞書インライン編集 (#107)
        // ----------------------------------------------------------------

        private void DgDictionary_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.EditingElement is not System.Windows.Controls.TextBox tb) return;
            if (e.Row.Item is not Models.DictionaryEntry entry) return;

            string colHeader = e.Column.Header as string ?? "";
            string newVal    = tb.Text.Trim();

            if ((colHeader == "表記" || colHeader == "読み") && string.IsNullOrWhiteSpace(newVal))
            {
                e.Cancel = true;
                MessageBox.Show($"「{colHeader}」は空にできません。",
                    "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                Dispatcher.BeginInvoke(() => DgDictionary.CancelEdit());
                return;
            }

            if (colHeader == "表記")
            {
                bool dup = _entries.Any(en => !ReferenceEquals(en, entry) && en.Display == newVal);
                if (dup)
                {
                    e.Cancel = true;
                    MessageBox.Show($"「{newVal}」は既に辞書に登録されています。",
                        "重複エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Dispatcher.BeginInvoke(() => DgDictionary.CancelEdit());
                }
            }
        }

        private void DgDictionary_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            // バインディングのコミットを待ってから保存する
            Dispatcher.BeginInvoke(() =>
            {
                ClearDictUndoSnapshot();
                _dictService.Invalidate();
                _dictService.Save();
                if (!string.IsNullOrEmpty(TxtInput.Text))
                    ApplyAndPreview();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        // ----------------------------------------------------------------
        // CSV インポート / エクスポート
        // ----------------------------------------------------------------

        private void MenuImportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title      = "CSV インポート",
                Filter     = "CSV ファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*",
                DefaultExt = "csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var report   = CsvService.ImportWithReport(dlg.FileName);
                var imported = report.ValidEntries;

                if (imported.Count == 0)
                {
                    string detail = report.HasIssues
                        ? $"すべての行がスキップされました。\n\n{report.FormatIssues()}\n\nCSV の形式（表記,読み,備考,優先順位）を確認してください。"
                        : "インポートできるエントリがありませんでした。\nCSV の形式（表記,読み,備考,優先順位）を確認してください。";
                    MessageBox.Show(detail, "インポート", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // #78 容量ガード: インポート単体 + インポート後総量の両方で判定
                int totalDisplayChars    = imported.Sum(en => en.Display.Length);
                int totalReadingChars    = imported.Sum(en => en.Reading.Length);
                int existingDisplayChars = _dictService.Entries.Sum(en => en.Display.Length);
                int afterCount           = _dictService.Entries.Count + imported.Count;
                int afterDisplayChars    = existingDisplayChars + totalDisplayChars;

                bool importLarge = imported.Count > 1000 || totalDisplayChars > 10_000 || totalReadingChars > 10_000;
                bool totalLarge  = afterCount > 2_000 || afterDisplayChars > 20_000;

                if (importLarge || totalLarge)
                {
                    string guardDetail = totalLarge && !importLarge
                        ? $"インポート後の辞書総量が大きくなります（合計 {afterCount} 件、表記合計 {afterDisplayChars} 文字）。\n\n"
                        : $"インポートするエントリが大量です（{imported.Count} 件、表記 {totalDisplayChars} 文字、読み {totalReadingChars} 文字）。\n\n";
                    var guardAnswer = MessageBox.Show(
                        guardDetail + "Aho-Corasick 辞書の構築に時間がかかる場合があります。\n続行しますか？",
                        "大量インポートの確認",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (guardAnswer != MessageBoxResult.Yes) return;
                }

                string confirmMsg = report.HasIssues
                    ? $"検証で以下の問題が検出されました:\n\n{report.FormatIssues()}\n\n" +
                      $"有効エントリ {imported.Count} 件をインポートします。\n\n" +
                      "「はい」: 現在の辞書に追加します\n" +
                      "「いいえ」: 現在の辞書を置き換えます"
                    : $"{imported.Count} 件のエントリをインポートします。\n\n" +
                      "「はい」: 現在の辞書に追加します\n" +
                      "「いいえ」: 現在の辞書を置き換えます";

                var answer = MessageBox.Show(
                    confirmMsg,
                    "インポート確認",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (answer == MessageBoxResult.Cancel) return;

                if (answer == MessageBoxResult.No)
                {
                    // 全置換: 重複チェック不要
                    _dictService.ReplaceAll(imported);
                    ClearDictUndoSnapshot();
                    SaveDictionaryAndRefresh();
                    SetStatus($"CSV インポート完了（全置換）: {imported.Count} 件");
                }
                else
                {
                    // 追加モード: HashSet で O(1) 判定・1 パス振り分け
                    var existingDisplays = new HashSet<string>(
                        _dictService.Entries.Select(entry => entry.Display),
                        StringComparer.Ordinal);
                    var newEntries = new List<DictionaryEntry>();
                    var duplicates = new List<DictionaryEntry>();
                    foreach (var item in imported)
                    {
                        if (existingDisplays.Contains(item.Display))
                            duplicates.Add(item);
                        else
                            newEntries.Add(item);
                    }

                    if (duplicates.Count == 0)
                    {
                        foreach (var item in imported) _dictService.AddEntry(item);
                        SaveDictionaryAndRefresh();
                        SetStatus($"CSV インポート完了: {imported.Count} 件追加");
                    }
                    else
                    {
                        var mergeAnswer = MessageBox.Show(
                            $"インポート内訳:  新規 {newEntries.Count} 件 / 重複 {duplicates.Count} 件\n\n" +
                            "「はい」　 : 重複を上書き（既存エントリの読みを更新）\n" +
                            "「いいえ」 : 重複をスキップ（既存エントリをそのまま保持）\n" +
                            "「キャンセル」: インポートを中止",
                            "重複エントリの処理",
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Question);

                        if (mergeAnswer == MessageBoxResult.Cancel) return;

                        foreach (var item in newEntries) _dictService.AddEntry(item);

                        if (mergeAnswer == MessageBoxResult.Yes)
                            foreach (var item in duplicates) _dictService.UpdateByDisplay(item);

                        ClearDictUndoSnapshot();
                        SaveDictionaryAndRefresh();
                        string overwriteNote = mergeAnswer == MessageBoxResult.Yes
                            ? $"重複 {duplicates.Count} 件上書き"
                            : $"重複 {duplicates.Count} 件スキップ";
                        SetStatus($"CSV インポート完了: 新規 {newEntries.Count} 件追加、{overwriteNote}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"CSV インポートに失敗しました。\n\n{ex.Message}",
                    "インポートエラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Logger.Error($"CSV インポートエラー: {ex.Message}");
            }
        }

        private void MenuExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title      = "CSV エクスポート",
                Filter     = "CSV ファイル (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName   = $"dictionary_{DateTime.Now:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                CsvService.Export(dlg.FileName, _dictService.Entries);
                SetStatus($"CSV エクスポート完了: {dlg.FileName}");
                MessageBox.Show($"CSV を保存しました。\n\n{dlg.FileName}",
                    "エクスポート完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"CSV エクスポートに失敗しました。\n\n{ex.Message}",
                    "エクスポートエラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Logger.Error($"CSV エクスポートエラー: {ex.Message}");
            }
        }

        private void MenuLoadSample_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(SampleDictionaryPath))
            {
                MessageBox.Show(
                    $"サンプル辞書ファイルが見つかりません。\n{SampleDictionaryPath}",
                    "サンプル辞書", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int added = _dictService.LoadSampleDictionary(SampleDictionaryPath);
                SaveDictionaryAndRefresh();
                SetStatus($"サンプル辞書を読み込みました: {added} 件追加");
                MessageBox.Show(
                    $"サンプル辞書から {added} 件を追加しました。\n（既存エントリと重複するものはスキップしました）",
                    "サンプル辞書読み込み",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"サンプル辞書の読み込みに失敗しました。\n{ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ----------------------------------------------------------------
        // ユーティリティ
        // ----------------------------------------------------------------

        private void SaveDictionaryAndRefresh()
        {
            _dictService.Save();
            RefreshDictionaryList();
            if (!string.IsNullOrEmpty(TxtInput.Text))
                ApplyAndPreview();
        }
    }
}
