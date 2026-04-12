using System;
using System.Collections.Generic;
using System.IO;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace TxtToVoice.Services
{
    /// <summary>音声保存フォーマット</summary>
    public enum AudioFormat
    {
        Wav,
        Mp3,
        Mp4   // AAC コーデック + MP4 コンテナ
    }

    /// <summary>読み上げ進捗イベントのデータ（単語位置）</summary>
    public sealed record SpeakProgressInfo(int CharacterPosition, int CharacterCount);

    /// <summary>
    /// System.Speech.Synthesis.SpeechSynthesizer のラッパー。
    /// 読み上げの開始・一時停止・再開・停止・音声ファイル保存（WAV/MP3/MP4）を提供する。
    ///
    /// 音声エンジンが利用できない環境でも IsAvailable=false として
    /// アプリが起動できるよう fault-tolerant に設計している。
    /// </summary>
    public class SpeechService : IDisposable
    {
        private SpeechSynthesizer? _synth;
        private bool _disposed;

        // UI スレッドへのディスパッチ用
        private readonly SynchronizationContext? _uiContext;

        // ----------------------------------------------------------------
        // イベント
        // ----------------------------------------------------------------

        /// <summary>読み上げ開始時に発火（UI スレッドで呼ばれる）</summary>
        public event EventHandler? SpeakStarted;

        /// <summary>読み上げ完了・中断時に発火（UI スレッドで呼ばれる）</summary>
        public event EventHandler? SpeakCompleted;

        /// <summary>単語読み上げ進捗イベント（UI スレッドで呼ばれる）</summary>
        public event EventHandler<SpeakProgressInfo>? SpeakProgress;

        /// <summary>エラー発生時に発火（UI スレッドで呼ばれる）。引数はエラーメッセージ</summary>
        public event EventHandler<string>? SpeakError;

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        /// <summary>
        /// 音声エンジンが利用できない場合は IsAvailable=false になる。
        /// 例外はスローしない。
        /// </summary>
        public SpeechService()
        {
            _uiContext = SynchronizationContext.Current;
            try
            {
                _synth = CreateSynthesizer();
                IsAvailable = true;
                Logger.Info("SpeechSynthesizer 初期化成功");
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                InitializationError = ex.InnerException?.Message ?? ex.Message;
                Logger.Error($"SpeechSynthesizer 初期化失敗: [{ex.GetType().Name}] {InitializationError}");
            }
        }

        private SpeechSynthesizer CreateSynthesizer()
        {
            var synth = new SpeechSynthesizer();
            synth.SetOutputToDefaultAudioDevice();
            synth.SpeakStarted   += (s, e) => RaiseOnUiThread(() => SpeakStarted?.Invoke(this, EventArgs.Empty));
            synth.SpeakCompleted += (s, e) => RaiseOnUiThread(() => SpeakCompleted?.Invoke(this, EventArgs.Empty));
            synth.SpeakProgress  += (s, e) => RaiseOnUiThread(
                () => SpeakProgress?.Invoke(this, new SpeakProgressInfo(e.CharacterPosition, e.CharacterCount)));
            return synth;
        }

        // ----------------------------------------------------------------
        // プロパティ
        // ----------------------------------------------------------------

        /// <summary>音声エンジンが利用可能かどうか</summary>
        public bool IsAvailable { get; private set; }

        /// <summary>初期化失敗時のエラーメッセージ（成功時は null）</summary>
        public string? InitializationError { get; private set; }

        public SynthesizerState State
        {
            get
            {
                if (_disposed || _synth == null) return SynthesizerState.Ready;
                return _synth.State;
            }
        }

        // ----------------------------------------------------------------
        // 音声・パラメータ設定
        // ----------------------------------------------------------------

        /// <summary>インストール済みの有効な音声名の一覧を返す。</summary>
        public IEnumerable<string> GetAvailableVoices()
        {
            if (_synth == null) return Array.Empty<string>();
            var list = new List<string>();
            try
            {
                foreach (var v in _synth.GetInstalledVoices())
                    if (v.Enabled) list.Add(v.VoiceInfo.Name);
            }
            catch (Exception ex)
            {
                Logger.Warn($"音声一覧取得失敗: {ex.Message}");
            }
            return list;
        }

        /// <summary>使用する音声を名前で指定する。</summary>
        public void SetVoice(string voiceName)
        {
            if (_synth == null || string.IsNullOrEmpty(voiceName)) return;
            try   { _synth.SelectVoice(voiceName); }
            catch (Exception ex) { Logger.Warn($"音声選択失敗: {voiceName} / {ex.Message}"); }
        }

        /// <summary>現在の音声名を返す。</summary>
        public string CurrentVoiceName => _synth?.Voice?.Name ?? string.Empty;

        /// <summary>読み上げ速度を設定する。-10〜10</summary>
        public void SetRate(int rate)
        {
            if (_synth != null) _synth.Rate = Math.Clamp(rate, -10, 10);
        }

        /// <summary>音量を設定する。0〜100</summary>
        public void SetVolume(int volume)
        {
            if (_synth != null) _synth.Volume = Math.Clamp(volume, 0, 100);
        }

        // ----------------------------------------------------------------
        // 再生操作
        // ----------------------------------------------------------------

        /// <summary>テキストを非同期で読み上げる。</summary>
        public void SpeakAsync(string text)
        {
            if (_synth == null)
            {
                RaiseOnUiThread(() => SpeakError?.Invoke(this, "音声エンジンが利用できません。\n" + InitializationError));
                return;
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                RaiseOnUiThread(() => SpeakError?.Invoke(this, "読み上げるテキストがありません。"));
                return;
            }

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
                RaiseOnUiThread(() => SpeakError?.Invoke(this, $"読み上げ中にエラーが発生しました。\n{ex.Message}"));
            }
        }

        /// <summary>SSML テキストを非同期で読み上げる。</summary>
        public void SpeakSsmlAsync(string ssml)
        {
            if (_synth == null)
            {
                RaiseOnUiThread(() => SpeakError?.Invoke(this, "音声エンジンが利用できません。\n" + InitializationError));
                return;
            }
            if (string.IsNullOrWhiteSpace(ssml))
            {
                RaiseOnUiThread(() => SpeakError?.Invoke(this, "読み上げるテキストがありません。"));
                return;
            }
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
                RaiseOnUiThread(() => SpeakError?.Invoke(this, $"読み上げ中にエラーが発生しました。\n{ex.Message}"));
            }
        }

        /// <summary>読み上げを一時停止する。</summary>
        public void Pause()
        {
            if (_synth?.State == SynthesizerState.Speaking)
            {
                _synth.Pause();
                Logger.Info("読み上げ一時停止");
            }
        }

        /// <summary>一時停止中の読み上げを再開する。</summary>
        public void Resume()
        {
            if (_synth?.State == SynthesizerState.Paused)
            {
                _synth.Resume();
                Logger.Info("読み上げ再開");
            }
        }

        /// <summary>読み上げを停止する。</summary>
        public void Stop()
        {
            _synth?.SpeakAsyncCancelAll();
            Logger.Info("読み上げ停止");
        }

        // ----------------------------------------------------------------
        // 音声ファイル保存
        // ----------------------------------------------------------------

        /// <summary>
        /// テキストを音声ファイルとして非同期で保存する。
        /// UI スレッドをブロックしない。
        /// </summary>
        /// <summary>テキストまたは SSML を音声ファイルとして非同期で保存する。</summary>
        public Task SaveToFileAsync(string content, string outputPath, AudioFormat format,
            bool isSsml = false, CancellationToken ct = default)
            => Task.Run(
                () => { ct.ThrowIfCancellationRequested(); SaveToFile(content, outputPath, format, isSsml, ct); },
                ct);

        /// <summary>
        /// テキストまたは SSML を音声ファイルとして保存する（同期処理）。
        /// 現在の音声・速度・音量設定を引き継ぐ。
        /// </summary>
        /// <param name="content">読み上げテキストまたは SSML 文字列</param>
        /// <param name="outputPath">出力ファイルパス</param>
        /// <param name="format">出力フォーマット（WAV / MP3 / MP4）</param>
        /// <param name="isSsml">true のとき content を SSML として扱う</param>
        /// <param name="ct">キャンセルトークン</param>
        public void SaveToFile(string content, string outputPath, AudioFormat format,
            bool isSsml = false, CancellationToken ct = default)
        {
            if (_synth == null)
                throw new InvalidOperationException("音声エンジンが利用できません。\n" + InitializationError);
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("読み上げるテキストがありません。");

            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (format == AudioFormat.Wav)
                SaveWavDirect(content, outputPath, isSsml, ct);
            else
                SaveEncoded(content, outputPath, format, isSsml, ct);
        }

        /// <summary>WAV を直接ファイルに書き出す。キャンセル時は OperationCanceledException をスローする。</summary>
        private void SaveWavDirect(string content, string outputPath, bool isSsml, CancellationToken ct)
        {
            using var wavSynth = BuildSynthClone();
            using var done = new ManualResetEventSlim(false);
            wavSynth.SpeakCompleted += (_, _) => { try { done.Set(); } catch (ObjectDisposedException) { } };

            // キャンセル時にベストエフォートで音声合成を中断する
            using var reg = ct.Register(() => wavSynth.SpeakAsyncCancelAll());

            try
            {
                wavSynth.SetOutputToWaveFile(outputPath);
                if (isSsml) wavSynth.SpeakSsmlAsync(content);
                else        wavSynth.SpeakAsync(content);
                done.Wait(ct);                  // 完了またはキャンセルを待機（ct 指定で無限待機を防ぐ）
                ct.ThrowIfCancellationRequested();
            }
            finally
            {
                try { wavSynth.SetOutputToDefaultAudioDevice(); } catch { /* 無視 */ }
            }
            Logger.Info($"WAV 保存完了: {outputPath}");
        }

        /// <summary>WAV をメモリ経由で MP3 / MP4(AAC) にエンコードして保存する。キャンセル時は OperationCanceledException をスローする。</summary>
        private void SaveEncoded(string content, string outputPath, AudioFormat format, bool isSsml, CancellationToken ct)
        {
            // 1. WAV をメモリストリームに生成
            using var ms = new MemoryStream();
            using var wavSynth = BuildSynthClone();
            using var done = new ManualResetEventSlim(false);
            wavSynth.SpeakCompleted += (_, _) => { try { done.Set(); } catch (ObjectDisposedException) { } };

            // キャンセル時にベストエフォートで音声合成を中断する
            using var reg = ct.Register(() => wavSynth.SpeakAsyncCancelAll());

            try
            {
                wavSynth.SetOutputToWaveStream(ms);
                if (isSsml) wavSynth.SpeakSsmlAsync(content);
                else        wavSynth.SpeakAsync(content);
                done.Wait(ct);                  // 完了またはキャンセルを待機（ct 指定で無限待機を防ぐ）
                ct.ThrowIfCancellationRequested();
            }
            finally
            {
                try { wavSynth.SetOutputToDefaultAudioDevice(); } catch { /* 無視 */ }
            }

            ms.Seek(0, SeekOrigin.Begin);

            // 2. NAudio で目的フォーマットにエンコード
            using var reader = new WaveFileReader(ms);

            if (format == AudioFormat.Mp3)
            {
                MediaFoundationEncoder.EncodeToMp3(reader, outputPath, desiredBitRate: 128_000);
                ct.ThrowIfCancellationRequested(); // エンコード完了後にキャンセル確認
                Logger.Info($"MP3 保存完了: {outputPath}");
            }
            else
            {
                MediaFoundationEncoder.EncodeToAac(reader, outputPath, desiredBitRate: 128_000);
                ct.ThrowIfCancellationRequested(); // エンコード完了後にキャンセル確認
                Logger.Info($"MP4(AAC) 保存完了: {outputPath}");
            }
        }

        /// <summary>現在の synth の設定（音声・速度・音量）を複製した SpeechSynthesizer を返す。</summary>
        private SpeechSynthesizer BuildSynthClone()
        {
            var s = new SpeechSynthesizer();
            s.Rate   = _synth!.Rate;
            s.Volume = _synth.Volume;
            string voice = CurrentVoiceName;
            if (!string.IsNullOrEmpty(voice))
            {
                try { s.SelectVoice(voice); } catch { /* 無視 */ }
            }
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

        // ----------------------------------------------------------------
        // ヘルパー
        // ----------------------------------------------------------------

        private void RaiseOnUiThread(Action action)
        {
            if (_uiContext != null)
                _uiContext.Post(_ => action(), null);
            else
                action();
        }
    }
}
