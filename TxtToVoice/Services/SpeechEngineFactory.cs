namespace TxtToVoice.Services
{
    /// <summary>
    /// 音声エンジン種別の定数・生成・ラベル書式を一箇所に集約するファクトリ。
    ///
    /// 各呼び出し元に文字列リテラル（"SystemSpeech" / "WinRT"）や
    /// エンジンラベル（"SAPI (System.Speech)" 等）が散在しないようにするためのもの。
    /// OSS TTS エンジン（backlog #33）追加時はここに定数と Create 分岐を足すだけで済む。
    ///
    /// エンジン種別の文字列定数・検証ロジックは <see cref="SpeechEngineTypes"/>（Core 層）に置き、
    /// 後方互換のために本クラスからも公開する。
    /// </summary>
    public static class SpeechEngineFactory
    {
        // ----------------------------------------------------------------
        // エンジン種別定数（SpeechEngineTypes に委譲）
        // ----------------------------------------------------------------

        public const string SystemSpeech = SpeechEngineTypes.SystemSpeech;
        public const string WinRt        = SpeechEngineTypes.WinRt;

        /// <summary>未知の値を受け取ったときの既定種別。</summary>
        public const string Default = SpeechEngineTypes.Default;

        /// <summary>指定した文字列が既知の有効なエンジン種別かどうかを返す。</summary>
        public static bool IsKnown(string? engineType) => SpeechEngineTypes.IsKnown(engineType);

        // ----------------------------------------------------------------
        // 生成
        // ----------------------------------------------------------------

        /// <summary>エンジン種別文字列から対応する <see cref="ISpeechEngine"/> を生成する。</summary>
        public static ISpeechEngine Create(string? engineType) => engineType switch
        {
            WinRt        => new WinRtSpeechEngine(),
            SystemSpeech => new SystemSpeechEngine(),
            _            => new SystemSpeechEngine(),
        };

        // ----------------------------------------------------------------
        // ラベル書式（UI 表示・ログ用）
        // ----------------------------------------------------------------

        /// <summary>
        /// ユーザー向けエンジンラベルを返す。
        /// <paramref name="prefixWindows"/> を true にすると "Windows " 接頭辞を付ける（About ダイアログ等）。
        /// </summary>
        public static string GetLabel(string? engineType, bool prefixWindows = false)
        {
            string core = engineType == WinRt
                ? "WinRT (OneCore)"
                : "SAPI (System.Speech)";
            return prefixWindows ? "Windows " + core : core;
        }
    }
}
