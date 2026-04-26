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
            // 読み上げ位置ハイライト
            ChkHighlight.IsChecked = s.ShowReadingHighlight;
            // 機微データ保存ポリシー
            _saveLastInputText       = s.SaveLastInputText;
            _saveRecentFiles          = s.SaveRecentFiles;
            _clearSensitiveDataOnExit = s.ClearSensitiveDataOnExit;
            _deleteLogOnExit          = s.DeleteLogOnExit;
            _speechEngineType         = s.SpeechEngineType;
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
                _recentFiles);

        // ----------------------------------------------------------------
        // 設定ダイアログ
        // ----------------------------------------------------------------

        private void MenuTextRules_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.TextRuleDialog(PathConfig.EffectiveTextRulesPath) { Owner = this };
            if (dlg.ShowDialog() == true && _playback == PlaybackState.Idle)
            {
                // ルール保存後、アイドル中はエンジンを即時差し替えて変更を反映する
                _speechService.ReplaceEngine(SpeechEngineFactory.Create(_speechEngineType));
                Logger.Info("読みルール変更によりエンジンを即時再起動しました。");
            }
        }

        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsDialog(
                _saveLastInputText, _saveRecentFiles,
                _clearSensitiveDataOnExit, _deleteLogOnExit,
                _speechEngineType)
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

            SaveCurrentSettings();
            UpdateRecentFilesMenu();
        }
    }
}
