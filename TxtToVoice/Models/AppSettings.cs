using System.Text.Json.Serialization;

namespace TxtToVoice.Models
{
    /// <summary>アプリケーション設定（読み上げ速度・音量・選択音声・SSML・セッションテキスト）</summary>
    public class AppSettings
    {
        [JsonPropertyName("rate")]
        public int Rate { get; set; } = 0;

        [JsonPropertyName("volume")]
        public int Volume { get; set; } = 100;

        [JsonPropertyName("voiceName")]
        public string VoiceName { get; set; } = string.Empty;

        /// <summary>SSML ポーズ挿入が有効かどうか</summary>
        [JsonPropertyName("ssmlPauseEnabled")]
        public bool SsmlPauseEnabled { get; set; } = false;

        /// <summary>前回セッションの入力テキスト（10,000文字以内のみ保存）</summary>
        [JsonPropertyName("lastInputText")]
        public string LastInputText { get; set; } = string.Empty;
    }
}
