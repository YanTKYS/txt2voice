using System;
using System.Collections.Generic;
using System.IO;
using System.Speech.Synthesis;
using System.Threading;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace TxtToVoice.Services
{
    /// <summary>
    /// System.Speech.Synthesis.SpeechSynthesizer を使う音声エンジン（Windows SAPI）。
    /// v0.3.0 以前の SpeechService から音声合成ロジックを切り出したもの。
    /// </summary>
    public class SystemSpeechEngine : ISpeechEngine
    {
        private SpeechSynthesizer? _synth;
        private bool _disposed;

        public event EventHandler? SpeakStarted;
        public event EventHandler? SpeakCompleted;
        public event EventHandler<SpeakProgressInfo>? SpeakProgress;
        public event EventHandler<string>? SpeakError;

        public SystemSpeechEngine()
        {
            try
            {
                _synth = new SpeechSynthesizer();
                _synth.SetOutputToDefaultAudioDevice();
                _synth.SpeakStarted   += (s, e) => SpeakStarted?.Invoke(this, EventArgs.Empty);
                _synth.SpeakCompleted += (s, e) => SpeakCompleted?.Invoke(this, EventArgs.Empty);
                _synth.SpeakProgress  += (s, e) =>
                    SpeakProgress?.Invoke(this, new SpeakProgressInfo(e.CharacterPosition, e.CharacterCount));
                IsAvailable = true;
                Logger.Info("SystemSpeechEngine 初期化成功");
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                InitializationError = ex.InnerException?.Message ?? ex.Message;
                Logger.Error($"SystemSpeechEngine 初期化失敗: [{ex.GetType().Name}] {InitializationError}");
            }
        }

        public bool IsAvailable { get; private set; }
        public string? InitializationError { get; private set; }
        public string CurrentVoiceName => _synth?.Voice?.Name ?? string.Empty;
        public string? CurrentVoiceId   => _synth?.Voice?.Id;

        // ----------------------------------------------------------------
        // 音声・パラメータ設定
        // ----------------------------------------------------------------

        public IReadOnlyList<string> GetAvailableVoices()
        {
            if (_synth == null) return Array.Empty<string>();
            var list = new List<string>();
            try
            {
                foreach (var v in _synth.GetInstalledVoices())
                    if (v.Enabled) list.Add(v.VoiceInfo.Name);
            }
            catch (Exception ex) { Logger.Warn($"音声一覧取得失敗: {ex.Message}"); }
            return list;
        }

        public void SetVoice(string voiceName)
        {
            if (_synth == null || string.IsNullOrEmpty(voiceName)) return;
            try   { _synth.SelectVoice(voiceName); }
            catch (Exception ex) { Logger.Warn($"音声選択失敗: {voiceName} / {ex.Message}"); }
        }

        public string? FindVoiceNameById(string voiceId)
        {
            if (_synth == null || string.IsNullOrEmpty(voiceId)) return null;
            try
            {
                foreach (var v in _synth.GetInstalledVoices())
                    if (v.Enabled && v.VoiceInfo.Id == voiceId)
                        return v.VoiceInfo.Name;
                return null;
            }
            catch { return null; }
        }

        public void SetRate(int rate)   { if (_synth != null) _synth.Rate   = Math.Clamp(rate,   -10, 10); }
        public void SetVolume(int volume){ if (_synth != null) _synth.Volume = Math.Clamp(volume,   0, 100); }

        // ----------------------------------------------------------------
        // 再生操作
        // ----------------------------------------------------------------

        public void SpeakAsync(string text)
        {
            if (_synth == null) { SpeakError?.Invoke(this, "音声エンジンが利用できません。\n" + InitializationError); return; }
            if (string.IsNullOrWhiteSpace(text)) { SpeakError?.Invoke(this, "読み上げるテキストがありません。"); return; }
            try
            {
                _synth.SpeakAsyncCancelAll();
                _synth.SetOutputToDefaultAudioDevice();
                _synth.SpeakAsync(text);
                Logger.Info($"読み上げ開始: {text.Length}文字");
            }
            catch (Exception ex)
            {
                Logger.Error($"読み上げエラー: {ex.Message}");
                SpeakError?.Invoke(this, $"読み上げ中にエラーが発生しました。\n{ex.Message}");
            }
        }

        public void SpeakSsmlAsync(string ssml)
        {
            if (_synth == null) { SpeakError?.Invoke(this, "音声エンジンが利用できません。\n" + InitializationError); return; }
            if (string.IsNullOrWhiteSpace(ssml)) { SpeakError?.Invoke(this, "読み上げるテキストがありません。"); return; }
            try
            {
                _synth.SpeakAsyncCancelAll();
                _synth.SetOutputToDefaultAudioDevice();
                _synth.SpeakSsmlAsync(ssml);
                Logger.Info("SSML読み上げ開始");
            }
            catch (Exception ex)
            {
                Logger.Error($"SSML読み上げエラー: {ex.Message}");
                SpeakError?.Invoke(this, $"読み上げ中にエラーが発生しました。\n{ex.Message}");
            }
        }

        public void Pause()
        {
            if (_synth?.State == SynthesizerState.Speaking) { _synth.Pause(); Logger.Info("読み上げ一時停止"); }
        }

        public void Resume()
        {
            if (_synth?.State == SynthesizerState.Paused) { _synth.Resume(); Logger.Info("読み上げ再開"); }
        }

        public void Stop()
        {
            _synth?.SpeakAsyncCancelAll();
            Logger.Info("読み上げ停止");
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

            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            if (format == AudioFormat.Wav) SaveWavDirect(content, outputPath, isSsml, progress, ct);
            else                           SaveEncoded(content, outputPath, format, isSsml, progress, ct);
        }

        private void SaveWavDirect(string content, string outputPath, bool isSsml,
            IProgress<string>? progress, CancellationToken ct)
        {
            using var wavSynth = BuildSynthClone();
            using var done = new ManualResetEventSlim(false);
            wavSynth.SpeakCompleted += (_, _) => { try { done.Set(); } catch (ObjectDisposedException) { } };
            using var reg = ct.Register(() => wavSynth.SpeakAsyncCancelAll());
            progress?.Report("音声を合成しています...");
            try
            {
                wavSynth.SetOutputToWaveFile(outputPath);
                if (isSsml) wavSynth.SpeakSsmlAsync(content);
                else        wavSynth.SpeakAsync(content);
                done.Wait(ct);
                ct.ThrowIfCancellationRequested();
            }
            finally { try { wavSynth.SetOutputToDefaultAudioDevice(); } catch { /* 無視 */ } }
            Logger.Info($"WAV 保存完了: {outputPath}");
        }

        private void SaveEncoded(string content, string outputPath, AudioFormat format, bool isSsml,
            IProgress<string>? progress, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            using var wavSynth = BuildSynthClone();
            using var done = new ManualResetEventSlim(false);
            wavSynth.SpeakCompleted += (_, _) => { try { done.Set(); } catch (ObjectDisposedException) { } };
            using var reg = ct.Register(() => wavSynth.SpeakAsyncCancelAll());
            progress?.Report("音声を合成しています...");
            try
            {
                wavSynth.SetOutputToWaveStream(ms);
                if (isSsml) wavSynth.SpeakSsmlAsync(content);
                else        wavSynth.SpeakAsync(content);
                done.Wait(ct);
                ct.ThrowIfCancellationRequested();
            }
            finally { try { wavSynth.SetOutputToDefaultAudioDevice(); } catch { /* 無視 */ } }
            ms.Seek(0, SeekOrigin.Begin);
            string encLabel = format == AudioFormat.Mp3 ? "MP3" : "MP4";
            progress?.Report($"{encLabel} にエンコードしています...");
            using var reader = new WaveFileReader(ms);
            if (format == AudioFormat.Mp3)
            {
                MediaFoundationEncoder.EncodeToMp3(reader, outputPath, desiredBitRate: 128_000);
                ct.ThrowIfCancellationRequested();
                Logger.Info($"MP3 保存完了: {outputPath}");
            }
            else
            {
                MediaFoundationEncoder.EncodeToAac(reader, outputPath, desiredBitRate: 128_000);
                ct.ThrowIfCancellationRequested();
                Logger.Info($"MP4(AAC) 保存完了: {outputPath}");
            }
        }

        private SpeechSynthesizer BuildSynthClone()
        {
            var s = new SpeechSynthesizer();
            s.Rate   = _synth!.Rate;
            s.Volume = _synth.Volume;
            string voice = CurrentVoiceName;
            if (!string.IsNullOrEmpty(voice)) { try { s.SelectVoice(voice); } catch { /* 無視 */ } }
            return s;
        }

        // ----------------------------------------------------------------
        // IDisposable
        // ----------------------------------------------------------------

        public void Dispose()
        {
            if (!_disposed)
            {
                try { _synth?.SpeakAsyncCancelAll(); } catch { /* 無視 */ }
                try { _synth?.Dispose(); }             catch { /* 無視 */ }
                _disposed = true;
            }
        }
    }
}
