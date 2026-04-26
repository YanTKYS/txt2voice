using System.Windows;
using TxtToVoice.Services;

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

        /// <summary>使用する音声エンジン種別（<see cref="SpeechEngineFactory"/> 定数、OK 押下後に確定）</summary>
        public string SpeechEngineType { get; private set; } = SpeechEngineFactory.Default;

        /// <summary>監査ログ保持月数（0 = 無制限、OK 押下後に確定）</summary>
        public int AuditRetentionMonths { get; private set; } = 13;

        public SettingsDialog(bool saveLastInputText, bool saveRecentFiles,
            bool clearSensitiveDataOnExit, bool deleteLogOnExit,
            string speechEngineType, int auditRetentionMonths = 13)
        {
            InitializeComponent();
            ChkSaveLastInputText.IsChecked        = saveLastInputText;
            ChkSaveRecentFiles.IsChecked           = saveRecentFiles;
            ChkClearSensitiveDataOnExit.IsChecked  = clearSensitiveDataOnExit;
            ChkDeleteLogOnExit.IsChecked           = deleteLogOnExit;
            RbWinRt.IsChecked        = speechEngineType == SpeechEngineFactory.WinRt;
            RbOpenJTalk.IsChecked    = speechEngineType == SpeechEngineFactory.OpenJTalk;
            RbSystemSpeech.IsChecked = speechEngineType != SpeechEngineFactory.WinRt
                                    && speechEngineType != SpeechEngineFactory.OpenJTalk;

            // 保持期間 ComboBox の初期選択
            foreach (System.Windows.Controls.ComboBoxItem item in CmbAuditRetention.Items)
            {
                if ((int)item.Tag == auditRetentionMonths)
                { CmbAuditRetention.SelectedItem = item; break; }
            }
            if (CmbAuditRetention.SelectedItem == null)
                CmbAuditRetention.SelectedIndex = 3; // 既定: 13か月
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            SaveLastInputText       = ChkSaveLastInputText.IsChecked        == true;
            SaveRecentFiles          = ChkSaveRecentFiles.IsChecked           == true;
            ClearSensitiveDataOnExit = ChkClearSensitiveDataOnExit.IsChecked  == true;
            DeleteLogOnExit          = ChkDeleteLogOnExit.IsChecked           == true;
            SpeechEngineType         = RbWinRt.IsChecked    == true ? SpeechEngineFactory.WinRt
                                     : RbOpenJTalk.IsChecked == true ? SpeechEngineFactory.OpenJTalk
                                     : SpeechEngineFactory.SystemSpeech;
            AuditRetentionMonths     = CmbAuditRetention.SelectedItem is System.Windows.Controls.ComboBoxItem ci
                                     ? (int)ci.Tag : 13;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
