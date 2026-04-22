using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// TextPreprocessor のゴールデンサンプルテスト。
    /// 入力 → 期待読み仮名の変換を回帰テストとして固定する。
    /// OS 非依存テスト（net8.0 で常時実行可能）。
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
        // 全角数字・全角パーセント正規化（フェーズ2）
        // ================================================================

        [Theory]
        [InlineData("３月",  "さんがつ")]   // 全角数字 + 月
        [InlineData("１２月", "じゅうにがつ")]
        [InlineData("１０月", "じゅうがつ")]
        public void Apply_全角月_読み仮名に変換する(string input, string expected)
        {
            Assert.Equal(expected, TextPreprocessor.Apply(input));
        }

        [Theory]
        [InlineData("１０％", "10パーセント")]  // 全角数字 + 全角パーセント
        [InlineData("５０％", "50パーセント")]
        public void Apply_全角パーセント_カタカナに変換する(string input, string expected)
        {
            Assert.Equal(expected, TextPreprocessor.Apply(input));
        }

        // ================================================================
        // 記号読み（フェーズ2）
        // ================================================================

        [Theory]
        [InlineData("〒123-4567",    "ゆうびんばんごう123-4567")]
        [InlineData("気温は25℃です", "気温は25どです")]
        [InlineData("50㎡の部屋",   "50へいほうめーとるの部屋")]
        public void Apply_記号_読み仮名に変換する(string input, string expected)
        {
            Assert.Equal(expected, TextPreprocessor.Apply(input));
        }

        // ================================================================
        // 複合ケース
        // ================================================================

        [Theory]
        [InlineData("令和5年3月15日",          "令和5年さんがつ15日")]
        [InlineData("消費税は10%です",         "消費税は10パーセントです")]
        [InlineData("4月から10%値上げ",        "しがつから10パーセント値上げ")]
        [InlineData("前年比120%で3月に達成",   "前年比120パーセントでさんがつに達成")]
        [InlineData("〒100-0001の25℃、50㎡",  "ゆうびんばんごう100-0001の25ど、50へいほうめーとる")]
        [InlineData("３月の気温は１０℃",       "さんがつの気温は10ど")]
        public void Apply_複合ケース_正しく変換される(string input, string expected)
        {
            Assert.Equal(expected, TextPreprocessor.Apply(input));
        }

        // ================================================================
        // Xか月（フェーズ3）
        // ================================================================

        [Theory]
        [InlineData("3か月",   "さんかげつ")]
        [InlineData("1ヶ月",   "いちかげつ")]
        [InlineData("12ヵ月",  "じゅうにかげつ")]
        [InlineData("6か月後", "ろくかげつ後")]
        public void Apply_かげつ_読み仮名に変換する(string input, string expected)
        {
            Assert.Equal(expected, TextPreprocessor.Apply(input));
        }

        [Theory]
        [InlineData("0か月",  "0か月")]   // 範囲外はそのまま
        [InlineData("25か月", "25か月")]  // 範囲外はそのまま
        public void Apply_かげつ_範囲外はそのまま(string input, string expected)
        {
            Assert.Equal(expected, TextPreprocessor.Apply(input));
        }

        // ================================================================
        // 第X回（フェーズ3）
        // ================================================================

        [Theory]
        [InlineData("第1回",   "だいいちかい")]
        [InlineData("第3回",   "だいさんかい")]
        [InlineData("第10回",  "だいじゅうかい")]
        [InlineData("第20回大会", "だいにじゅうかい大会")]
        public void Apply_第X回_読み仮名に変換する(string input, string expected)
        {
            Assert.Equal(expected, TextPreprocessor.Apply(input));
        }

        // ================================================================
        // 電話番号ハイフン（フェーズ3）
        // ================================================================

        [Theory]
        [InlineData("0120-123-4567",  "0120の123の4567")]
        [InlineData("03-1234-5678",   "03の1234の5678")]
        [InlineData("090-1234-5678",  "090の1234の5678")]
        public void Apply_電話番号_ハイフンをのに変換する(string input, string expected)
        {
            Assert.Equal(expected, TextPreprocessor.Apply(input));
        }

        [Theory]
        [InlineData("2024-01-01", "2024-01-01")]  // 日付はそのまま（除外ケース）
        public void Apply_電話番号_日付形式は変換しない(string input, string expected)
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
