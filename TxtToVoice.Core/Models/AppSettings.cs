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

        /// <summary>
        /// 音声の内部識別子（WinRT: VoiceInformation.Id、SAPI: VoiceInfo.Id）。
        /// voiceName（DisplayName）より安定した識別子として保存する。
        /// 設定ファイルに存在しない場合は空文字列（後方互換: voiceName で検索）。
        /// </summary>
        [JsonPropertyName("voiceId")]
        public string VoiceId { get; set; } = string.Empty;

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

        // ----------------------------------------------------------------
        // 監査モード拡張：ログ削除
        // ----------------------------------------------------------------

        /// <summary>終了時にその日のログファイルを削除するかどうか（監査向け、デフォルト: false）</summary>
        [JsonPropertyName("deleteLogOnExit")]
        public bool DeleteLogOnExit { get; set; } = false;

        // ----------------------------------------------------------------
        // 音声エンジン
        // ----------------------------------------------------------------

        /// <summary>
        /// 使用する音声エンジン種別（デフォルト: "SystemSpeech"）。
        /// 有効値は <c>TxtToVoice.Services.SpeechEngineFactory</c> の定数を参照すること
        /// （Models 層は Services に依存させないため文字列リテラルで保持）。
        /// </summary>
        [JsonPropertyName("speechEngineType")]
        public string SpeechEngineType { get; set; } = "SystemSpeech";

        // ----------------------------------------------------------------
        // 監査ログ保持期間
        // ----------------------------------------------------------------

        /// <summary>
        /// 月次監査ログの保持期間（か月）。0 = 無制限（自動削除なし）。デフォルト: 13。
        /// 起動時および設定変更時に閾値超の古い audit_YYYYMM.csv を自動削除する。
        /// </summary>
        [JsonPropertyName("auditRetentionMonths")]
        public int AuditRetentionMonths { get; set; } = 13;

        // ----------------------------------------------------------------
        // プレビュー設定
        // ----------------------------------------------------------------

        /// <summary>
        /// SSML ポーズ強度（0=短め / 1=標準 / 2=長め、デフォルト: 1）。
        /// SsmlBuilder.Build() の pauseStrength パラメータに渡す値。
        /// </summary>
        [JsonPropertyName("ssmlPauseStrength")]
        public int SsmlPauseStrength { get; set; } = 1;

        /// <summary>
        /// 音声保存ダイアログのデフォルトファイル名プレフィックス（デフォルト: "kouhou"）。
        /// 命名テンプレート内の {prefix} 変数として使用される。
        /// </summary>
        [JsonPropertyName("saveFilePrefix")]
        public string SaveFilePrefix { get; set; } = "kouhou";

        /// <summary>
        /// 音声保存ファイル名の命名テンプレート（デフォルト: "{prefix}_{datetime}"）。
        /// 変数: {prefix} {date}=yyyyMMdd {time}=HHmmss {datetime}=yyyyMMdd_HHmmss {title}=原稿先頭行
        /// </summary>
        [JsonPropertyName("fileNameTemplate")]
        public string FileNameTemplate { get; set; } = "{prefix}_{datetime}";

        /// <summary>自動プレビュー更新（ChkAutoPreview）が有効かどうか（デフォルト: false）</summary>
        [JsonPropertyName("autoPreviewEnabled")]
        public bool AutoPreviewEnabled { get; set; } = false;

        /// <summary>プレビューを注釈付き（【元表記→読み】）で表示するかどうか（デフォルト: true）</summary>
        [JsonPropertyName("annotatedPreviewMode")]
        public bool AnnotatedPreviewMode { get; set; } = true;
    }
}
