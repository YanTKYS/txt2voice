using System.Windows;
using System.Windows.Controls;

namespace TxtToVoice.Dialogs
{
    public partial class InputDialog : Window
    {
        public string Result { get; private set; } = "";

        public InputDialog(string title, string prompt, string initial = "")
        {
            InitializeComponent();
            Title = title;
            TxtPrompt.Text = prompt;
            TxtValue.Text  = initial;
            BtnOk.IsEnabled = !string.IsNullOrWhiteSpace(initial);
        }

        private void TxtValue_TextChanged(object sender, TextChangedEventArgs e)
            => BtnOk.IsEnabled = !string.IsNullOrWhiteSpace(TxtValue.Text);

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = TxtValue.Text.Trim();
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
