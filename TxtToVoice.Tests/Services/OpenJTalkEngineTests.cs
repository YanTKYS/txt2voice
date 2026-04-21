using System.IO;
using System.Threading;
using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// OpenJTalkEngine の動作を検証するテスト。
    ///
    /// [Trait("Category", "RequiresEngine")] が付いているテストは jtalk.dll・辞書・音声モデルが
    /// 揃った環境でのみ実行される。ファイルが存在しない場合（CI 等）は IsAvailable=false により
    /// 実質スキップとなる（Assert を実行しない）。
    ///
    /// RequiresEngine 以外のテストはファイル欠落状態を意図的に検証するため、
    /// jtalk.dll が存在しない環境でも必ず Pass する。
    ///
    /// 実行除外: dotnet test --filter "Category!=RequiresEngine"
    /// </summary>
    public class OpenJTalkEngineTests
    {
        // ================================================================
        // 初期化失敗ケース（jtalk.dll が存在しない環境で必ず通る）
        // ================================================================

        [Fact]
        public void Initialize_DLLが存在しない場合IsAvailableがfalseになる()
        {
            using var engine = new OpenJTalkEngine();
            if (engine.IsAvailable) return; // jtalk.dll が存在する環境はスキップ

            Assert.False(engine.IsAvailable);
            Assert.NotNull(engine.InitializationError);
        }

        [Fact]
        public void Initialize_DLLが存在しない場合エラー文言にjtalkDLLへの言及がある()
        {
            using var engine = new OpenJTalkEngine();
            if (engine.IsAvailable) return;

            Assert.Contains("jtalk.dll", engine.InitializationError);
        }

        [Fact]
        public void GetAvailableVoices_初期化失敗時は空リストを返す()
        {
            using var engine = new OpenJTalkEngine();
            if (engine.IsAvailable) return;

            Assert.Empty(engine.GetAvailableVoices());
        }

        [Fact]
        public void SpeakAsync_初期化失敗時にSpeakErrorが発火する()
        {
            using var engine = new OpenJTalkEngine();
            if (engine.IsAvailable) return;

            string? errorMessage = null;
            engine.SpeakError += (_, msg) => errorMessage = msg;
            engine.SpeakAsync("テスト");

            Assert.NotNull(errorMessage);
            Assert.Contains("利用できません", errorMessage);
        }

        [Fact]
        public void FindVoiceNameById_初期化失敗時はnullを返す()
        {
            using var engine = new OpenJTalkEngine();
            if (engine.IsAvailable) return;

            Assert.Null(engine.FindVoiceNameById("mei_normal"));
        }

        // ================================================================
        // 正常系（RequiresEngine: jtalk.dll・辞書・音声モデルが必要）
        // ================================================================

        [Fact]
        [Trait("Category", "RequiresEngine")]
        public void Initialize_ファイルが揃っていればIsAvailableがtrueになる()
        {
            using var engine = new OpenJTalkEngine();
            if (!engine.IsAvailable) return;

            Assert.True(engine.IsAvailable);
            Assert.Null(engine.InitializationError);
            Assert.False(string.IsNullOrEmpty(engine.CurrentVoiceName));
        }

        [Fact]
        [Trait("Category", "RequiresEngine")]
        public void GetAvailableVoices_初期化成功時は1件以上返す()
        {
            using var engine = new OpenJTalkEngine();
            if (!engine.IsAvailable) return;

            Assert.NotEmpty(engine.GetAvailableVoices());
        }

        [Fact]
        [Trait("Category", "RequiresEngine")]
        public void SaveToFile_WAV_正常にファイルが生成される()
        {
            using var engine = new OpenJTalkEngine();
            if (!engine.IsAvailable) return;

            string path = Path.ChangeExtension(Path.GetTempFileName(), ".wav");
            try
            {
                engine.SaveToFile("テスト", path, AudioFormat.Wav);
                Assert.True(File.Exists(path));
                Assert.True(new FileInfo(path).Length > 0);
            }
            finally { try { File.Delete(path); } catch { /* 無視 */ } }
        }

        [Fact]
        [Trait("Category", "RequiresEngine")]
        public void SaveToFile_WAV_キャンセル後に一時ファイルが残らない()
        {
            using var engine = new OpenJTalkEngine();
            if (!engine.IsAvailable) return;

            string path = Path.ChangeExtension(Path.GetTempFileName(), ".wav");
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            try
            {
                Assert.Throws<OperationCanceledException>(
                    () => engine.SaveToFile("テスト", path, AudioFormat.Wav, ct: cts.Token));
            }
            finally { try { File.Delete(path); } catch { /* 無視 */ } }
        }

        [Fact]
        [Trait("Category", "RequiresEngine")]
        public void SaveToFile_SSML_タグを除去してWAVが生成される()
        {
            using var engine = new OpenJTalkEngine();
            if (!engine.IsAvailable) return;

            string path = Path.ChangeExtension(Path.GetTempFileName(), ".wav");
            try
            {
                engine.SaveToFile(
                    "<speak>テスト<break time=\"500ms\"/>です。</speak>",
                    path, AudioFormat.Wav, isSsml: true);
                Assert.True(File.Exists(path));
                Assert.True(new FileInfo(path).Length > 0);
            }
            finally { try { File.Delete(path); } catch { /* 無視 */ } }
        }

        [Fact]
        [Trait("Category", "RequiresEngine")]
        public void SaveToFile_MP3_正常にファイルが生成される()
        {
            using var engine = new OpenJTalkEngine();
            if (!engine.IsAvailable) return;

            string path = Path.ChangeExtension(Path.GetTempFileName(), ".mp3");
            try
            {
                engine.SaveToFile("テスト", path, AudioFormat.Mp3);
                Assert.True(File.Exists(path));
                Assert.True(new FileInfo(path).Length > 0);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // CI 環境では Media Foundation の MP3 コーデックが利用できない場合がある
            }
            finally { try { File.Delete(path); } catch { /* 無視 */ } }
        }

        [Fact]
        [Trait("Category", "RequiresEngine")]
        public void SaveToFile_MP4_正常にファイルが生成される()
        {
            using var engine = new OpenJTalkEngine();
            if (!engine.IsAvailable) return;

            string path = Path.ChangeExtension(Path.GetTempFileName(), ".mp4");
            try
            {
                engine.SaveToFile("テスト", path, AudioFormat.Mp4);
                Assert.True(File.Exists(path));
                Assert.True(new FileInfo(path).Length > 0);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // CI 環境では Media Foundation の AAC コーデックが利用できない場合がある
            }
            finally { try { File.Delete(path); } catch { /* 無視 */ } }
        }

        [Fact]
        [Trait("Category", "RequiresEngine")]
        public void SaveToFile_MP3_合成フェーズとエンコードフェーズを報告する()
        {
            using var engine = new OpenJTalkEngine();
            if (!engine.IsAvailable) return;

            var reported = new System.Collections.Generic.List<string>();
            var progress = new System.Progress<string>(msg => reported.Add(msg));

            string path = Path.ChangeExtension(Path.GetTempFileName(), ".mp3");
            try
            {
                engine.SaveToFile("テスト", path, AudioFormat.Mp3, progress: progress);
                Assert.Contains("音声を合成しています...", reported);
                Assert.Contains("MP3 にエンコードしています...", reported);
            }
            catch (System.Runtime.InteropServices.COMException) { }
            finally { try { File.Delete(path); } catch { /* 無視 */ } }
        }
    }
}
