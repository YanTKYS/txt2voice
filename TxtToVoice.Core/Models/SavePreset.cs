using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TxtToVoice.Models
{
    /// <summary>音声保存プリセット（ファイル名テンプレート・保存形式・SSML強度をまとめて保存）。</summary>
    public sealed class SavePreset
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        /// <summary>"single" = 単体保存 / "batch" = 一括保存</summary>
        [JsonPropertyName("saveMode")]
        public string SaveMode { get; init; } = "single";

        [JsonPropertyName("fileNameTemplate")]
        public string FileNameTemplate { get; init; } = "{prefix}_{datetime}";

        [JsonPropertyName("batchFormats")]
        public List<string> BatchFormats { get; init; } = new() { "mp3" };

        /// <summary>SSML ポーズ強度（0=短め / 1=標準 / 2=長め）。</summary>
        [JsonPropertyName("ssmlStrength")]
        public int SsmlStrength { get; init; } = 1;
    }
}
