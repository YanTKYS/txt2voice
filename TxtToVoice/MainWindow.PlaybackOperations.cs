using System;
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

        // ----------------------------------------------------------------
        // 設定の読み込み・保存
        // ----------------------------------------------------------------

        /// <summary>設定ファイルからスライダー値・音声名を復元する。InitializeVoiceCombo の後に呼ぶこと。</summary>
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
            Logger.Info($"設定を読み込みました: Rate={s.Rate}, Volume={s.Volume}, Voice={s.VoiceName}");
        }

        private void SaveCurrentSettings()
        {
            _settingsService.Save(new AppSettings
            {
                Rate      = (int)SldRate.Value,
                Volume    = (int)SldVolume.Value,
                VoiceName = _speechService.CurrentVoiceName
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

            var (speechText, map) = _dictService.ApplyDictionaryForSpeech(rawText);
            _positionMap = map;
            _speechService.SpeakAsync(speechText);
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
            // SpeakCompleted イベントで状態リセット・ハイライトクリアが行われる
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
            TxtInput.Focus(); // ハイライト表示のためフォーカスを当てる
        }

        private void OnSpeakCompleted(object? sender, EventArgs e)
        {
            _isSpeaking  = false;
            _isPaused    = false;
            _positionMap = null;
            UpdatePlaybackButtons();
            SetStatus("読み上げ完了。");
            TxtInput.Select(0, 0); // ハイライトをクリア
        }

        private void OnSpeakError(object? sender, string message)
        {
            _isSpeaking  = false;
            _isPaused    = false;
            _positionMap = null;
            UpdatePlaybackButtons();
            TxtInput.Select(0, 0); // ハイライトをクリア
            MessageBox.Show(
                $"読み上げ中にエラーが発生しました。\n\n{message}",
                "読み上げエラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            SetStatus($"エラー: {message}");
        }

        /// <summary>読み上げ進捗ハイライト（UI スレッドで呼ばれる）</summary>
        private void OnSpeakProgress(object? sender, SpeakProgressInfo e)
        {
            if (_positionMap is null) return;
            var (origStart, origLen) = _positionMap.MapToOriginal(e.CharacterPosition);
            if (origStart < 0) return;

            int absStart = origStart + _speechOriginOffset;
            int absLen   = Math.Max(origLen, 1);
            // テキスト境界を超えないようにクランプ
            absLen = Math.Min(absLen, TxtInput.Text.Length - absStart);
            if (absLen <= 0) return;

            TxtInput.Select(absStart, absLen);
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
        }

        private void SldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_speechService is null) return;
            int vol = (int)SldVolume.Value;
            TxtVolumeVal.Text = vol.ToString();
            _speechService.SetVolume(vol);
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

            string speechText = _dictService.ApplyDictionary(rawText);

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
                await _speechService.SaveToFileAsync(speechText, dlg.FileName, format);
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
