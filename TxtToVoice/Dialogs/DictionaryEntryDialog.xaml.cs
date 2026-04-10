using System;
using System.Windows;
using TxtToVoice.Models;

namespace TxtToVoice.Dialogs
{
    /// <summary>
    /// 辞書エントリの追加・編集ダイアログ。
    /// </summary>
    public partial class DictionaryEntryDialog : Window
    {
        private readonly Action<string>? _speakAction;

        /// <summary>OKボタン押下後に結果エントリを保持する。</summary>
        public DictionaryEntry? Result { get; private set; }

        public DictionaryEntryDialog(DictionaryEntry? existing = null, Action<string>? speakAction = null)
        {
            InitializeComponent();
            _speakAction = speakAction;
            BtnSpeak.IsEnabled = speakAction != null;

            if (existing != null)
            {
                Title = "辞書エントリ編集";
                TxtDisplay.Text  = existing.Display;
                TxtReading.Text  = existing.Reading;
                TxtRemarks.Text  = existing.Remarks;
                TxtPriority.Text = existing.Priority.ToString();
            }
            else
            {
                Title = "辞書エントリ追加";
            }

            Loaded += (_, _) => TxtDisplay.Focus();
        }

        private void BtnSpeak_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtReading.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("「読み」を入力してから試し読みを行ってください。",
                    "試し読み", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _speakAction?.Invoke(text);
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // バリデーション
            if (string.IsNullOrWhiteSpace(TxtDisplay.Text))
            {
                MessageBox.Show("「表記」を入力してください。", "入力エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtDisplay.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(TxtReading.Text))
            {
                MessageBox.Show("「読み」を入力してください。", "入力エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtReading.Focus();
                return;
            }
            if (!int.TryParse(TxtPriority.Text, out int priority) || priority < 1 || priority > 100)
            {
                MessageBox.Show("「優先順位」は 1〜100 の整数で入力してください。", "入力エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPriority.Focus();
                return;
            }

            Result = new DictionaryEntry
            {
                Display  = TxtDisplay.Text.Trim(),
                Reading  = TxtReading.Text.Trim(),
                Remarks  = TxtRemarks.Text.Trim(),
                Priority = priority
            };
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
