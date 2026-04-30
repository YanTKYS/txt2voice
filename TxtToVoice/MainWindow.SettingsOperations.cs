using System;
using System.Windows;
using System.Windows.Controls;
using TxtToVoice.Dialogs;
using TxtToVoice.Models;
using TxtToVoice.Services;

namespace TxtToVoice
{
    public partial class MainWindow
    {
        // ----------------------------------------------------------------
        // 設定の読み込み・保存
        // ----------------------------------------------------------------

        /// <summary>設定ファイルからスライダー値・音声名・SSML・前回テキストを復元する。InitializeVoiceCombo の後に呼ぶこと。</summary>
        internal void LoadSettings()
        {
            var s = _settingsService.Load();
            // スライダー設定（ValueChanged → SetRate/SetVolume が呼ばれる）
            SldRate.Value   = Math.Clamp(s.Rate,   -10, 10);
            SldVolume.Value = Math.Clamp(s.Volume,   0, 100);
            // 音声選択: VoiceId（内部識別子）で検索し、見つからない場合は VoiceName にフォールバック
            string? voiceDisplayName = null;
            if (!string.IsNullOrEmpty(s.VoiceId))
                voiceDisplayName = _speechService.FindVoiceNameById(s.VoiceId);
            if (voiceDisplayName == null && !string.IsNullOrEmpty(s.VoiceName))
                voiceDisplayName = s.VoiceName;
            if (voiceDisplayName != null)
            {
                int idx = CmbVoice.Items.IndexOf(voiceDisplayName);
                if (idx >= 0) CmbVoice.SelectedIndex = idx;
            }
            // SSML モード
            ChkSsml.IsChecked = s.SsmlPauseEnabled;
            // 読み上げ位置ハイライト（SSML ON のときは無効）
            ChkHighlight.IsEnabled = !s.SsmlPauseEnabled;
            ChkHighlight.IsChecked = s.ShowReadingHighlight;
            // 機微データ保存ポリシー
            _saveLastInputText       = s.SaveLastInputText;
            _saveRecentFiles          = s.SaveRecentFiles;
            _clearSensitiveDataOnExit = s.ClearSensitiveDataOnExit;
            _deleteLogOnExit          = s.DeleteLogOnExit;
            _speechEngineType         = s.SpeechEngineType;
            _auditRetentionMonths     = s.AuditRetentionMonths;
            _saveFilePrefix           = s.SaveFilePrefix;
            _fileNameTemplate         = s.FileNameTemplate;
            _batchSaveFormats         = s.BatchSaveFormats;
            // SSML ポーズ強度
            CmbSsmlStrength.SelectedIndex = Math.Clamp(s.SsmlPauseStrength, 0, 2);
            // プレビュー設定
            ChkAutoPreview.IsChecked    = s.AutoPreviewEnabled;
            _annotatedPreview           = s.AnnotatedPreviewMode;
            RbPreviewAnnotated.IsChecked = s.AnnotatedPreviewMode;
            RbPreviewClean.IsChecked     = !s.AnnotatedPreviewMode;
            // 表示設定
            ToggleLineNumbers(s.ShowLineNumbers);
            // キュー永続化
            _persistQueue = s.PersistQueue;
            LoadQueue();
            // 監査モードを Logger に即時反映（INFO ログ抑制）
            Logger.SuppressInfo = _clearSensitiveDataOnExit;
            // 前回セッションのテキストを復元（ポリシーが許可している場合のみ）
            if (_saveLastInputText && !string.IsNullOrEmpty(s.LastInputText))
                TxtInput.Text = s.LastInputText;
            // 最近使ったファイル（ポリシーが許可している場合のみ）
            if (_saveRecentFiles)
            {
                _recentFiles.Clear();
                _recentFiles.AddRange(s.RecentFiles);
            }
            UpdateRecentFilesMenu();
            UpdateEstimatedTime();
            LoadProfiles();
            LoadSavePresets();
            Logger.Info($"設定を読み込みました: Rate={s.Rate}, Volume={s.Volume}, Voice={s.VoiceName}, Ssml={s.SsmlPauseEnabled}");
        }

        internal void SaveCurrentSettings()
        {
            _settingsService.Save(BuildAppSettings(isExit: false));
        }

        /// <summary>
        /// 現在の UI 状態・ポリシーから <see cref="AppSettings"/> を組み立てる。
        /// <paramref name="isExit"/> が true のときは終了時の機微データ消去ポリシーを適用し、
        /// LastInputText および RecentFiles を <c>_clearSensitiveDataOnExit</c> でフィルタする。
        /// 終了時以外（設定変更等）では LastInputText は保存対象に含めない（従来動作）。
        /// ロジックの詳細は <see cref="AppSettingsBuilder.Build"/> を参照。
        /// </summary>
        internal AppSettings BuildAppSettings(bool isExit)
            => AppSettingsBuilder.Build(
                isExit,
                (int)SldRate.Value,
                (int)SldVolume.Value,
                _speechService.CurrentVoiceName,
                _speechService.CurrentVoiceId,
                ChkSsml.IsChecked    == true,
                ChkHighlight.IsChecked == true,
                _saveLastInputText,
                _saveRecentFiles,
                _clearSensitiveDataOnExit,
                _deleteLogOnExit,
                _speechEngineType,
                TxtInput.Text,
                _recentFiles,
                _auditRetentionMonths,
                ChkAutoPreview.IsChecked == true,
                _annotatedPreview,
                CmbSsmlStrength.SelectedIndex,
                _saveFilePrefix,
                _fileNameTemplate,
                _batchSaveFormats,
                _showLineNumbers,
                _persistQueue);

        // ----------------------------------------------------------------
        // 設定ダイアログ
        // ----------------------------------------------------------------

        private void MenuTextRules_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.TextRuleDialog(PathConfig.EffectiveTextRulesPath) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            if (_playback == PlaybackState.Idle)
            {
                // アイドル中: エンジンを即時差し替えて変更を反映する (#71)
                _speechService.ReplaceEngine(SpeechEngineFactory.Create(_speechEngineType));
                InitializeVoiceCombo();
                _speechService.SetRate((int)SldRate.Value);
                _speechService.SetVolume((int)SldVolume.Value);
                Logger.Info("読みルール変更によりエンジンを即時再起動しました。");
                SetStatus("読みルールを保存しました。");
            }
            else
            {
                // 再生中: 即時反映できないためユーザーに通知する (#77)
                Logger.Info("読みルールを保存しましたが、再生中のため即時反映を延期しました。");
                SetStatus("読みルールを保存しました。次回エンジン起動時に反映されます。");
            }
        }

        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsDialog(
                _saveLastInputText, _saveRecentFiles,
                _clearSensitiveDataOnExit, _deleteLogOnExit,
                _speechEngineType, _auditRetentionMonths,
                _saveFilePrefix, _fileNameTemplate, _batchSaveFormats,
                _showLineNumbers, _persistQueue)
            {
                Owner = this
            };
            if (dlg.ShowDialog() != true) return;

            string previousEngineType = _speechEngineType;

            _saveLastInputText       = dlg.SaveLastInputText;
            _saveRecentFiles          = dlg.SaveRecentFiles;
            _clearSensitiveDataOnExit = dlg.ClearSensitiveDataOnExit;
            _deleteLogOnExit          = dlg.DeleteLogOnExit;
            _speechEngineType         = dlg.SpeechEngineType;
            _auditRetentionMonths     = dlg.AuditRetentionMonths;
            _saveFilePrefix           = dlg.SaveFilePrefix;
            _fileNameTemplate         = dlg.FileNameTemplate;
            _batchSaveFormats         = dlg.BatchSaveFormats;
            ToggleLineNumbers(dlg.ShowLineNumbers);
            _persistQueue = dlg.PersistQueue;
            if (_persistQueue) SaveQueue();

            // 監査モードの変化を Logger にも即時反映する
            Logger.SuppressInfo = _clearSensitiveDataOnExit;

            // _saveRecentFiles が false になった場合はメモリ上の履歴も消去
            if (!_saveRecentFiles)
                _recentFiles.Clear();

            // 再生停止中かつエンジン種別が変わった場合は即時切替
            if (_speechEngineType != previousEngineType && _playback == PlaybackState.Idle)
            {
                _speechService.ReplaceEngine(SpeechEngineFactory.Create(_speechEngineType));
                InitializeVoiceCombo();
                _speechService.SetRate((int)SldRate.Value);
                _speechService.SetVolume((int)SldVolume.Value);
                Logger.Info($"エンジンを即時切替: {SpeechEngineFactory.GetLabel(_speechEngineType)}");
            }

            // 保持期間変更を即時適用
            AuditLogger.PurgeOldLogs(_auditRetentionMonths);

            SaveCurrentSettings();
            UpdateRecentFilesMenu();
        }
    }
}
