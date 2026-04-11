using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TxtToVoice.Models
{
    /// <summary>アプリケーション設定（読み上げ速度・音量・選択音声・SSML・セッションテキスト・最近使ったファイル）</summary>
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

        /// <summary>最近使ったファイルのパス一覧（最大 5 件、新しい順）</summary>
        [JsonPropertyName("recentFiles")]
        public List<string> RecentFiles { get; set; } = new();

        // ----------------------------------------------------------------
        // 機微データ保存ポリシー
        // ----------------------------------------------------------------

        /// <summary>前回テキストをアプリ終了時に保存するかどうか（デフォルト: true）</summary>
        [JsonPropertyName("saveLastInputText")]
        public bool SaveLastInputText { get; set; } = true;

        /// <summary>最近使ったファイルのリストを保存するかどうか（デフォルト: true）</summary>
        [JsonPropertyName("saveRecentFiles")]
        public bool SaveRecentFiles { get; set; } = true;

        /// <summary>終了時にテキスト・ファイル履歴を消去するかどうか（監査向け、デフォルト: false）</summary>
        [JsonPropertyName("clearSensitiveDataOnExit")]
        public bool ClearSensitiveDataOnExit { get; set; } = false;

        // ----------------------------------------------------------------
        // 読み上げ位置ハイライト
        // ----------------------------------------------------------------

        /// <summary>読み上げ中に蛍光色で現在位置をハイライト表示するかどうか（デフォルト: true）</summary>
        [JsonPropertyName("showReadingHighlight")]
        public bool ShowReadingHighlight { get; set; } = true;
    }
}
