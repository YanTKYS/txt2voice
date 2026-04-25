using System.Text.Json.Serialization;

namespace TxtToVoice.Models
{
    public sealed class TextRule
    {
        [JsonPropertyName("pattern")]
        public string Pattern { get; init; } = "";

        [JsonPropertyName("replacement")]
        public string Replacement { get; init; } = "";

        [JsonPropertyName("description")]
        public string Description { get; init; } = "";

        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; } = true;
    }
}
