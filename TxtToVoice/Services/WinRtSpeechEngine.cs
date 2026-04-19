using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace TxtToVoice.Services
{
    /// <summary>
    /// Windows WinRT (Windows.Media.SpeechSynthesis) を使う音声エンジン。
    /// 端末に導入済みの OneCore 系音声（Ayumi, Haruka 等の高品質版）を利用できる。
    ///
    /// 必要要件: Windows 10 Build 19041 (2004) 以降。
    ///
    /// 速度変換: SAPI -10..10 → WinRT SpeakingRate 0.5..3.0
    /// 音量変換: SAPI 0..100  → WinRT AudioVolume  0.0..1.0
    ///
    /// 注意: 読み上げ開始イベントは音声合成完了後にメディア再生が始まるタイミングで発火するため、
    /// SystemSpeechEngine に比べてわずかに遅延が生じる場合がある。
    /// </summary>
    public class WinRtSpeechEngine : ISpeechEngine
    {
        private Windows.Media.SpeechSynthesis.SpeechSynthesizer? _synth;
        private MediaPlayer? _player;
        private MediaPlaybackItem? _mediaItem;
        private CancellationTokenSource? _speakCts;
        private volatile bool _isSpeaking;
        private bool _disposed;

        public event EventHandler? SpeakStarted;
        public event EventHandler? SpeakCompleted;
        public event EventHandler<SpeakProgressInfo>? SpeakProgress;
        public event EventHandler<string>? SpeakError;

        public WinRtSpeechEngine()
        {
            try
            {
                _synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
                // 単語境界メタデータを有効にして SpeakProgress イベントを発火できるようにする
                _synth.Options.IncludeWordBoundaryMetadata = true;
                IsAvailable = true;
                Logger.Info("WinRtSpeechEngine 初期化成功");
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                InitializationError = ex.InnerException?.Message ?? ex.Message;
                Logger.Error($"WinRtSpeechEngine 初期化失敗: [{ex.GetType().Name}] {InitializationError}");
            }
        }

        public bool IsAvailable { get; private set; }
        public string? InitializationError { get; private set; }
        public string  CurrentVoiceName => _synth?.Voice.DisplayName ?? string.Empty;
        public string? CurrentVoiceId
        {
            get { try { return _synth?.Voice.Id; } catch { return null; } }
        }

        // ----------------------------------------------------------------
        // 音声・パラメータ設定
        // ----------------------------------------------------------------

        public IReadOnlyList<string> GetAvailableVoices()
        {
            try
            {
                return Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices
                    .Select(v => v.DisplayName)
                    .ToList();
            }
            catch (Exception ex) { Logger.Warn($"WinRT 音声一覧取得失敗: {ex.Message}"); return Array.Empty<string>(); }
        }

        public void SetVoice(string voiceName)
        {
            if (_synth == null || string.IsNullOrEmpty(voiceName)) return;
            try
            {
                var voice = Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices
                    .FirstOrDefault(v => v.DisplayName == voiceName);
                if (voice != null) _synth.Voice = voice;
            }
            catch (Exception ex) { Logger.Warn($"WinRT 音声選択失敗: {voiceName} / {ex.Message}"); }
        }

        public string? FindVoiceNameById(string voiceId)
        {
            if (string.IsNullOrEmpty(voiceId)) return null;
            try
            {
                return Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices
                    .FirstOrDefault(v => v.Id == voiceId)?.DisplayName;
            }
            catch { return null; }
        }

        public void SetRate(int rate)
        {
            if (_synth != null)
                _synth.Options.SpeakingRate = SapiRateToWinRt(Math.Clamp(rate, -10, 10));
        }

        public void SetVolume(int volume)
        {
            if (_synth != null)
                _synth.Options.AudioVolume = Math.Clamp(volume, 0, 100) / 100.0;
        }

        /// <summary>SAPI レート（-10〜10）を WinRT SpeakingRate（0.5〜3.0）に変換する。</summary>
        private static double SapiRateToWinRt(int rate)
            => rate >= 0 ? 1.0 + rate * 0.2          // 0 → 1.0, 10 → 3.0
                         : 0.5 + (rate + 10) * 0.05;  // -10 → 0.5, 0 → 1.0

        // ----------------------------------------------------------------
        // 再生操作
        // ----------------------------------------------------------------

        public void SpeakAsync(string text)
        {
            if (!IsAvailable) { SpeakError?.Invoke(this, "音声エンジンが利用できません。\n" + InitializationError); return; }
            if (string.IsNullOrWhiteSpace(text)) { SpeakError?.Invoke(this, "読み上げるテキストがありません。"); return; }
            _speakCts?.Cancel();
            _speakCts = new CancellationTokenSource();
            _ = SpeakInternalAsync(text, false, _speakCts.Token);
        }

        public void SpeakSsmlAsync(string ssml)
        {
            if (!IsAvailable) { SpeakError?.Invoke(this, "音声エンジンが利用できません。\n" + InitializationError); return; }
            if (string.IsNullOrWhiteSpace(ssml)) { SpeakError?.Invoke(this, "読み上げるテキストがありません。"); return; }
            _speakCts?.Cancel();
            _speakCts = new CancellationTokenSource();
            _ = SpeakInternalAsync(ssml, true, _speakCts.Token);
        }

        private async Task SpeakInternalAsync(string content, bool isSsml, CancellationToken ct)
        {
            try
            {
                StopInternal(fireEvent: true);
                ct.ThrowIfCancellationRequested();

                // 音声合成（WinRT 非同期）
                SpeechSynthesisStream stream;
                try
                {
                    stream = isSsml
                        ? await _synth!.SynthesizeSsmlToStreamAsync(content)
                        : await _synth!.SynthesizeTextToStreamAsync(content);
                }
                catch when (ct.IsCancellationRequested) { return; }

                if (ct.IsCancellationRequested) return;

                // MediaPlayer をセットアップして再生
                _player = new MediaPlayer { AutoPlay = false };

                _player.MediaEnded += (s, e) =>
                {
                    _isSpeaking = false;
                    SpeakCompleted?.Invoke(this, EventArgs.Empty);
                };
                _player.MediaFailed += (s, e) =>
                {
                    _isSpeaking = false;
                    SpeakError?.Invoke(this, e.ErrorMessage);
                };
                _player.PlaybackSession.PlaybackStateChanged += (s, e) =>
                {
                    if (s.PlaybackState == MediaPlaybackState.Playing && !_isSpeaking)
                    {
                        _isSpeaking = true;
                        SpeakStarted?.Invoke(this, EventArgs.Empty);
                    }
                };

                // MediaPlaybackItem 経由で TimedMetadataTracks から SpeakProgress を発火する
                var mediaSource = MediaSource.CreateFromStream(stream, stream.ContentType);
                _mediaItem = new MediaPlaybackItem(mediaSource);
                SetupTimedMetadataTracks(_mediaItem);

                _player.Source = _mediaItem;
                _player.Play();

                Logger.Info($"WinRT 読み上げ開始: {content.Length}文字");
            }
            catch (OperationCanceledException)
            {
                // キャンセルは正常終了
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Logger.Error($"WinRT 読み上げエラー: {ex.Message}");
                SpeakError?.Invoke(this, $"読み上げ中にエラーが発生しました。\n{ex.Message}");
            }
        }

        /// <summary>
        /// MediaPlaybackItem の TimedMetadataTracks に SpeechCue リスナーを登録する。
        /// SpeechSynthesisStream は合成済みのインメモリストリームのため、
        /// 作成直後にすべてのトラックが利用可能になる。
        /// </summary>
        private void SetupTimedMetadataTracks(MediaPlaybackItem item)
        {
            int count = (int)item.TimedMetadataTracks.Count;
            for (int i = 0; i < count; i++)
            {
                item.TimedMetadataTracks.SetPresentationMode(
                    (uint)i, TimedMetadataTrackPresentationMode.ApplicationPresented);
                item.TimedMetadataTracks[i].CueEntered += OnSpeechCueEntered;
            }
        }

        private void OnSpeechCueEntered(TimedMetadataTrack sender, MediaCueEventArgs args)
        {
            if (args.Cue is SpeechCue cue)
            {
                int start = cue.StartPositionInInput ?? 0;
                int count = (cue.EndPositionInInput ?? start) - start;
                SpeakProgress?.Invoke(this, new SpeakProgressInfo(start, count));
            }
        }

        public void Pause()
        {
            _player?.Pause();
            Logger.Info("WinRT 読み上げ一時停止");
        }

        public void Resume()
        {
            _player?.Play();
            Logger.Info("WinRT 読み上げ再開");
        }

        public void Stop()
        {
            _speakCts?.Cancel();
            _speakCts = new CancellationTokenSource(); // 次回 speak に備えて新規作成
            StopInternal(fireEvent: true);
            Logger.Info("WinRT 読み上げ停止");
        }

        private void StopInternal(bool fireEvent)
        {
            bool wasSpeaking = _isSpeaking;
            _isSpeaking = false;
            DisposePlayer();
            if (fireEvent && wasSpeaking)
                SpeakCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void DisposePlayer()
        {
            var p = _player;
            _player = null;
            _mediaItem = null; // player が mediaItem を保持しているため、参照を手放せば GC に委ねられる
            if (p == null) return;
            try { p.Pause(); }       catch { /* 無視 */ }
            try { p.Source = null; } catch { /* 無視 */ }
            try { p.Dispose(); }     catch { /* 無視 */ }
        }

        // ----------------------------------------------------------------
        // 音声ファイル保存
        // ----------------------------------------------------------------

        public void SaveToFile(string content, string outputPath, AudioFormat format,
            bool isSsml = false, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            if (_synth == null)
                throw new InvalidOperationException("音声エンジンが利用できません。\n" + InitializationError);
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("読み上げるテキストがありません。");

            ct.ThrowIfCancellationRequested();

            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // WinRT 合成（Task.Run 内のため .GetAwaiter().GetResult() が安全）
            progress?.Report("音声を合成しています...");
            SpeechSynthesisStream stream;
            try
            {
                stream = isSsml
                    ? _synth.SynthesizeSsmlToStreamAsync(content).GetAwaiter().GetResult()
                    : _synth.SynthesizeTextToStreamAsync(content).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"WinRT 音声合成失敗: {ex.Message}", ex);
            }

            // GetTempFileName() はファイルを実際に生成するため GetRandomFileName() を使用する（#42）
            string tempWavPath = Path.Combine(
                Path.GetTempPath(), Path.ChangeExtension(Path.GetRandomFileName(), ".wav"));
            try
            {
                using (stream)
                {
                    ct.ThrowIfCancellationRequested();
                    stream.Seek(0);
                    using var netStream = stream.AsStreamForRead();
                    using var tempFs = new FileStream(tempWavPath, FileMode.Create, FileAccess.Write);
                    netStream.CopyTo(tempFs);
                }

                ct.ThrowIfCancellationRequested();

                if (format == AudioFormat.Wav)
                {
                    // File.Move は異なるボリューム間で IOException を投げるため Copy+Delete にする（#43）
                    // tempWavPath の削除は finally ブロックに委ねる
                    File.Copy(tempWavPath, outputPath, overwrite: true);
                    Logger.Info($"WinRT WAV 保存完了: {outputPath}");
                }
                else
                {
                    string encLabel = format == AudioFormat.Mp3 ? "MP3" : "MP4";
                    progress?.Report($"{encLabel} にエンコードしています...");
                    using var reader = new WaveFileReader(tempWavPath);
                    if (format == AudioFormat.Mp3)
                    {
                        MediaFoundationEncoder.EncodeToMp3(reader, outputPath, desiredBitRate: 128_000);
                        ct.ThrowIfCancellationRequested();
                        Logger.Info($"WinRT MP3 保存完了: {outputPath}");
                    }
                    else
                    {
                        MediaFoundationEncoder.EncodeToAac(reader, outputPath, desiredBitRate: 128_000);
                        ct.ThrowIfCancellationRequested();
                        Logger.Info($"WinRT MP4(AAC) 保存完了: {outputPath}");
                    }
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempWavPath))
                    try { File.Delete(tempWavPath); } catch { /* 無視 */ }
            }
        }

        // ----------------------------------------------------------------
        // IDisposable
        // ----------------------------------------------------------------

        public void Dispose()
        {
            if (!_disposed)
            {
                _speakCts?.Cancel();
                _speakCts?.Dispose();
                DisposePlayer();
                try { _synth?.Dispose(); } catch { /* 無視 */ }
                _synth = null;
                _disposed = true;
            }
        }
    }
}
