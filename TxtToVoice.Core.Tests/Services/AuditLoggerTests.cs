using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using TxtToVoice.Services;

namespace TxtToVoice.Tests.Services
{
    public class AuditLoggerTests : IDisposable
    {
        // テスト専用の一時ディレクトリに audit.csv を書き出す
        private readonly string _tmpDir;
        private readonly string _csvPath;

        public AuditLoggerTests()
        {
            _tmpDir  = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}");
            _csvPath = Path.Combine(_tmpDir, "audit.csv");
            Directory.CreateDirectory(_tmpDir);
            AuditLoggerTestHelper.OverridePath(_csvPath);
        }

        public void Dispose()
        {
            AuditLoggerTestHelper.ResetPath();
            try { Directory.Delete(_tmpDir, recursive: true); } catch { }
        }

        // ================================================================
        // ヘッダー・行フォーマット
        // ================================================================

        [Fact]
        public void Record_成功_CSVヘッダーと1行を書き込む()
        {
            AuditLogger.Record("SystemSpeech", "mp3", success: true, outputPath: @"C:\out\test.mp3");

            string[] lines = File.ReadAllLines(_csvPath);
            Assert.Equal(2, lines.Length);
            Assert.Equal("timestamp,engineType,format,success,errorCode,fileHash", lines[0]);

            string[] cols = ParseCsvLine(lines[1]);
            Assert.Equal(6, cols.Length);
            Assert.Equal("SystemSpeech", cols[1]);
            Assert.Equal("mp3",          cols[2]);
            Assert.Equal("true",         cols[3]);
            Assert.Equal(string.Empty,   cols[4]);
            Assert.Equal(8,              cols[5].Length); // SHA-256 先頭 8 文字
        }

        [Fact]
        public void Record_失敗_errorCodeが記録されfileHashが空()
        {
            AuditLogger.Record("OpenJTalk", "wav", success: false,
                errorCode: "TTV-E-SAVE-001");

            string[] cols = ParseCsvLine(File.ReadAllLines(_csvPath)[1]);
            Assert.Equal("OpenJTalk",       cols[1]);
            Assert.Equal("wav",             cols[2]);
            Assert.Equal("false",           cols[3]);
            Assert.Equal("TTV-E-SAVE-001",  cols[4]);
            Assert.Equal(string.Empty,      cols[5]); // 失敗時は fileHash なし
        }

        [Fact]
        public void Record_複数回_ヘッダーは1行のみ()
        {
            AuditLogger.Record("WinRT", "mp3", success: true,  outputPath: @"C:\a.mp3");
            AuditLogger.Record("WinRT", "wav", success: false, errorCode: "TTV-E-SAVE-001");

            string[] lines = File.ReadAllLines(_csvPath);
            Assert.Equal(3, lines.Length); // header + 2 data rows
            Assert.Equal(1, lines.Count(l => l.StartsWith("timestamp,")));
        }

        [Fact]
        public void Record_outputPathなし_fileHashが空()
        {
            AuditLogger.Record("SystemSpeech", "mp3", success: true);

            string[] cols = ParseCsvLine(File.ReadAllLines(_csvPath)[1]);
            Assert.Equal(string.Empty, cols[5]);
        }

        [Fact]
        public void Record_timestampがISO8601形式()
        {
            AuditLogger.Record("SystemSpeech", "wav", success: true, outputPath: @"C:\x.wav");

            string ts = ParseCsvLine(File.ReadAllLines(_csvPath)[1])[0];
            Assert.True(DateTime.TryParse(ts, out _), $"ISO 8601 パース失敗: {ts}");
        }

        [Fact]
        public void Record_engineTypeにカンマを含む場合_引用符でエスケープされる()
        {
            AuditLogger.Record("Engine,With,Commas", "wav", success: true);

            string[] cols = ParseCsvLine(File.ReadAllLines(_csvPath)[1]);
            Assert.Equal("Engine,With,Commas", cols[1]);
        }

        // ================================================================
        // 月次ローテーション
        // ================================================================

        [Fact]
        public void AuditLogPath_現在年月を含むファイル名になる()
        {
            string expected = $"audit_{DateTimeOffset.Now:yyyyMM}.csv";
            Assert.Contains(expected, PathConfig.AuditLogPath);
        }

        [Fact]
        public void AuditLogPathForMonth_指定月のファイル名を返す()
        {
            var month = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
            string path = PathConfig.AuditLogPathForMonth(month);
            Assert.EndsWith("audit_202601.csv", path);
        }

        [Fact]
        public void AuditLogPathForMonth_月が異なれば別ファイルになる()
        {
            var jan = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var feb = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.NotEqual(PathConfig.AuditLogPathForMonth(jan),
                            PathConfig.AuditLogPathForMonth(feb));
        }

        // ================================================================
        // PurgeOldLogs
        // ================================================================

        [Fact]
        public void PurgeOldLogs_保持期間超過ファイルを削除する()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"purge_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "audit_202201.csv"), "header\n");
                string thisMonth = DateTimeOffset.Now.ToString("yyyyMM");
                File.WriteAllText(Path.Combine(dir, $"audit_{thisMonth}.csv"), "header\n");

                AuditLogger.PurgeOldLogsFrom(retentionMonths: 13, dataDirectory: dir);

                Assert.False(File.Exists(Path.Combine(dir, "audit_202201.csv")),
                    "保持期間超過ファイルが削除されていない");
                Assert.True(File.Exists(Path.Combine(dir, $"audit_{thisMonth}.csv")),
                    "今月ファイルが誤って削除された");
            }
            finally { Directory.Delete(dir, recursive: true); }
        }

        [Fact]
        public void PurgeOldLogs_保持期間0は削除しない()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"purge_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "audit_202201.csv"), "header\n");
                AuditLogger.PurgeOldLogsFrom(retentionMonths: 0, dataDirectory: dir);
                Assert.True(File.Exists(Path.Combine(dir, "audit_202201.csv")),
                    "無制限設定なのにファイルが削除された");
            }
            finally { Directory.Delete(dir, recursive: true); }
        }

        [Fact]
        public void PurgeOldLogs_保持期間内ファイルは削除しない()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"purge_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                string lastMonth = DateTimeOffset.Now.AddMonths(-1).ToString("yyyyMM");
                File.WriteAllText(Path.Combine(dir, $"audit_{lastMonth}.csv"), "header\n");
                AuditLogger.PurgeOldLogsFrom(retentionMonths: 13, dataDirectory: dir);
                Assert.True(File.Exists(Path.Combine(dir, $"audit_{lastMonth}.csv")),
                    "保持期間内ファイルが誤って削除された");
            }
            finally { Directory.Delete(dir, recursive: true); }
        }

        [Fact]
        public void PurgeOldLogs_ディレクトリ不在でも例外を投げない()
        {
            string nonExistent = Path.Combine(Path.GetTempPath(), $"no_such_{Guid.NewGuid():N}");
            var ex = Record.Exception(() => AuditLogger.PurgeOldLogsFrom(13, nonExistent));
            Assert.Null(ex);
        }

        // ================================================================
        // ヘルパー: RFC 4180 準拠の CSV 1行パーサ
        // ================================================================

        private static string[] ParseCsvLine(string line)
        {
            var fields  = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            foreach (char c in line)
            {
                if (c == '"')
                    inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes)
                { fields.Add(current.ToString()); current.Clear(); }
                else
                    current.Append(c);
            }
            fields.Add(current.ToString());
            return fields.ToArray();
        }
    }

    /// <summary>テスト用: AuditLogger の出力パスを差し替えるヘルパー。</summary>
    internal static class AuditLoggerTestHelper
    {
        public static void OverridePath(string path)  => AuditLogger.TestOverridePath = path;
        public static void ResetPath()                => AuditLogger.TestOverridePath = null;
    }
}
