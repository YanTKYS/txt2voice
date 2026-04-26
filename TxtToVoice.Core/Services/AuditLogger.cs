using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TxtToVoice.Services
{
    /// <summary>
    /// 音声ファイル保存操作の監査ログを CSV に追記する。
    ///
    /// 出力先: PathConfig.AuditLogPath（通常モード: %LOCALAPPDATA%\TxtToVoice\audit.csv）
    /// 列定義: timestamp, engineType, format, success, errorCode, fileHash
    ///
    /// 個人情報ゼロ方針:
    ///   ファイル名は SHA-256 ハッシュ先頭 8 文字のみ記録し、実パスは保持しない。
    /// </summary>
    public static class AuditLogger
    {
        private static readonly object _fileLock = new();
        private const string CsvHeader = "timestamp,engineType,format,success,errorCode,fileHash";

        // テスト専用: null のときは PathConfig.AuditLogPath を使用する
        internal static string? TestOverridePath;

        /// <summary>
        /// 保存操作の結果を監査ログに 1 行追記する。
        /// </summary>
        /// <param name="engineType">エンジン種別（SpeechEngineTypes 定数）</param>
        /// <param name="format">保存フォーマット（"wav" / "mp3" / "mp4"）</param>
        /// <param name="success">保存成功なら true</param>
        /// <param name="errorCode">失敗時のエラーコード（省略可）</param>
        /// <param name="outputPath">保存先ファイルパス（ハッシュ化して記録、省略可）</param>
        public static void Record(string engineType, string format, bool success,
            string? errorCode = null, string? outputPath = null)
        {
            try
            {
                string logPath = TestOverridePath ?? PathConfig.AuditLogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

                string fileHash = success && !string.IsNullOrEmpty(outputPath)
                    ? HashPath8(outputPath)
                    : string.Empty;

                string line = string.Join(",",
                    DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    CsvEscape(engineType),
                    CsvEscape(format),
                    success ? "true" : "false",
                    CsvEscape(errorCode ?? string.Empty),
                    fileHash);

                lock (_fileLock)
                {
                    bool isNew = !File.Exists(logPath);
                    if (isNew)
                        File.AppendAllText(logPath, CsvHeader + Environment.NewLine, Encoding.UTF8);
                    File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"AuditLogger: CSV 書き込み失敗: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------
        // ヘルパー
        // ----------------------------------------------------------------

        // ファイル名部分のみを SHA-256 でハッシュし先頭 8 文字（16 進数小文字）を返す
        private static string HashPath8(string path)
        {
            string name = Path.GetFileName(path);
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
            return Convert.ToHexString(hash)[..8].ToLowerInvariant();
        }

        // RFC 4180 に準拠した CSV フィールドのエスケープ
        private static string CsvEscape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return '"' + value.Replace("\"", "\"\"") + '"';
            return value;
        }
    }
}
