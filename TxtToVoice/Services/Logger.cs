using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace TxtToVoice.Services
{
    /// <summary>
    /// シンプルなファイルロガー。
    /// ログは AppData\Local\TxtToVoice\logs\app_YYYYMMDD.log に出力される。
    ///
    /// 監査モード（SuppressInfo = true）では INFO レベルの書き込みを抑制する。
    /// WARN / ERROR は常に記録する。
    /// </summary>
    public static class Logger
    {
        private static string LogDir => PathConfig.LogDirectory;

        private static string LogPath =>
            Path.Combine(LogDir, $"app_{DateTime.Now:yyyyMMdd}.log");

        // 複数スレッド（UIスレッド + 音声合成スレッド）から呼ばれるためロックで保護
        private static readonly object _lock = new();

        /// <summary>
        /// true のとき INFO レベルのログ書き込みを抑制する（監査モード向け）。
        /// WARN / ERROR は引き続き記録される。
        /// </summary>
        public static bool SuppressInfo { get; set; } = false;

        public static void Info(string message)
        {
            if (SuppressInfo) return;
            Write("INFO ", message);
        }

        public static void Warn(string message)  => Write("WARN ", message);
        public static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            // 監査モードでは WARN/ERROR のメッセージ中にあるファイルフルパスをファイル名のみに置換する
            if (SuppressInfo)
                message = AnonymizePaths(message);

            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(LogDir);
                    string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
                    File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // ログ書き込みに失敗してもアプリは継続する
                }
            }
        }

        /// <summary>
        /// メッセージ中の Windows 絶対パス・UNC パスをファイル名のみに置換する。
        /// 対応パターン:
        ///   1. シングルクォート付き（.NET 例外メッセージに多い形式、空白含むパスに対応）
        ///   2. ダブルクォート付き（空白含むパスに対応）
        ///   3. クォートなしドライブレターパス（空白・コンマ・クォートで切る）
        ///   4. クォートなし UNC パス（\\server\share\... 形式）
        /// </summary>
        internal static string AnonymizePaths(string message)
        {
            // 1. シングルクォート付きパス（空白含む・UNC 両対応、.NET 例外メッセージに多い）
            message = Regex.Replace(message, @"'(?:[A-Za-z]:\\|\\\\)[^']+'",
                m =>
                {
                    string inner = m.Value[1..^1];
                    try   { return $"'{Path.GetFileName(inner)}'"; }
                    catch { return m.Value; }
                });
            // 2. ダブルクォート付きパス（空白含む・UNC 両対応）
            message = Regex.Replace(message, @"""(?:[A-Za-z]:\\|\\\\)[^""]+""",
                m =>
                {
                    string inner = m.Value[1..^1];
                    try   { return $"\"{Path.GetFileName(inner)}\""; }
                    catch { return m.Value; }
                });
            // 3. クォートなしドライブレターパス（空白・コンマ・クォートで区切る）
            message = Regex.Replace(message, @"[A-Za-z]:\\[^\s,""']+",
                m => { try { return Path.GetFileName(m.Value); } catch { return m.Value; } });
            // 4. クォートなし UNC パス（\\server\share\... 形式）
            message = Regex.Replace(message, @"\\\\[A-Za-z0-9._-]+\\[^\s,""']+",
                m => { try { return Path.GetFileName(m.Value); } catch { return m.Value; } });
            return message;
        }

        /// <summary>
        /// 今日のログファイルを削除する。終了時ログ消去オプション（監査向け）で使用する。
        /// 削除に失敗してもアプリは継続する。
        /// </summary>
        public static void DeleteTodayLog()
        {
            lock (_lock)
            {
                try
                {
                    string path = LogPath;
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch
                {
                    // 削除に失敗しても無視する
                }
            }
        }
    }
}
