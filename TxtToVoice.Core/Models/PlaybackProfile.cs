using System.Text.Json.Serialization;

namespace TxtToVoice.Models
{
    public sealed class PlaybackProfile
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("voice_name")]
        public string VoiceName { get; init; } = "";

        [JsonPropertyName("rate")]
        public int Rate { get; init; } = 0;

        [JsonPropertyName("volume")]
        public int Volume { get; init; } = 100;

        [JsonPropertyName("ssml_enabled")]
        public bool SsmlEnabled { get; init; } = false;

        [JsonPropertyName("ssml_strength")]
        public int SsmlStrength { get; init; } = 1;
    }
}
