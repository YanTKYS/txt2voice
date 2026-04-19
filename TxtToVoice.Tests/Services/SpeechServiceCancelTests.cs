using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// SpeechService のキャンセル伝播動作を検証するテスト。
    ///
    /// 【エンジン不要テスト】キャンセル先行検知（token 既キャンセル）— CI で実行可能。
    /// SaveToFileAsync は Task.Run(..., ct) を使用しているため、
    /// ct がキャンセル済みの場合は lambda 実行前にタスクがキャンセル状態になり、
    /// await で OperationCanceledException がスローされる。
    ///
    /// 【RequiresEngine テスト】中間キャンセル— エンジン起動後にキャンセルを発火して
    /// OperationCanceledException が確実に伝播することを確認する。
    /// タイミング依存のため CI では [Trait("Category", "RequiresEngine")] で除外する。
    /// 実行除外: dotnet test --filter "Category!=RequiresEngine"
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
        // 中間キャンセル（RequiresEngine — エンジン起動後に CancelAfter で発火）
        // ================================================================

        [Fact]
        [Trait("Category", "RequiresEngine")]
        public async Task SaveToFileAsync_WAV_合成中キャンセルでOperationCanceledをスロー()
        {
            using var engine = new SystemSpeechEngine();
            if (!engine.IsAvailable) return;
            using var svc = new SpeechService(engine);

            string outputPath = Path.Combine(_tempDir, "midcancel.wav");
            // 長文で合成時間を稼ぐ（約 2000 文字; 50 ms 以内に完了する可能性は低い）
            string text = string.Concat(Enumerable.Repeat("これは中間キャンセルテスト用の長いテキストです。", 40));
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            try
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => svc.SaveToFileAsync(text, outputPath, AudioFormat.Wav, ct: cts.Token));
            }
            finally { try { File.Delete(outputPath); } catch { /* 無視 */ } }
        }

        [Fact]
        [Trait("Category", "RequiresEngine")]
        public async Task SaveToFileAsync_MP3_合成後エンコード前キャンセルでOperationCanceledをスロー()
        {
            using var engine = new SystemSpeechEngine();
            if (!engine.IsAvailable) return;
            using var svc = new SpeechService(engine);

            string outputPath = Path.Combine(_tempDir, "midcancel.mp3");
            // 長文で合成時間を稼ぐ
            string text = string.Concat(Enumerable.Repeat("これは中間キャンセルテスト用の長いテキストです。", 40));
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            try
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => svc.SaveToFileAsync(text, outputPath, AudioFormat.Mp3, ct: cts.Token));
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // CI 環境では Media Foundation の MP3 コーデックが利用できない場合がある
                return;
            }
            finally { try { File.Delete(outputPath); } catch { /* 無視 */ } }
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
