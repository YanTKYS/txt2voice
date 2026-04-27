using System.Windows;
using System.Windows.Controls;
using TxtToVoice.Models;

namespace TxtToVoice.Dialogs
{
    public partial class TemplateEntryDialog : Window
    {
        public Template? Result { get; private set; }

        public TemplateEntryDialog(Template? initial = null)
        {
            InitializeComponent();

            if (initial != null)
            {
                TxtTitle.Text   = initial.Title;
                TxtContent.Text = initial.Content;
            }

            ValidateTitle(TxtTitle.Text);
        }

        private void TxtTitle_TextChanged(object sender, TextChangedEventArgs e)
            => ValidateTitle(TxtTitle.Text);

        private bool ValidateTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                TxtError.Text = "タイトルを入力してください。";
                TxtError.Visibility = Visibility.Visible;
                BtnOk.IsEnabled = false;
                return false;
            }
            TxtError.Visibility = Visibility.Collapsed;
            BtnOk.IsEnabled = true;
            return true;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateTitle(TxtTitle.Text)) return;

            Result = new Template
            {
                Title   = TxtTitle.Text.Trim(),
                Content = TxtContent.Text,
            };
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
