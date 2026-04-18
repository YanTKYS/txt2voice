using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
            TxtDictCount.Text = $"辞書: {_entries.Count} 件";
        }

        // ----------------------------------------------------------------
        // プレビュー
        // ----------------------------------------------------------------

        private void BtnApplyDictionary_Click(object sender, RoutedEventArgs e) => ApplyAndPreview();

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

            SetStatus("辞書を適用してプレビューを更新しました。");
        }

        private void PreviewMode_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.IsChecked != true) return;
            _annotatedPreview = RbPreviewAnnotated.IsChecked == true;
            if (_dictService is null) return;
            if (!string.IsNullOrEmpty(TxtInput.Text))
                ApplyAndPreview();
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
                SaveDictionaryAndRefresh();
                SetStatus($"辞書に追加しました: 「{dlg.Result.Display}」→「{dlg.Result.Reading}」");
            }
        }

        private void BtnEditEntry_Click(object sender, RoutedEventArgs e)       => EditSelectedEntry();
        private void DgDictionary_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => EditSelectedEntry();

        private void EditSelectedEntry()
        {
            int idx = DgDictionary.SelectedIndex;
            if (idx < 0 || idx >= _dictService.Entries.Count) return;

            var entry = _dictService.Entries[idx];
            Action<string>? speakAction = _speechService.IsAvailable ? _speechService.SpeakAsync : null;
            var dlg   = new DictionaryEntryDialog(entry.Clone(), speakAction) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                _dictService.UpdateEntry(idx, dlg.Result);
                SaveDictionaryAndRefresh();
                SetStatus($"辞書を更新しました: 「{dlg.Result.Display}」→「{dlg.Result.Reading}」");
            }
        }

        private void BtnDeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            int idx = DgDictionary.SelectedIndex;
            if (idx < 0) return;

            var entry  = _dictService.Entries[idx];
            var result = MessageBox.Show(
                $"「{entry.Display}」→「{entry.Reading}」を辞書から削除しますか？",
                "削除確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _dictService.RemoveEntry(idx);
                SaveDictionaryAndRefresh();
                SetStatus($"辞書から削除しました: 「{entry.Display}」");
            }
        }

        private void DgDictionary_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Insert: BtnAddEntry_Click(sender, e);    e.Handled = true; break;
                case Key.F2:     EditSelectedEntry();              e.Handled = true; break;
                case Key.Delete: BtnDeleteEntry_Click(sender, e); e.Handled = true; break;
            }
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
                var imported = CsvService.Import(dlg.FileName);
                if (imported.Count == 0)
                {
                    MessageBox.Show(
                        "インポートできるエントリがありませんでした。\nCSVの形式（表記,読み,備考,優先順位）を確認してください。",
                        "インポート", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var answer = MessageBox.Show(
                    $"{imported.Count} 件のエントリをインポートします。\n\n" +
                    "「はい」: 現在の辞書に追加します\n" +
                    "「いいえ」: 現在の辞書を置き換えます",
                    "インポート確認",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (answer == MessageBoxResult.Cancel) return;

                if (answer == MessageBoxResult.No)
                {
                    // 全置換: 重複チェック不要
                    _dictService.ReplaceAll(imported);
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
