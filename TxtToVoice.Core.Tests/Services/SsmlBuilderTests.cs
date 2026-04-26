using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// SsmlBuilder のテキスト変換ロジックを検証するテスト。
    /// </summary>
    public class SsmlBuilderTests
    {
        private const string SpeakOpen =
            "<speak version=\"1.0\" " +
            "xmlns=\"http://www.w3.org/2001/10/synthesis\" " +
            "xml:lang=\"ja-JP\">";
        private const string SpeakClose = "</speak>";

        // ----------------------------------------------------------------
        // 基本構造
        // ----------------------------------------------------------------

        [Fact]
        public void Build_EmptyString_ReturnsEmptySpeak()
        {
            string result = SsmlBuilder.Build(string.Empty);
            Assert.Equal(SpeakOpen + SpeakClose, result);
        }

        [Fact]
        public void Build_PlainText_WrapsInSpeak()
        {
            string result = SsmlBuilder.Build("こんにちは");
            Assert.StartsWith(SpeakOpen, result);
            Assert.EndsWith(SpeakClose, result);
            Assert.Contains("こんにちは", result);
        }

        // ----------------------------------------------------------------
        // XML エスケープ
        // ----------------------------------------------------------------

        [Fact]
        public void Build_AmpersandIsEscaped()
        {
            string result = SsmlBuilder.Build("A&B");
            Assert.Contains("A&amp;B", result);
        }

        [Fact]
        public void Build_LessThanIsEscaped()
        {
            string result = SsmlBuilder.Build("a<b");
            Assert.Contains("a&lt;b", result);
        }

        [Fact]
        public void Build_GreaterThanIsEscaped()
        {
            string result = SsmlBuilder.Build("a>b");
            Assert.Contains("a&gt;b", result);
        }

        // ----------------------------------------------------------------
        // 句読点ポーズ
        // ----------------------------------------------------------------

        [Fact]
        public void Build_PeriodInserts600ms()
        {
            string result = SsmlBuilder.Build("終わり。次へ");
            Assert.Contains("終わり。<break time=\"600ms\"/>次へ", result);
        }

        [Fact]
        public void Build_ExclamationInserts600ms()
        {
            string result = SsmlBuilder.Build("すごい！");
            Assert.Contains("すごい！<break time=\"600ms\"/>", result);
        }

        [Fact]
        public void Build_QuestionInserts600ms()
        {
            string result = SsmlBuilder.Build("本当？");
            Assert.Contains("本当？<break time=\"600ms\"/>", result);
        }

        [Fact]
        public void Build_CommaInserts200ms()
        {
            string result = SsmlBuilder.Build("えーと、次は");
            Assert.Contains("えーと、<break time=\"200ms\"/>次は", result);
        }

        [Fact]
        public void Build_MiddleDotInserts200ms()
        {
            string result = SsmlBuilder.Build("A・B");
            Assert.Contains("A・<break time=\"200ms\"/>B", result);
        }

        // ----------------------------------------------------------------
        // 改行ポーズ
        // ----------------------------------------------------------------

        [Fact]
        public void Build_SingleNewlineInserts400ms()
        {
            string result = SsmlBuilder.Build("行1\n行2");
            Assert.Contains("<break time=\"400ms\"/>行2", result);
        }

        [Fact]
        public void Build_CrLfInserts400ms()
        {
            string result = SsmlBuilder.Build("行1\r\n行2");
            Assert.Contains("<break time=\"400ms\"/>行2", result);
        }

        [Fact]
        public void Build_BlankLineInserts800ms()
        {
            string result = SsmlBuilder.Build("段落1\n\n段落2");
            Assert.Contains("<break time=\"800ms\"/>段落2", result);
        }

        [Fact]
        public void Build_BlankLineDoesNotAlsoInsert400ms()
        {
            // \n\n → 800ms のみ。400ms が残らないことを確認
            string result = SsmlBuilder.Build("段落1\n\n段落2");
            // 800ms は含まれる
            Assert.Contains("<break time=\"800ms\"/>", result);
            // 800ms に続けて 400ms が挿入されていないことを確認
            Assert.DoesNotContain("<break time=\"800ms\"/><break time=\"400ms\"/>", result);
        }

        // ----------------------------------------------------------------
        // ポーズ強度（pauseStrength）
        // ----------------------------------------------------------------

        [Fact]
        public void Build_PauseStrength0_HalfDuration()
        {
            string result = SsmlBuilder.Build("終わり。次へ", pauseStrength: 0);
            Assert.Contains("終わり。<break time=\"300ms\"/>次へ", result);
        }

        [Fact]
        public void Build_PauseStrength0_CommaHalfDuration()
        {
            string result = SsmlBuilder.Build("えーと、次は", pauseStrength: 0);
            Assert.Contains("えーと、<break time=\"100ms\"/>次は", result);
        }

        [Fact]
        public void Build_PauseStrength2_OneAndHalfDuration()
        {
            string result = SsmlBuilder.Build("終わり。次へ", pauseStrength: 2);
            Assert.Contains("終わり。<break time=\"900ms\"/>次へ", result);
        }

        [Fact]
        public void Build_PauseStrength2_BlankLineOneAndHalf()
        {
            string result = SsmlBuilder.Build("段落1\n\n段落2", pauseStrength: 2);
            Assert.Contains("<break time=\"1200ms\"/>段落2", result);
        }

        [Fact]
        public void Build_PauseStrength1_SameAsDefault()
        {
            // strength=1 は既定と同一結果であることを確認
            Assert.Equal(SsmlBuilder.Build("終わり。次へ"),
                         SsmlBuilder.Build("終わり。次へ", pauseStrength: 1));
        }
    }
}
