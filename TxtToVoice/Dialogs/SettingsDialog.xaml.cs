using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

        /// <summary>音声保存ファイル名プレフィックス（{prefix} 変数の値、OK 押下後に確定）</summary>
        public string SaveFilePrefix { get; private set; } = "kouhou";

        /// <summary>ファイル名命名テンプレート（OK 押下後に確定）</summary>
        public string FileNameTemplate { get; private set; } = "{prefix}_{datetime}";

        /// <summary>一括保存形式リスト（OK 押下後に確定）</summary>
        public List<string> BatchSaveFormats { get; private set; } = new() { "mp3" };

        public SettingsDialog(bool saveLastInputText, bool saveRecentFiles,
            bool clearSensitiveDataOnExit, bool deleteLogOnExit,
            string speechEngineType, int auditRetentionMonths = 13,
            string saveFilePrefix = "kouhou",
            string fileNameTemplate = "{prefix}_{datetime}",
            IEnumerable<string>? batchSaveFormats = null)
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

            foreach (ComboBoxItem item in CmbAuditRetention.Items)
            {
                if (Convert.ToInt32(item.Tag) == auditRetentionMonths)
                { CmbAuditRetention.SelectedItem = item; break; }
            }
            if (CmbAuditRetention.SelectedItem == null)
                CmbAuditRetention.SelectedIndex = 3;

            TxtSaveFilePrefix.Text    = saveFilePrefix;
            TxtFileNameTemplate.Text  = fileNameTemplate;
            UpdateFileNamePreview();

            var formats = batchSaveFormats?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string> { "mp3" };
            ChkBatchMp3.IsChecked = formats.Contains("mp3");
            ChkBatchWav.IsChecked = formats.Contains("wav");
            ChkBatchMp4.IsChecked = formats.Contains("mp4");
        }

        private void TxtSaveFilePrefix_TextChanged(object sender, TextChangedEventArgs e)
            => UpdateFileNamePreview();

        private void TxtFileNameTemplate_TextChanged(object sender, TextChangedEventArgs e)
            => UpdateFileNamePreview();

        private void UpdateFileNamePreview()
        {
            string prefix   = string.IsNullOrWhiteSpace(TxtSaveFilePrefix.Text)   ? "kouhou"            : TxtSaveFilePrefix.Text.Trim();
            string template = string.IsNullOrWhiteSpace(TxtFileNameTemplate.Text) ? "{prefix}_{datetime}" : TxtFileNameTemplate.Text.Trim();
            TxtFileNamePreview.Text = FileNameBuilder.Preview(template, prefix) + ".mp3";
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
            AuditRetentionMonths = CmbAuditRetention.SelectedItem is ComboBoxItem ci
                                 ? Convert.ToInt32(ci.Tag) : 13;
            SaveFilePrefix    = string.IsNullOrWhiteSpace(TxtSaveFilePrefix.Text)
                              ? "kouhou" : TxtSaveFilePrefix.Text.Trim();
            FileNameTemplate  = string.IsNullOrWhiteSpace(TxtFileNameTemplate.Text)
                              ? "{prefix}_{datetime}" : TxtFileNameTemplate.Text.Trim();
            BatchSaveFormats = new List<string>();
            if (ChkBatchMp3.IsChecked == true) BatchSaveFormats.Add("mp3");
            if (ChkBatchWav.IsChecked == true) BatchSaveFormats.Add("wav");
            if (ChkBatchMp4.IsChecked == true) BatchSaveFormats.Add("mp4");
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
