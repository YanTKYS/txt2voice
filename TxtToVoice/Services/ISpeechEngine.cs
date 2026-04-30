using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TxtToVoice.Services
{
    /// <summary>
    /// 音声合成エンジンの抽象インターフェース。
    /// <see cref="SystemSpeechEngine"/>（System.Speech 互換）と
    /// <see cref="WinRtSpeechEngine"/>（WinRT OneCore）が実装する。
    ///
    /// イベントの呼び出しスレッドは実装依存。呼び出し側（SpeechService）が
    /// UI スレッドへのディスパッチを担当する。
    /// </summary>
    public interface ISpeechEngine : IDisposable
    {
        /// <summary>音声エンジンが利用可能かどうか</summary>
        bool IsAvailable { get; }

        /// <summary>初期化失敗時のエラーメッセージ（成功時は null）</summary>
        string? InitializationError { get; }

        /// <summary>現在選択されている音声の名前</summary>
        string CurrentVoiceName { get; }

        /// <summary>現在選択されている音声の内部識別子（WinRT: VoiceInformation.Id、SAPI: VoiceInfo.Id）。取得できない場合は null。</summary>
        string? CurrentVoiceId { get; }

        /// <summary>内部識別子で音声を検索し、コンボボックス表示名を返す。見つからない場合は null。</summary>
        string? FindVoiceNameById(string voiceId);

        /// <summary>読み上げ開始時に発火</summary>
        event EventHandler? SpeakStarted;

        /// <summary>読み上げ完了・停止時に発火</summary>
        event EventHandler? SpeakCompleted;

        /// <summary>単語読み上げ進捗</summary>
        event EventHandler<SpeakProgressInfo>? SpeakProgress;

        /// <summary>エラー発生時に発火（引数: エラーメッセージ）</summary>
        event EventHandler<string>? SpeakError;

        /// <summary>インストール済みの有効な音声名の一覧を返す。</summary>
        IReadOnlyList<string> GetAvailableVoices();

        /// <summary>使用する音声を名前で指定する。</summary>
        void SetVoice(string voiceName);

        /// <summary>読み上げ速度を設定する（SAPI 準拠: -10〜10、0 が標準）。</summary>
        void SetRate(int rate);

        /// <summary>音量を設定する（0〜100）。</summary>
        void SetVolume(int volume);

        /// <summary>テキストを非同期で読み上げる。返却する Task は読み上げ完了（停止含む）で完了し、エラー時はフォルトする。</summary>
        Task SpeakAsync(string text, CancellationToken ct = default);

        /// <summary>SSML を非同期で読み上げる。返却する Task は読み上げ完了（停止含む）で完了し、エラー時はフォルトする。</summary>
        Task SpeakSsmlAsync(string ssml, CancellationToken ct = default);

        /// <summary>読み上げを一時停止する。</summary>
        void Pause();

        /// <summary>一時停止を再開する。</summary>
        void Resume();

        /// <summary>読み上げを停止する。</summary>
        void Stop();

        /// <summary>
        /// テキストまたは SSML を音声ファイルとして保存する（同期処理）。
        /// Task.Run 内から呼び出すこと。
        /// <paramref name="progress"/> に合成フェーズ・エンコードフェーズのメッセージを報告する。
        /// </summary>
        void SaveToFile(string content, string outputPath, AudioFormat format,
            bool isSsml = false, IProgress<string>? progress = null, CancellationToken ct = default);
    }
}
