using System;
using System.IO;

namespace TxtToVoice.Services
{
    /// <summary>
    /// データ保存先パスを一元管理する静的クラス。
    ///
    /// ポータブルモード:
    ///   EXE と同じフォルダに portable.flag ファイルが存在し、かつ EXE フォルダへの
    ///   書き込みが可能な場合に有効。辞書・設定・ログをすべて EXE フォルダ配下に保存する。
    ///
    /// 通常モード:
    ///   %LOCALAPPDATA%\TxtToVoice\ に保存する（デフォルト）。
    ///
    /// ポータブルフォールバック:
    ///   portable.flag が存在するが EXE フォルダへの書き込みができない場合、
    ///   自動的に通常モードへ切り替える（PortableFallbackApplied = true）。
    /// </summary>
    public static class PathConfig
    {
        /// <summary>ポータブルモードで動作中かどうか。</summary>
        public static readonly bool IsPortable;

        /// <summary>
        /// portable.flag が存在したが書込不可のため通常モードへ自動切替した場合に true。
        /// MainWindow の起動時チェックで警告表示に使用する。
        /// </summary>
        public static readonly bool PortableFallbackApplied;

        /// <summary>辞書・設定ファイルの保存ディレクトリ。</summary>
        public static readonly string DataDirectory;

        /// <summary>ログファイルの保存ディレクトリ。</summary>
        public static readonly string LogDirectory;

        static PathConfig()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string flagFile = Path.Combine(exeDir, "portable.flag");
            bool requestedPortable = File.Exists(flagFile);

            string normalDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TxtToVoice");

            if (requestedPortable && TryWriteAccess(exeDir))
            {
                IsPortable             = true;
                PortableFallbackApplied = false;
                DataDirectory          = exeDir;
                LogDirectory           = Path.Combine(exeDir, "logs");
            }
            else
            {
                IsPortable             = false;
                PortableFallbackApplied = requestedPortable; // true = 書込不可でフォールバック
                DataDirectory          = normalDataDir;
                LogDirectory           = Path.Combine(normalDataDir, "logs");
            }
        }

        /// <summary>辞書 JSON ファイルのフルパス。</summary>
        public static string DictionaryPath => Path.Combine(DataDirectory, "dictionary.json");

        /// <summary>設定 JSON ファイルのフルパス。</summary>
        public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

        /// <summary>現在月の監査ログ CSV ファイルのフルパス（月次ローテーション形式）。</summary>
        public static string AuditLogPath => AuditLogPathForMonth(DateTimeOffset.Now);

        /// <summary>
        /// 指定月の監査ログ CSV ファイルのフルパス。
        /// ファイル名形式: audit_YYYYMM.csv（例: audit_202601.csv）
        /// </summary>
        public static string AuditLogPathForMonth(DateTimeOffset month) =>
            Path.Combine(DataDirectory, $"audit_{month:yyyyMM}.csv");

        /// <summary>
        /// テキスト変換ルール JSON ファイルのフルパス（EXE 配置ディレクトリ固定）。
        /// Program Files 等で書き込み不可の場合は <see cref="UserTextRulesPath"/> に保存される。
        /// </summary>
        public static string TextRulesPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "text_rules.json");

        /// <summary>
        /// ユーザーが保存した text_rules.json のフルパス（DataDirectory 下）。
        /// EXE 配置ディレクトリが書き込み不可のときの保存先フォールバック。
        /// </summary>
        public static string UserTextRulesPath =>
            Path.Combine(DataDirectory, "text_rules.json");

        /// <summary>
        /// 読み込み時に使う有効パス。ユーザー保存済みファイルを優先し、未保存時は EXE 配下デフォルトを返す。
        /// </summary>
        public static string EffectiveTextRulesPath =>
            File.Exists(UserTextRulesPath) ? UserTextRulesPath : TextRulesPath;

        /// <summary>原稿テンプレート JSON ファイルのフルパス。</summary>
        public static string TemplatesPath => Path.Combine(DataDirectory, "templates.json");

        /// <summary>再生プロファイル JSON ファイルのフルパス。</summary>
        public static string ProfilesPath => Path.Combine(DataDirectory, "profiles.json");

        /// <summary>保存プリセット JSON ファイルのフルパス。</summary>
        public static string SavePresetsPath => Path.Combine(DataDirectory, "save_presets.json");

        // ----------------------------------------------------------------
        // 書き込みテスト
        // ----------------------------------------------------------------

        private static bool TryWriteAccess(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string probe = Path.Combine(directory, ".write_probe");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
