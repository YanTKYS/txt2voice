using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// SpeechService のキャンセル伝播動作を検証するテスト。
    ///
    /// 音声エンジン（Windows SAPI）を必要としない「キャンセル先行検知」の動作のみを対象とする。
    /// CI など音声エンジンが不在の環境でも実行可能。
    ///
    /// SaveToFileAsync は Task.Run(..., ct) を使用しているため、
    /// ct がキャンセル済みの場合は lambda 実行前にタスクがキャンセル状態になり、
    /// await で OperationCanceledException がスローされる。
    /// </summary>
    public class SpeechServiceCancelTests : IDisposable
    {
        private readonly SpeechService _svc = new();
        private readonly string _tempDir;

        public SpeechServiceCancelTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"TxtToVoiceTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            _svc.Dispose();
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* 無視 */ }
        }

        // ================================================================
        // キャンセル先行検知（エンジン不要）
        // ================================================================

        [Fact]
        public async Task SaveToFileAsync_WAV_キャンセル済みTokenで即OperationCanceledをスロー()
        {
            string outputPath = Path.Combine(_tempDir, "test.wav");
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // 事前キャンセル

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _svc.SaveToFileAsync("テスト", outputPath, AudioFormat.Wav, ct: cts.Token));

            // キャンセルが先行したため書きかけファイルは生成されていないこと
            Assert.False(File.Exists(outputPath));
        }

        [Fact]
        public async Task SaveToFileAsync_MP3_キャンセル済みTokenで即OperationCanceledをスロー()
        {
            string outputPath = Path.Combine(_tempDir, "test.mp3");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _svc.SaveToFileAsync("テスト", outputPath, AudioFormat.Mp3, ct: cts.Token));

            Assert.False(File.Exists(outputPath));
        }

        [Fact]
        public async Task SaveToFileAsync_MP4_キャンセル済みTokenで即OperationCanceledをスロー()
        {
            string outputPath = Path.Combine(_tempDir, "test.mp4");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _svc.SaveToFileAsync("テスト", outputPath, AudioFormat.Mp4, ct: cts.Token));

            Assert.False(File.Exists(outputPath));
        }

        // ================================================================
        // SpeechService の初期化（音声エンジン不在への耐性）
        // ================================================================

        [Fact]
        public void SpeechService_初期化時に例外をスローしない()
        {
            // 音声エンジンが不在でも IsAvailable=false として正常に初期化されること
            // Record.Exception で「例外が発生しないこと」を明示的に検証する
            var ex = Record.Exception(() => { using var svc = new SpeechService(); });
            Assert.Null(ex);
        }

        [Fact]
        public void SpeechService_IsAvailable_false_のとき_InitializationError_が設定される()
        {
            using var svc = new SpeechService();
            if (svc.IsAvailable)
                Assert.Null(svc.InitializationError);
            else
                Assert.NotNull(svc.InitializationError);
        }
    }
}
