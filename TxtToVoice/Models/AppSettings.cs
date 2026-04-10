using System.Text.Json.Serialization;

namespace TxtToVoice.Models
{
    /// <summary>アプリケーション設定（読み上げ速度・音量・選択音声）</summary>
    public class AppSettings
    {
        [JsonPropertyName("rate")]
        public int Rate { get; set; } = 0;

        [JsonPropertyName("volume")]
        public int Volume { get; set; } = 100;

        [JsonPropertyName("voiceName")]
        public string VoiceName { get; set; } = string.Empty;
    }
}
