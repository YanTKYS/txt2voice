using System;
using System.IO;
using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// 音声品質評価用 WAV 生成テスト（#51 OpenJTalk 音声品質評価インフラ）。
    ///
    /// S1〜S4 の固定原稿を OpenJTalk エンジンで WAV 合成する。
    /// 出力先は環境変数 VOICE_EVAL_OUTPUT_DIR（未設定時は %TEMP%\txtvoice-eval）。
    /// CI では voice-quality-eval-wavs アーティファクトとしてアップロードされる。
    ///
    /// 実行: dotnet test --filter "Category=VoiceQualityEval"
    /// 聴取評価結果の記入先: docs/release-checklist.md
    /// </summary>
    [Trait("Category", "RequiresEngine")]
    [Trait("Category", "VoiceQualityEval")]
    public class VoiceQualityEvalTests
    {
        private const string SkipReason =
            "OpenJTalk ランタイムが存在しません（jtalk.dll / 辞書 / 音声モデル）。" +
            "setup_openjtalk.ps1 を実行するか、CI の openjtalk-engine-test.yml を参照してください。";

        private static readonly string OutputDir =
            Environment.GetEnvironmentVariable("VOICE_EVAL_OUTPUT_DIR")
            ?? Path.Combine(Path.GetTempPath(), "txtvoice-eval");

        // S1〜S4 は #51 評価セットと同一内容（improvement-proposals.md より）
        private static readonly (string Id, string Script)[] EvalScripts =
        {
            ("S1", "今月3月の広報紙をお届けします。消費税は10%です。"),
            ("S2", "〒100-0001東京都千代田区、気温は25℃、面積50㎡。"),
            ("S3", "令和7年度の予算案について市民の皆様にご説明いたします。"),
            ("S4", "ご不明な点は、0120-XXX-YYYにお電話ください。"),
        };

        [SkippableFact]
        public void Generate_S1_月パーセント読み()
            => GenerateWav("S1");

        [SkippableFact]
        public void Generate_S2_記号読み()
            => GenerateWav("S2");

        [SkippableFact]
        public void Generate_S3_長文流暢さ()
            => GenerateWav("S3");

        [SkippableFact]
        public void Generate_S4_電話番号読み()
            => GenerateWav("S4");

        private static void GenerateWav(string id)
        {
            using var engine = new OpenJTalkEngine();
            Skip.IfNot(engine.IsAvailable, SkipReason);

            string script = FindScript(id);
            Directory.CreateDirectory(OutputDir);
            string wavPath = Path.Combine(OutputDir, id + ".wav");

            engine.SaveToFile(script, wavPath, AudioFormat.Wav);

            Assert.True(File.Exists(wavPath), $"WAV ファイルが生成されませんでした: {wavPath}");
            Assert.True(new FileInfo(wavPath).Length > 0, "WAV ファイルが空です。");
        }

        private static string FindScript(string id)
        {
            foreach (var (eid, script) in EvalScripts)
            {
                if (eid == id) return script;
            }
            throw new ArgumentException($"未知の評価スクリプト ID: {id}");
        }
    }
}
