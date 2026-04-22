using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
    /// ISpeechEngine のアダプタ。エンジンからのイベントを UI スレッドへ転送し、
    /// SaveToFileAsync の非同期ラップを提供する。
    ///
    /// 音声エンジンの実装は <see cref="SystemSpeechEngine"/>（SAPI）または
    /// <see cref="WinRtSpeechEngine"/>（WinRT OneCore）を使用する。
    /// 設定ダイアログでエンジン種別を切り替えでき、変更は次回起動時に適用される。
    /// </summary>
    public class SpeechService : IDisposable
    {
        private readonly ISpeechEngine _engine;
        private bool _disposed;
        private readonly SynchronizationContext? _uiContext;

        // ----------------------------------------------------------------
        // イベント（UI スレッドで呼ばれる）
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

        public SpeechService(ISpeechEngine engine)
        {
            _uiContext = SynchronizationContext.Current;
            _engine = engine;

            _engine.SpeakStarted   += (s, e) => RaiseOnUiThread(() => SpeakStarted?.Invoke(this, EventArgs.Empty));
            _engine.SpeakCompleted += (s, e) => RaiseOnUiThread(() => SpeakCompleted?.Invoke(this, EventArgs.Empty));
            _engine.SpeakProgress  += (s, e) => RaiseOnUiThread(() => SpeakProgress?.Invoke(this, e));
            _engine.SpeakError     += (s, e) => RaiseOnUiThread(() => SpeakError?.Invoke(this, e));
        }

        /// <summary>後方互換のための便宜コンストラクタ。SystemSpeechEngine（SAPI）を使用する。</summary>
        public SpeechService() : this(new SystemSpeechEngine()) { }

        // ----------------------------------------------------------------
        // プロパティ（エンジンに委譲）
        // ----------------------------------------------------------------

        public bool IsAvailable => _engine.IsAvailable;
        public string? InitializationError => _engine.InitializationError;
        public string  CurrentVoiceName  => _engine.CurrentVoiceName;
        public string? CurrentVoiceId    => _engine.CurrentVoiceId;
        public string? FindVoiceNameById(string voiceId) => _engine.FindVoiceNameById(voiceId);

        /// <summary>エンジンが OpenJTalk の場合に診断結果を返す。他エンジンでは null。</summary>
        public OpenJTalkDiagnostics? GetOpenJTalkDiagnostics()
            => (_engine as OpenJTalkEngine)?.Diagnostics;

        // ----------------------------------------------------------------
        // 音声・パラメータ設定
        // ----------------------------------------------------------------

        public IEnumerable<string> GetAvailableVoices() => _engine.GetAvailableVoices();
        public void SetVoice(string voiceName)  => _engine.SetVoice(voiceName);
        public void SetRate(int rate)            => _engine.SetRate(rate);
        public void SetVolume(int volume)        => _engine.SetVolume(volume);

        // ----------------------------------------------------------------
        // 再生操作
        // ----------------------------------------------------------------

        public void SpeakAsync(string text)    => _engine.SpeakAsync(text);
        public void SpeakSsmlAsync(string ssml) => _engine.SpeakSsmlAsync(ssml);
        public void Pause()                     => _engine.Pause();
        public void Resume()                    => _engine.Resume();
        public void Stop()                      => _engine.Stop();

        // ----------------------------------------------------------------
        // 音声ファイル保存
        // ----------------------------------------------------------------

        /// <summary>テキストまたは SSML を音声ファイルとして非同期で保存する。</summary>
        public Task SaveToFileAsync(string content, string outputPath, AudioFormat format,
            bool isSsml = false, IProgress<string>? progress = null, CancellationToken ct = default)
            => Task.Run(
                () => { ct.ThrowIfCancellationRequested(); _engine.SaveToFile(content, outputPath, format, isSsml, progress, ct); },
                ct);

        // ----------------------------------------------------------------
        // IDisposable
        // ----------------------------------------------------------------

        public void Dispose()
        {
            if (!_disposed)
            {
                try { _engine.Stop(); }    catch { /* 無視 */ }
                try { _engine.Dispose(); } catch { /* 無視 */ }
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
