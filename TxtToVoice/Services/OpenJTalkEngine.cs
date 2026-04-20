using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace TxtToVoice.Services
{
    /// <summary>
    /// OpenJTalk (jtalkdll) を使う日本語音声エンジン。
    ///
    /// 前提ファイル（exe 配置後の相対パス）:
    ///   jtalk.dll                              — exe と同じディレクトリ
    ///   Data\openjtalk\open_jtalk_dic_utf_8\   — MeCab UTF-8 辞書
    ///   Data\openjtalk\voice\*.htsvoice        — HTS Voice（Mei 等）
    ///
    /// ファイルが揃っていない場合は IsAvailable = false で初期化失敗扱いになる。
    /// setup.ps1 を poc\OpenJTalkPoC\ で実行し、生成物を Data\openjtalk\ に配置すること。
    ///
    /// HTS Voice "Mei": (C) 2009-2015 Nagoya Institute of Technology, CC BY 3.0
    /// THIRD_PARTY_LICENSES.txt を参照。
    /// </summary>
    public class OpenJTalkEngine : ISpeechEngine
    {
        private IntPtr _handle = IntPtr.Zero;
        private string _currentVoiceName = string.Empty;
        private string _currentVoicePath = string.Empty;
        private readonly string _dicPath;
        private readonly string _voiceDir;
        private double _speed    = 1.0;
        private double _volumeDb = 0.0;
        private WaveOutEvent? _waveOut;
        private CancellationTokenSource? _cts;
        private readonly object _synthLock = new();
        private bool _disposed;

        public event EventHandler? SpeakStarted;
        public event EventHandler? SpeakCompleted;
        public event EventHandler<SpeakProgressInfo>? SpeakProgress;
        public event EventHandler<string>? SpeakError;

        public bool IsAvailable { get; private set; }
        public string? InitializationError { get; private set; }
        public string CurrentVoiceName => _currentVoiceName;
        public string? CurrentVoiceId   => _currentVoicePath;

        public OpenJTalkEngine()
        {
            string baseDir = AppContext.BaseDirectory;
            _dicPath  = Path.Combine(baseDir, "Data", "openjtalk", "open_jtalk_dic_utf_8");
            _voiceDir = Path.Combine(baseDir, "Data", "openjtalk", "voice");

            if (!NativeJTalk.IsDllPresent())
            {
                Fail($"jtalk.dll が見つかりません: {Path.Combine(baseDir, "jtalk.dll")}\n  setup.ps1 を実行してください。");
                return;
            }
            if (!Directory.Exists(_dicPath))
            {
                Fail($"MeCab 辞書が見つかりません: {_dicPath}\n  setup.ps1 を実行してください。");
                return;
            }

            _currentVoicePath = FindDefaultVoice();
            if (string.IsNullOrEmpty(_currentVoicePath))
            {
                Fail($"音声モデル (.htsvoice) が見つかりません: {_voiceDir}\n  setup.ps1 を実行してください。");
                return;
            }
            _currentVoiceName = Path.GetFileNameWithoutExtension(_currentVoicePath);

            try
            {
                _handle = NativeJTalk.Initialize(_currentVoicePath, _dicPath);
                if (_handle == IntPtr.Zero)
                {
                    Fail("OpenJTalk の初期化に失敗しました（ハンドルが null）。辞書・音声モデルのパスを確認してください。");
                    return;
                }
                IsAvailable = true;
                Logger.Info($"OpenJTalkEngine 初期化成功: voice={_currentVoiceName}");
            }
            catch (DllNotFoundException ex)
            {
                Fail($"jtalk.dll のロードに失敗しました: {ex.Message}\n  x64 ビルドかどうか、依存 DLL が揃っているか確認してください。");
            }
            catch (Exception ex)
            {
                Fail(ex.InnerException?.Message ?? ex.Message);
            }
        }

        private void Fail(string message)
        {
            IsAvailable = false;
            InitializationError = message;
            Logger.Error($"OpenJTalkEngine 初期化失敗: {message}");
        }

        private string FindDefaultVoice()
        {
            if (!Directory.Exists(_voiceDir)) return string.Empty;
            string mei = Path.Combine(_voiceDir, "mei_normal.htsvoice");
            if (File.Exists(mei)) return mei;
            return Directory.GetFiles(_voiceDir, "*.htsvoice", SearchOption.AllDirectories)
                            .FirstOrDefault() ?? string.Empty;
        }

        // ----------------------------------------------------------------
        // 音声・パラメータ設定
        // ----------------------------------------------------------------

        public IReadOnlyList<string> GetAvailableVoices()
        {
            if (!Directory.Exists(_voiceDir)) return Array.Empty<string>();
            return Directory.GetFiles(_voiceDir, "*.htsvoice", SearchOption.AllDirectories)
                            .Select(p => Path.GetFileNameWithoutExtension(p))
                            .OrderBy(n => n)
                            .ToList();
        }

        public void SetVoice(string voiceName)
        {
            if (string.IsNullOrEmpty(voiceName)) return;
            string path = Path.Combine(_voiceDir, voiceName + ".htsvoice");
            if (!File.Exists(path)) { Logger.Warn($"OpenJTalkEngine: 音声モデルが見つかりません: {path}"); return; }

            IntPtr newHandle = NativeJTalk.Initialize(path, _dicPath);
            if (newHandle == IntPtr.Zero) { Logger.Warn($"OpenJTalkEngine: voice 変更の再初期化失敗: {voiceName}"); return; }

            lock (_synthLock)
            {
                if (_handle != IntPtr.Zero) NativeJTalk.Clear(_handle);
                _handle           = newHandle;
                _currentVoicePath = path;
                _currentVoiceName = voiceName;
            }
            ApplyParams();
            Logger.Info($"OpenJTalkEngine voice 変更: {voiceName}");
        }

        public string? FindVoiceNameById(string voiceId)
        {
            if (string.IsNullOrEmpty(voiceId)) return null;
            string name = Path.GetFileNameWithoutExtension(voiceId);
            var voices = GetAvailableVoices();
            return voices.Contains(name) ? name : null;
        }

        public void SetRate(int rate)
        {
            // SAPI -10..10 → jtalkdll speed 0.5..2.0（rate 0 = 1.0x）
            _speed = Math.Clamp(1.0 + rate * 0.1, 0.5, 2.0);
            lock (_synthLock)
                if (_handle != IntPtr.Zero) NativeJTalk.SetSpeed(_handle, _speed);
        }

        public void SetVolume(int volume)
        {
            // SAPI 0..100 → dB（100 = 0 dB / 0 = -20 dB）
            _volumeDb = (volume - 100) * 0.2;
            lock (_synthLock)
                if (_handle != IntPtr.Zero) NativeJTalk.SetVolume(_handle, _volumeDb);
        }

        private void ApplyParams()
        {
            lock (_synthLock)
            {
                if (_handle == IntPtr.Zero) return;
                NativeJTalk.SetSpeed(_handle, _speed);
                NativeJTalk.SetVolume(_handle, _volumeDb);
            }
        }

        // ----------------------------------------------------------------
        // 再生操作
        // ----------------------------------------------------------------

        public void SpeakAsync(string text)
        {
            if (!IsAvailable) { SpeakError?.Invoke(this, "音声エンジンが利用できません。\n" + InitializationError); return; }
            if (string.IsNullOrWhiteSpace(text)) { SpeakError?.Invoke(this, "読み上げるテキストがありません。"); return; }

            CancelCurrent();
            _cts = new CancellationTokenSource();
            var cts = _cts;
            Task.Run(() => SpeakCore(text, cts.Token));
        }

        public void SpeakSsmlAsync(string ssml)
        {
            // OpenJTalk は SSML 非対応 — タグを除去してプレーンテキストとして読む
            SpeakAsync(StripSsmlTags(ssml));
        }

        private void SpeakCore(string text, CancellationToken ct)
        {
            string tmpWav = Path.Combine(Path.GetTempPath(), $"txtvoice_jtalk_{Guid.NewGuid():N}.wav");
            try
            {
                bool ok;
                lock (_synthLock)
                    ok = NativeJTalk.SpeakToFile(_handle, text, tmpWav);

                if (ct.IsCancellationRequested || !ok)
                {
                    if (!ok) SpeakError?.Invoke(this, "OpenJTalk WAV 合成に失敗しました。");
                    return;
                }

                byte[] wav = File.ReadAllBytes(tmpWav);
                try { File.Delete(tmpWav); } catch { }

                SpeakStarted?.Invoke(this, EventArgs.Empty);
                PlayWavBytes(wav, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Error($"OpenJTalkEngine 読み上げエラー: {ex.Message}");
                SpeakError?.Invoke(this, $"読み上げ中にエラーが発生しました。\n{ex.Message}");
            }
            finally
            {
                try { if (File.Exists(tmpWav)) File.Delete(tmpWav); } catch { }
                SpeakCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        private void PlayWavBytes(byte[] wav, CancellationToken ct)
        {
            using var ms = new MemoryStream(wav);
            using var reader = new WaveFileReader(ms);
            using var waveOut = new WaveOutEvent();
            _waveOut = waveOut;

            using var done = new ManualResetEventSlim(false);
            waveOut.PlaybackStopped += (_, _) => done.Set();
            waveOut.Init(reader);
            waveOut.Play();
            done.Wait();    // Stop() が waveOut.Stop() を呼ぶと PlaybackStopped が発火して抜ける

            _waveOut = null;
        }

        public void Pause()  => _waveOut?.Pause();
        public void Resume() => _waveOut?.Play();

        public void Stop() => CancelCurrent();

        private void CancelCurrent()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            try { _waveOut?.Stop(); } catch { }
        }

        // ----------------------------------------------------------------
        // 音声ファイル保存
        // ----------------------------------------------------------------

        public void SaveToFile(string content, string outputPath, AudioFormat format,
            bool isSsml = false, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("音声エンジンが利用できません。\n" + InitializationError);

            string text = isSsml ? StripSsmlTags(content) : content;
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("読み上げるテキストがありません。");

            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            if (format == AudioFormat.Wav)
            {
                progress?.Report("音声を合成しています...");
                lock (_synthLock)
                {
                    bool ok = NativeJTalk.SpeakToFile(_handle, text, outputPath);
                    ct.ThrowIfCancellationRequested();
                    if (!ok) throw new InvalidOperationException("OpenJTalk WAV 合成に失敗しました。");
                }
                Logger.Info($"WAV 保存完了: {outputPath}");
                return;
            }

            // MP3 / MP4: WAV に合成してからエンコード
            string tmpWav = Path.Combine(Path.GetTempPath(), $"txtvoice_jtalk_{Guid.NewGuid():N}.wav");
            try
            {
                progress?.Report("音声を合成しています...");
                lock (_synthLock)
                {
                    bool ok = NativeJTalk.SpeakToFile(_handle, text, tmpWav);
                    ct.ThrowIfCancellationRequested();
                    if (!ok) throw new InvalidOperationException("OpenJTalk WAV 合成に失敗しました。");
                }
                string encLabel = format == AudioFormat.Mp3 ? "MP3" : "MP4";
                progress?.Report($"{encLabel} にエンコードしています...");
                using var reader = new WaveFileReader(tmpWav);
                if (format == AudioFormat.Mp3)
                    MediaFoundationEncoder.EncodeToMp3(reader, outputPath, desiredBitRate: 128_000);
                else
                    MediaFoundationEncoder.EncodeToAac(reader, outputPath, desiredBitRate: 128_000);
                ct.ThrowIfCancellationRequested();
                Logger.Info($"{encLabel} 保存完了: {outputPath}");
            }
            finally
            {
                try { if (File.Exists(tmpWav)) File.Delete(tmpWav); } catch { }
            }
        }

        // ----------------------------------------------------------------
        // ユーティリティ
        // ----------------------------------------------------------------

        private static string StripSsmlTags(string ssml)
            => Regex.Replace(ssml, "<[^>]+>", string.Empty);

        // ----------------------------------------------------------------
        // IDisposable
        // ----------------------------------------------------------------

        public void Dispose()
        {
            if (!_disposed)
            {
                CancelCurrent();
                lock (_synthLock)
                {
                    if (_handle != IntPtr.Zero)
                    {
                        try { NativeJTalk.Clear(_handle); } catch { }
                        _handle = IntPtr.Zero;
                    }
                }
                _disposed = true;
            }
        }
    }
}
