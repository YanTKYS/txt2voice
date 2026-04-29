using System.Collections.Generic;
using System.Windows;

namespace TxtToVoice.Dialogs
{
    public partial class BatchSaveResultDialog : Window
    {
        private readonly string _pathText;

        public BatchSaveResultDialog(IEnumerable<string> savedPaths, IEnumerable<string> errorMessages)
        {
            InitializeComponent();

            var paths  = new List<string>(savedPaths);
            var errors = new List<string>(errorMessages);

            TxtHeader.Text = paths.Count > 0
                ? $"{paths.Count} ファイルを保存しました。"
                : "保存に失敗しました。";

            var lines = new List<string>(paths);
            if (errors.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("【エラー】");
                lines.AddRange(errors);
            }
            _pathText = string.Join("\n", lines);
            TxtPaths.Text = _pathText;
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
            => Clipboard.SetText(_pathText);

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
