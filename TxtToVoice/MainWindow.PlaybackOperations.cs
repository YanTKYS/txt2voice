using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TxtToVoice.Models;
using TxtToVoice.Services;

namespace TxtToVoice
{
    public partial class MainWindow
    {
        // ----------------------------------------------------------------
        // フィールド（再生・保存専用）
        // ----------------------------------------------------------------

        private SpeechPositionMap? _positionMap;
        private int _speechOriginOffset;
        private DateTime _lastProgressLog = DateTime.MinValue;

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
            // 音声選択
            if (!string.IsNullOrEmpty(s.VoiceName))
            {
                int idx = CmbVoice.Items.IndexOf(s.VoiceName);
                if (idx >= 0) CmbVoice.SelectedIndex = idx;
            }
            // SSML モード
            ChkSsml.IsChecked = s.SsmlPauseEnabled;
            // 機微データ保存ポリシー
            _saveLastInputText       = s.SaveLastInputText;
            _saveRecentFiles          = s.SaveRecentFiles;
            _clearSensitiveDataOnExit = s.ClearSensitiveDataOnExit;
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

        private void SaveCurrentSettings()
        {
            _settingsService.Save(new AppSettings
            {
                Rate             = (int)SldRate.Value,
                Volume           = (int)SldVolume.Value,
                VoiceName        = _speechService.CurrentVoiceName,
                SsmlPauseEnabled = ChkSsml.IsChecked == true,
                RecentFiles      = _saveRecentFiles ? _recentFiles : new List<string>(),
                // ポリシー設定は常に保存
                SaveLastInputText        = _saveLastInputText,
                SaveRecentFiles          = _saveRecentFiles,
                ClearSensitiveDataOnExit = _clearSensitiveDataOnExit
            });
        }

        // ----------------------------------------------------------------
        // 再生操作ボタン
        // ----------------------------------------------------------------

        private void BtnPlay_Click(object sender, RoutedEventArgs e)   => StartSpeech();
        private void BtnPause_Click(object sender, RoutedEventArgs e)  => PauseSpeech();
        private void BtnResume_Click(object sender, RoutedEventArgs e) => ResumeSpeech();
        private void BtnStop_Click(object sender, RoutedEventArgs e)   => StopSpeech();

        private void StartSpeech()
        {
            bool hasSelection = TxtInput.SelectionLength > 0;
            string rawText = hasSelection ? TxtInput.SelectedText : TxtInput.Text;
            _speechOriginOffset = hasSelection ? TxtInput.SelectionStart : 0;

            if (string.IsNullOrWhiteSpace(rawText))
            {
                MessageBox.Show("読み上げるテキストがありません。",
                    "読み上げ", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool useSsml = ChkSsml.IsChecked == true;
            var (speechText, map) = _dictService.ApplyDictionaryForSpeech(rawText);

            if (useSsml)
            {
                _positionMap = null; // SSML モード時はハイライト無効
                _speechService.SpeakSsmlAsync(SsmlBuilder.Build(speechText));
            }
            else
            {
                _positionMap = map;
                _speechService.SpeakAsync(speechText);
            }
        }

        private void PauseSpeech()
        {
            if (_isSpeaking && !_isPaused)
            {
                _speechService.Pause();
                _isPaused = true;
                UpdatePlaybackButtons();
                SetStatus("一時停止中。「再開」で読み上げを続けます。");
            }
        }

        private void ResumeSpeech()
        {
            if (_isSpeaking && _isPaused)
            {
                _speechService.Resume();
                _isPaused = false;
                UpdatePlaybackButtons();
                SetStatus("読み上げ再開中...");
            }
        }

        private void StopSpeech()
        {
            _speechService.Stop();
            // SpeakCompleted イベントで状態リセットが行われる
        }

        // ----------------------------------------------------------------
        // 音声合成イベント（UI スレッドで呼ばれる）
        // ----------------------------------------------------------------

        private void OnSpeakStarted(object? sender, EventArgs e)
        {
            _isSpeaking = true;
            _isPaused   = false;
            UpdatePlaybackButtons();
            SetStatus("読み上げ中...");
        }

        private void OnSpeakCompleted(object? sender, EventArgs e)
        {
            _isSpeaking  = false;
            _isPaused    = false;
            _positionMap = null;
            UpdatePlaybackButtons();
            SetStatus("読み上げ完了。");
        }

        private void OnSpeakError(object? sender, string message)
        {
            _isSpeaking  = false;
            _isPaused    = false;
            _positionMap = null;
            UpdatePlaybackButtons();
            MessageBox.Show(
                $"読み上げ中にエラーが発生しました。\n\n{message}",
                "読み上げエラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            SetStatus($"エラー: {message}");
        }

        /// <summary>
        /// 読み上げ進捗（UI スレッドで呼ばれる）。
        /// ステータスバーは毎回更新し、Logger への書き込みは 1 秒ごとに間引く。
        /// </summary>
        private void OnSpeakProgress(object? sender, SpeakProgressInfo e)
        {
            if (_positionMap is null) return;
            var (origStart, origLen) = _positionMap.MapToOriginal(e.CharacterPosition);
            if (origStart < 0) return;

            int absEnd = Math.Min(
                origStart + _speechOriginOffset + Math.Max(origLen, 1),
                TxtInput.Text.Length);
            int total = TxtInput.Text.Length;

            // UI 更新は毎回（SetStatus は使わず直接書いてログを抑制）
            TxtStatus.Text = $"読み上げ中... ({absEnd} / {total} 文字)";

            // Logger への書き込みは 1 秒ごとに間引く
            var now = DateTime.Now;
            if ((now - _lastProgressLog).TotalSeconds >= 1.0)
            {
                Logger.Info($"[進捗] {absEnd} / {total} 文字");
                _lastProgressLog = now;
            }
        }

        private void UpdatePlaybackButtons()
        {
            if (!_speechService.IsAvailable) return;

            BtnPlay.IsEnabled    = !_isSpeaking;
            BtnPause.IsEnabled   =  _isSpeaking && !_isPaused;
            BtnResume.IsEnabled  =  _isSpeaking &&  _isPaused;
            BtnStop.IsEnabled    =  _isSpeaking;
            BtnSaveWav.IsEnabled = !_isSpeaking;
        }

        private void DisableSpeechControls()
        {
            BtnPlay.IsEnabled    = false;
            BtnPause.IsEnabled   = false;
            BtnResume.IsEnabled  = false;
            BtnStop.IsEnabled    = false;
            BtnSaveWav.IsEnabled = false;
            CmbVoice.IsEnabled   = false;
            SldRate.IsEnabled    = false;
            SldVolume.IsEnabled  = false;
        }

        // ----------------------------------------------------------------
        // 音声パラメータ
        // ----------------------------------------------------------------

        private void CmbVoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_speechService is null) return;
            if (CmbVoice.SelectedItem is string voiceName)
            {
                _speechService.SetVoice(voiceName);
                SaveCurrentSettings();
            }
        }

        private void SldRate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_speechService is null) return;
            int rate = (int)SldRate.Value;
            TxtRateVal.Text = rate.ToString("+0;-0;0");
            _speechService.SetRate(rate);
            SaveCurrentSettings();
            UpdateEstimatedTime();
        }

        // ----------------------------------------------------------------
        // 想定読み上げ時間
        // ----------------------------------------------------------------

        /// <summary>
        /// 文字数とスライダー速度から読み上げ想定時間を計算して TxtEstTime に表示する。
        /// rate=0 のとき約 5 文字/秒（300 文字/分）を基準とし、
        /// rate に応じて 2^(rate/6) 倍のスケールで変化させる。
        /// </summary>
        internal void UpdateEstimatedTime()
        {
            int charCount = TxtInput.Text.Length;
            if (charCount == 0)
            {
                TxtEstTime.Text = string.Empty;
                return;
            }

            double cps     = 5.0 * Math.Pow(2.0, SldRate.Value / 6.0);
            int    seconds = (int)Math.Ceiling(charCount / cps);

            string timeStr = seconds < 60
                ? $"{seconds} 秒"
                : seconds % 60 == 0
                    ? $"{seconds / 60} 分"
                    : $"{seconds / 60} 分 {seconds % 60} 秒";

            TxtEstTime.Text = $"  /  読み上げ 約 {timeStr}";
        }

        private void SldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_speechService is null) return;
            int vol = (int)SldVolume.Value;
            TxtVolumeVal.Text = vol.ToString();
            _speechService.SetVolume(vol);
            SaveCurrentSettings();
        }

        private void ChkSsml_Changed(object sender, RoutedEventArgs e)
        {
            SaveCurrentSettings();
        }

        // ----------------------------------------------------------------
        // 音声保存（WAV / MP3 / MP4）— 非同期
        // ----------------------------------------------------------------

        private void BtnSaveWav_Click(object sender, RoutedEventArgs e) => SaveAudio();

        private async void SaveAudio()
        {
            string rawText = TxtInput.Text;
            if (string.IsNullOrWhiteSpace(rawText))
            {
                MessageBox.Show("保存するテキストがありません。",
                    "音声保存", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool useSsml = ChkSsml.IsChecked == true;
            string speechText = _dictService.ApplyDictionary(rawText);
            string content = useSsml ? SsmlBuilder.Build(speechText) : speechText;

            var dlg = new SaveFileDialog
            {
                Title       = "音声ファイルとして保存",
                Filter      = "MP3ファイル (*.mp3)|*.mp3|WAVファイル (*.wav)|*.wav|MP4ファイル (*.mp4)|*.mp4",
                DefaultExt  = "mp3",
                FilterIndex = 1,
                FileName    = $"kouhou_{DateTime.Now:yyyyMMdd_HHmmss}"
            };
            if (dlg.ShowDialog() != true) return;

            AudioFormat format = Path.GetExtension(dlg.FileName).ToLowerInvariant() switch
            {
                ".mp3" => AudioFormat.Mp3,
                ".mp4" => AudioFormat.Mp4,
                _      => AudioFormat.Wav
            };

            SetStatus("音声保存中...");
            BtnSaveWav.IsEnabled = false;

            try
            {
                await _speechService.SaveToFileAsync(content, dlg.FileName, format, isSsml: useSsml);
                SetStatus($"音声保存完了: {Path.GetFileName(dlg.FileName)}");
                MessageBox.Show(
                    $"音声ファイルを保存しました。\n\n{dlg.FileName}",
                    "保存完了",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error($"音声保存エラー: {ex.Message}");
                MessageBox.Show(
                    $"音声ファイルの保存に失敗しました。\n\n{ex.Message}",
                    "保存エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                SetStatus("音声保存に失敗しました。");
            }
            finally
            {
                BtnSaveWav.IsEnabled = true;
            }
        }
    }
}
