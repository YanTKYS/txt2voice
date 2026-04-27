using System.Text.Json.Serialization;

namespace TxtToVoice.Models
{
    public sealed class Template
    {
        [JsonPropertyName("title")]
        public string Title { get; init; } = "";

        [JsonPropertyName("content")]
        public string Content { get; init; } = "";
    }
}
