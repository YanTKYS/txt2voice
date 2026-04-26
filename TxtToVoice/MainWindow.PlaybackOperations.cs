using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        // 蛍光イエロー — システム選択色（青）と明確に区別できる「蛍光ペン」色
        private static readonly SolidColorBrush HighlightBrush =
            new(Color.FromRgb(0xFF, 0xEB, 0x3B));   // Material Yellow 500

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
                _speechService.SpeakSsmlAsync(SsmlBuilder.Build(speechText, CmbSsmlStrength.SelectedIndex));
            }
            else
            {
                _positionMap = map;
                _speechService.SpeakAsync(speechText);
            }
        }

        private void PauseSpeech()
        {
            if (_playback.IsSpeaking && !_playback.IsPaused)
            {
                _speechService.Pause();
                _playback = PlaybackState.Paused;
                UpdatePlaybackButtons();
                SetStatus("一時停止中。「再開」で読み上げを続けます。");
            }
        }

        private void ResumeSpeech()
        {
            if (_playback.IsSpeaking && _playback.IsPaused)
            {
                _speechService.Resume();
                _playback = PlaybackState.Active;
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
            _playback = PlaybackState.Active;
            UpdatePlaybackButtons();
            SetStatus("読み上げ中...");
        }

        private void OnSpeakCompleted(object? sender, EventArgs e)
        {
            _playback    = PlaybackState.Idle;
            _positionMap = null;
            UpdatePlaybackButtons();
            ClearReadingHighlight();
            SetStatus("読み上げ完了。");
        }

        private void OnSpeakError(object? sender, string message)
        {
            _playback    = PlaybackState.Idle;
            _positionMap = null;
            UpdatePlaybackButtons();
            ClearReadingHighlight();
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
        /// ハイライト ON 時は蛍光イエローで現在単語をハイライト表示する。
        /// </summary>
        private void OnSpeakProgress(object? sender, SpeakProgressInfo e)
        {
            if (_positionMap is null) return;
            var (origStart, origLen) = _positionMap.MapToOriginal(e.CharacterPosition);
            if (origStart < 0) return;

            int absStart = origStart + _speechOriginOffset;
            int len      = Math.Min(Math.Max(origLen, 1), TxtInput.Text.Length - absStart);
            int absEnd   = absStart + len;
            int total    = TxtInput.Text.Length;

            // UI 更新は毎回（SetStatus は使わず直接書いてログを抑制）
            TxtStatus.Text = $"読み上げ中... ({absEnd} / {total} 文字)";

            // 蛍光ハイライト（チェック ON かつ非 SSML モードの場合のみ）
            if (ChkHighlight.IsChecked == true && len > 0)
            {
                // 蛍光イエロー = 「蛍光ペン」の視覚的連想。通常の選択色（青）と明確に区別できる
                TxtInput.SelectionBrush     = HighlightBrush;
                TxtInput.SelectionTextBrush = Brushes.Black;
                TxtInput.Select(absStart, len);

                // 読み上げ位置が画面に入るようスクロール
                int lineIdx = TxtInput.GetLineIndexFromCharacterIndex(absStart);
                if (lineIdx >= 0) TxtInput.ScrollToLine(lineIdx);
            }

            // Logger への書き込みは 1 秒ごとに間引く
            var now = DateTime.Now;
            if ((now - _lastProgressLog).TotalSeconds >= 1.0)
            {
                Logger.Info($"[進捗] {absEnd} / {total} 文字");
                _lastProgressLog = now;
            }
        }

        /// <summary>ハイライト（蛍光色選択）を解除し、SelectionBrush をシステム既定に戻す。</summary>
        private void ClearReadingHighlight()
        {
            TxtInput.SelectionBrush     = SystemColors.HighlightBrush;
            TxtInput.SelectionTextBrush = SystemColors.HighlightTextBrush;
            TxtInput.Select(0, 0);
        }

        private void UpdatePlaybackButtons()
        {
            if (!_speechService.IsAvailable) return;

            BtnPlay.IsEnabled    = !_playback.IsSpeaking;
            BtnPause.IsEnabled   =  _playback.IsSpeaking && !_playback.IsPaused;
            BtnResume.IsEnabled  =  _playback.IsSpeaking &&  _playback.IsPaused;
            BtnStop.IsEnabled    =  _playback.IsSpeaking;
            BtnSaveWav.IsEnabled = !_playback.IsSpeaking;
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
            if (_speechService is null) return;
            bool ssml = ChkSsml.IsChecked == true;
            ChkHighlight.IsEnabled = !ssml;
            if (ssml) ChkHighlight.IsChecked = false;
            SaveCurrentSettings();
        }

        private void CmbSsmlStrength_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_speechService is null) return;
            SaveCurrentSettings();
        }

        private void ChkHighlight_Changed(object sender, RoutedEventArgs e)
        {
            if (_speechService is null) return;
            // ハイライトを OFF にした場合、再生中なら既存のハイライトをすぐ消す
            if (ChkHighlight.IsChecked == false && _playback.IsSpeaking)
                ClearReadingHighlight();
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
            string content = useSsml ? SsmlBuilder.Build(speechText, CmbSsmlStrength.SelectedIndex) : speechText;

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

            using var cts = new CancellationTokenSource();
            var progressDialog = new Dialogs.SaveProgressDialog(cts) { Owner = this };
            progressDialog.UpdateProgress(5);
            var progress = new Progress<string>(msg =>
            {
                progressDialog.UpdatePhase(msg);
                int pct = msg.Contains("エンコード") ? 70 : 30;
                progressDialog.UpdateProgress(pct);
            });
            progressDialog.Show();

            try
            {
                await _speechService.SaveToFileAsync(content, dlg.FileName, format,
                    isSsml: useSsml, progress: progress, ct: cts.Token);
                progressDialog.UpdateProgress(100);
                progressDialog.MarkCompleted();
                AuditLogger.Record(_speechEngineType, format.ToString().ToLowerInvariant(),
                    success: true, outputPath: dlg.FileName);
                SetStatus($"音声保存完了: {Path.GetFileName(dlg.FileName)}");
                MessageBox.Show(
                    $"音声ファイルを保存しました。\n\n{dlg.FileName}",
                    "保存完了",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                try { File.Delete(dlg.FileName); } catch { /* 無視 */ }
                SetStatus("音声保存をキャンセルしました。");
                Logger.Info($"音声保存キャンセル: {dlg.FileName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{TtvErrorCode.SaveFailed}] 音声保存エラー: {ex.Message}");
                AuditLogger.Record(_speechEngineType, format.ToString().ToLowerInvariant(),
                    success: false, errorCode: TtvErrorCode.SaveFailed);
                MessageBox.Show(
                    $"[{TtvErrorCode.SaveFailed}] 音声ファイルの保存に失敗しました。\n\n{ex.Message}",
                    "保存エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                SetStatus($"[{TtvErrorCode.SaveFailed}] 音声保存に失敗しました。");
            }
            finally
            {
                progressDialog.Close();
                BtnSaveWav.IsEnabled = true;
            }
        }
    }
}
