using System.Windows;

namespace TxtToVoice.Dialogs
{
    /// <summary>
    /// 機微データ保存ポリシーを設定するダイアログ。
    /// </summary>
    public partial class SettingsDialog : Window
    {
        /// <summary>前回テキストを保存するかどうか（OK 押下後に確定）</summary>
        public bool SaveLastInputText { get; private set; }

        /// <summary>最近使ったファイルリストを保存するかどうか（OK 押下後に確定）</summary>
        public bool SaveRecentFiles { get; private set; }

        /// <summary>終了時に機微データを消去するかどうか（OK 押下後に確定）</summary>
        public bool ClearSensitiveDataOnExit { get; private set; }

        public SettingsDialog(bool saveLastInputText, bool saveRecentFiles, bool clearSensitiveDataOnExit)
        {
            InitializeComponent();
            ChkSaveLastInputText.IsChecked        = saveLastInputText;
            ChkSaveRecentFiles.IsChecked           = saveRecentFiles;
            ChkClearSensitiveDataOnExit.IsChecked  = clearSensitiveDataOnExit;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            SaveLastInputText       = ChkSaveLastInputText.IsChecked        == true;
            SaveRecentFiles          = ChkSaveRecentFiles.IsChecked           == true;
            ClearSensitiveDataOnExit = ChkClearSensitiveDataOnExit.IsChecked  == true;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
