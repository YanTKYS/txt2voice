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
                TxtInput.Text = ReadTextFileWithFallback(dlg.FileName);
                SetStatus($"ファイルを読み込みました: {Path.GetFileName(dlg.FileName)}");
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
        }
    }
}
