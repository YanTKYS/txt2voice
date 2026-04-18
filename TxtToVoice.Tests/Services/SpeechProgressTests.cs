using System.Collections.Generic;
using System.IO;
using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// 音声保存時のフェーズメッセージ（IProgress&lt;string&gt;）を検証するテスト（#28/#39）。
    ///
    /// [Trait("Category", "RequiresEngine")] が付いているテストは実際の音声エンジンを必要とする。
    /// エンジンが利用できない環境（CI 等）では IsAvailable チェックにより実質スキップされる。
    /// 実行除外: dotnet test --filter "Category!=RequiresEngine"
    /// </summary>
    public class SpeechProgressTests
    {
        // ================================================================
        // SAPI（SystemSpeechEngine）
        // ================================================================

        [Fact]
        [Trait("Category", "RequiresEngine")]
        public void SAPI_WAV保存時に合成フェーズメッセージを報告する()
        {
            using var engine = new SystemSpeechEngine();
            if (!engine.IsAvailable) return;

            var reported = new List<string>();
            var progress = new System.Progress<string>(msg => reported.Add(msg));

            string path = Path.ChangeExtension(Path.GetTempFileName(), ".wav");
            try
            {
                engine.SaveToFile("テスト", path, AudioFormat.Wav, progress: progress);
                Assert.Contains("音声を合成しています...", reported);
            }
            finally { try { File.Delete(path); } catch { /* 無視 */ } }
        }

        [Fact]
        [Trait("Category", "RequiresEngine")]
        public void SAPI_MP3保存時に合成フェーズとエンコードフェーズを報告する()
        {
            using var engine = new SystemSpeechEngine();
            if (!engine.IsAvailable) return;

            var reported = new List<string>();
            var progress = new System.Progress<string>(msg => reported.Add(msg));

            string path = Path.ChangeExtension(Path.GetTempFileName(), ".mp3");
            try
            {
                engine.SaveToFile("テスト", path, AudioFormat.Mp3, progress: progress);
                Assert.Contains("音声を合成しています...", reported);
                Assert.Contains("MP3 にエンコードしています...", reported);
            }
            finally { try { File.Delete(path); } catch { /* 無視 */ } }
        }

        [Fact]
        [Trait("Category", "RequiresEngine")]
        public void SAPI_MP4保存時にMP4エンコードフェーズを報告する()
        {
            using var engine = new SystemSpeechEngine();
            if (!engine.IsAvailable) return;

            var reported = new List<string>();
            var progress = new System.Progress<string>(msg => reported.Add(msg));

            string path = Path.ChangeExtension(Path.GetTempFileName(), ".mp4");
            try
            {
                engine.SaveToFile("テスト", path, AudioFormat.Mp4, progress: progress);
                Assert.Contains("MP4 にエンコードしています...", reported);
            }
            finally { try { File.Delete(path); } catch { /* 無視 */ } }
        }
    }
}
