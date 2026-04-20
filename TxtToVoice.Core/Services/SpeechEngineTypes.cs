namespace TxtToVoice.Services
{
    /// <summary>
    /// 音声エンジン種別の文字列定数と検証ロジック。
    /// OS 非依存の Core 層に置き、AppSettingsService と SpeechEngineFactory の双方から参照する。
    /// </summary>
    public static class SpeechEngineTypes
    {
        public const string SystemSpeech = "SystemSpeech";
        public const string WinRt        = "WinRT";
        public const string OpenJTalk    = "OpenJTalk";

        /// <summary>未知の値を受け取ったときの既定種別。</summary>
        public const string Default = SystemSpeech;

        /// <summary>指定した文字列が既知の有効なエンジン種別かどうかを返す。</summary>
        public static bool IsKnown(string? type) => type == SystemSpeech || type == WinRt || type == OpenJTalk;
    }
}
