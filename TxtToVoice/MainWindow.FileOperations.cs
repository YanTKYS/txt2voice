using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
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
            _recentFiles.Remove(path);
            _recentFiles.Insert(0, path);
            if (_recentFiles.Count > MaxRecentFiles)
                _recentFiles.RemoveRange(MaxRecentFiles, _recentFiles.Count - MaxRecentFiles);
            SaveCurrentSettings();
            UpdateRecentFilesMenu();
        }

        /// <summary>BOM → UTF-8 → Shift_JIS の順で自動判別して読み込む。</summary>
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
    }
}
