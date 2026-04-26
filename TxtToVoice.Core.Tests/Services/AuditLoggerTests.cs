using System;
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

            string[] cols = lines[1].Split(',');
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

            string[] cols = File.ReadAllLines(_csvPath)[1].Split(',');
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

            string[] cols = File.ReadAllLines(_csvPath)[1].Split(',');
            Assert.Equal(string.Empty, cols[5]);
        }

        [Fact]
        public void Record_timestampがISO8601形式()
        {
            AuditLogger.Record("SystemSpeech", "wav", success: true, outputPath: @"C:\x.wav");

            string ts = File.ReadAllLines(_csvPath)[1].Split(',')[0];
            Assert.True(DateTime.TryParse(ts, out _), $"ISO 8601 パース失敗: {ts}");
        }
    }

    /// <summary>テスト用: AuditLogger の出力パスを差し替えるヘルパー。</summary>
    internal static class AuditLoggerTestHelper
    {
        public static void OverridePath(string path)  => AuditLogger.TestOverridePath = path;
        public static void ResetPath()                => AuditLogger.TestOverridePath = null;
    }
}
