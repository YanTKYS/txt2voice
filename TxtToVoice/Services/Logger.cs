using System;
using System.IO;
using System.Text;

namespace TxtToVoice.Services
{
    /// <summary>
    /// シンプルなファイルロガー。
    /// ログは AppData\Local\TxtToVoice\logs\app_YYYYMMDD.log に出力される。
    /// </summary>
    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TxtToVoice", "logs");

        private static string LogPath =>
            Path.Combine(LogDir, $"app_{DateTime.Now:yyyyMMdd}.log");

        // 複数スレッド（UIスレッド + 音声合成スレッド）から呼ばれるためロックで保護
        private static readonly object _lock = new();

        public static void Info(string message)  => Write("INFO ", message);
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
    }
}
