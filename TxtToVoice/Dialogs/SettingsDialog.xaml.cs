using System.Windows;

namespace TxtToVoice.Dialogs
{
    /// <summary>
    /// 音声エンジン種別・機微データ保存ポリシーを設定するダイアログ。
    /// </summary>
    public partial class SettingsDialog : Window
    {
        /// <summary>前回テキストを保存するかどうか（OK 押下後に確定）</summary>
        public bool SaveLastInputText { get; private set; }

        /// <summary>最近使ったファイルリストを保存するかどうか（OK 押下後に確定）</summary>
        public bool SaveRecentFiles { get; private set; }

        /// <summary>終了時に機微データを消去するかどうか（OK 押下後に確定）</summary>
        public bool ClearSensitiveDataOnExit { get; private set; }

        /// <summary>終了時にログファイルを削除するかどうか（OK 押下後に確定）</summary>
        public bool DeleteLogOnExit { get; private set; }

        /// <summary>使用する音声エンジン種別（"SystemSpeech" または "WinRT"、OK 押下後に確定）</summary>
        public string SpeechEngineType { get; private set; } = "SystemSpeech";

        public SettingsDialog(bool saveLastInputText, bool saveRecentFiles,
            bool clearSensitiveDataOnExit, bool deleteLogOnExit,
            string speechEngineType)
        {
            InitializeComponent();
            ChkSaveLastInputText.IsChecked        = saveLastInputText;
            ChkSaveRecentFiles.IsChecked           = saveRecentFiles;
            ChkClearSensitiveDataOnExit.IsChecked  = clearSensitiveDataOnExit;
            ChkDeleteLogOnExit.IsChecked           = deleteLogOnExit;
            RbWinRt.IsChecked       = speechEngineType == "WinRT";
            RbSystemSpeech.IsChecked = speechEngineType != "WinRT";
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            SaveLastInputText       = ChkSaveLastInputText.IsChecked        == true;
            SaveRecentFiles          = ChkSaveRecentFiles.IsChecked           == true;
            ClearSensitiveDataOnExit = ChkClearSensitiveDataOnExit.IsChecked  == true;
            DeleteLogOnExit          = ChkDeleteLogOnExit.IsChecked           == true;
            SpeechEngineType         = RbWinRt.IsChecked == true ? "WinRT" : "SystemSpeech";
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
