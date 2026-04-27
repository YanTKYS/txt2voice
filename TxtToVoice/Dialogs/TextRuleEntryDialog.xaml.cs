using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using TxtToVoice.Models;

namespace TxtToVoice.Dialogs
{
    public partial class TextRuleEntryDialog : Window
    {
        public TextRule? Result { get; private set; }

        public TextRuleEntryDialog(TextRule? initial = null)
        {
            InitializeComponent();

            if (initial != null)
            {
                TxtPattern.Text      = initial.Pattern;
                TxtReplacement.Text  = initial.Replacement;
                TxtDescription.Text  = initial.Description;
                ChkEnabled.IsChecked = initial.Enabled;
            }

            // 初期状態のバリデーション
            ValidatePattern(TxtPattern.Text);
        }

        private void TxtPattern_TextChanged(object sender, TextChangedEventArgs e)
            => ValidatePattern(TxtPattern.Text);

        private bool ValidatePattern(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                TxtError.Text = "パターンを入力してください。";
                TxtError.Visibility = Visibility.Visible;
                BtnOk.IsEnabled = false;
                return false;
            }

            try
            {
                _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(500));
                TxtError.Visibility = Visibility.Collapsed;
                BtnOk.IsEnabled = true;
                return true;
            }
            catch (ArgumentException ex)
            {
                TxtError.Text = $"正規表現エラー: {ex.Message}";
                TxtError.Visibility = Visibility.Visible;
                BtnOk.IsEnabled = false;
                return false;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidatePattern(TxtPattern.Text)) return;

            Result = new TextRule
            {
                Pattern     = TxtPattern.Text.Trim(),
                Replacement = TxtReplacement.Text,
                Description = TxtDescription.Text.Trim(),
                Enabled     = ChkEnabled.IsChecked == true,
            };
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
