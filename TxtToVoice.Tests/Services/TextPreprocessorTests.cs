using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// TextPreprocessor のゴールデンサンプルテスト。
    /// 入力 → 期待読み仮名の変換を回帰テストとして固定する。
    /// </summary>
    public class TextPreprocessorTests
    {
        // ================================================================
        // 月表記（フェーズ1）
        // ================================================================

        [Theory]
        [InlineData("1月",  "いちがつ")]
        [InlineData("2月",  "にがつ")]
        [InlineData("3月",  "さんがつ")]
        [InlineData("4月",  "しがつ")]
        [InlineData("5月",  "ごがつ")]
        [InlineData("6月",  "ろくがつ")]
        [InlineData("7月",  "しちがつ")]
        [InlineData("8月",  "はちがつ")]
        [InlineData("9月",  "くがつ")]
        [InlineData("10月", "じゅうがつ")]
        [InlineData("11月", "じゅういちがつ")]
        [InlineData("12月", "じゅうにがつ")]
        public void Apply_月_1から12を読み仮名に変換する(string input, string expected)
        {
            Assert.Equal(expected, TextPreprocessor.Apply(input));
        }

        [Theory]
        [InlineData("0月",  "0月")]   // 範囲外はそのまま
        [InlineData("13月", "13月")]  // 範囲外はそのまま
        public void Apply_月_範囲外はそのまま(string input, string expected)
        {
            Assert.Equal(expected, TextPreprocessor.Apply(input));
        }

        // ================================================================
        // パーセント（フェーズ1）
        // ================================================================

        [Theory]
        [InlineData("10%",   "10パーセント")]
        [InlineData("50%",   "50パーセント")]
        [InlineData("100%",  "100パーセント")]
        [InlineData("0.5%",  "0.5パーセント")]
        public void Apply_パーセント_記号をカタカナに変換する(string input, string expected)
        {
            Assert.Equal(expected, TextPreprocessor.Apply(input));
        }

        // ================================================================
        // 複合ケース
        // ================================================================

        [Theory]
        [InlineData("令和5年3月15日", "令和5年さんがつ15日")]
        [InlineData("消費税は10%です", "消費税は10パーセントです")]
        [InlineData("4月から10%値上げ", "しがつから10パーセント値上げ")]
        [InlineData("前年比120%で3月に達成", "前年比120パーセントでさんがつに達成")]
        public void Apply_複合ケース_正しく変換される(string input, string expected)
        {
            Assert.Equal(expected, TextPreprocessor.Apply(input));
        }

        // ================================================================
        // 無変換ケース（副作用なし確認）
        // ================================================================

        [Theory]
        [InlineData("今日は良い天気です")]
        [InlineData("")]
        public void Apply_変換対象なし_入力がそのまま返る(string input)
        {
            Assert.Equal(input, TextPreprocessor.Apply(input));
        }
    }
}
