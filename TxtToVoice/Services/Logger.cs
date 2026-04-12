using System;
using System.IO;
using System.Text;

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
