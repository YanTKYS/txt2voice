using System;
using System.Text.Json.Serialization;

namespace TxtToVoice.Models
{
    /// <summary>読み上げキューの1エントリ。Label（表示用短縮テキスト）・Text（本文）・作成日時を保持する。</summary>
    public sealed class QueueEntry
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }
    }
}
