namespace TxtToVoice.Services
{
    /// <summary>
    /// アプリケーション共通エラーコード。
    /// ダイアログ表示文と Logger.Error() の両方に含め、問い合わせ時の一次切り分けに使用する。
    ///
    /// 命名規則: TTV-E-{コンポーネント}-{連番}
    ///   TTV-E-OJT-xxx  : OpenJTalk エンジン
    ///   TTV-E-SAPI-xxx : SAPI (SystemSpeech) エンジン
    ///   TTV-E-WRT-xxx  : WinRT エンジン
    ///   TTV-E-SAVE-xxx : 音声ファイル保存
    /// </summary>
    public static class TtvErrorCode
    {
        // ---- OpenJTalk エンジン ------------------------------------------------
        /// <summary>jtalk.dll が見つからない</summary>
        public const string OjtDllMissing   = "TTV-E-OJT-001";
        /// <summary>MeCab 辞書ディレクトリが見つからない</summary>
        public const string OjtDicMissing   = "TTV-E-OJT-002";
        /// <summary>音声モデル (.htsvoice) が見つからない</summary>
        public const string OjtVoiceMissing = "TTV-E-OJT-003";
        /// <summary>openjtalk_initialize() がゼロハンドルを返した</summary>
        public const string OjtInitFailed   = "TTV-E-OJT-004";
        /// <summary>openjtalk_speakToFile() が false を返した</summary>
        public const string OjtSynthFailed  = "TTV-E-OJT-005";

        // ---- SAPI (System.Speech) エンジン ------------------------------------
        /// <summary>SystemSpeechEngine の初期化に失敗した</summary>
        public const string SapiInitFailed  = "TTV-E-SAPI-001";

        // ---- WinRT エンジン ---------------------------------------------------
        /// <summary>WinRtSpeechEngine の初期化に失敗した</summary>
        public const string WrtInitFailed   = "TTV-E-WRT-001";

        // ---- 音声ファイル保存 -------------------------------------------------
        /// <summary>音声ファイルの保存に失敗した</summary>
        public const string SaveFailed      = "TTV-E-SAVE-001";

        // ---- ファイル I/O（設定・プロファイル・テンプレート等） ---------------
        /// <summary>再生プロファイルの保存に失敗した</summary>
        public const string IoProfileSaveFailed  = "TTV-E-IO-001";
        /// <summary>保存プリセットの保存に失敗した</summary>
        public const string IoPresetSaveFailed   = "TTV-E-IO-002";
        /// <summary>アプリ設定の保存に失敗した</summary>
        public const string IoSettingsSaveFailed = "TTV-E-IO-003";
        /// <summary>テンプレートの保存に失敗した</summary>
        public const string IoTemplateSaveFailed = "TTV-E-IO-004";
        /// <summary>読み上げキューの保存に失敗した</summary>
        public const string IoQueueSaveFailed    = "TTV-E-IO-005";
        /// <summary>CSV エクスポートに失敗した</summary>
        public const string IoCsvExportFailed    = "TTV-E-IO-006";

        // ---- 未ハンドル UI 例外 -----------------------------------------------
        /// <summary>DispatcherUnhandledException で捕捉された予期しない例外</summary>
        public const string UiUnhandled = "TTV-E-UI-001";
    }
}
