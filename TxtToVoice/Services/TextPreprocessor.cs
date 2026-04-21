using System.Text.RegularExpressions;

namespace TxtToVoice.Services
{
    /// <summary>
    /// OpenJTalk 向けテキスト前処理。
    /// MeCab が誤読しやすい表記パターンを読み仮名に変換する。
    ///
    /// フェーズ1（実装済み）: 月表記・パーセント
    /// フェーズ2（未実装）  : 記号（〒 ℃ ㎡ など）
    /// フェーズ3（未実装）  : 慣用表現
    /// </summary>
    internal static class TextPreprocessor
    {
        private static readonly string[] MonthReadings =
        {
            "いちがつ", "にがつ", "さんがつ", "しがつ", "ごがつ", "ろくがつ",
            "しちがつ", "はちがつ", "くがつ", "じゅうがつ", "じゅういちがつ", "じゅうにがつ"
        };

        // \d{1,2}月 — 1〜12 の月表記のみ変換（0月・13月以上はそのまま）
        private static readonly Regex MonthPattern = new(@"(\d{1,2})月", RegexOptions.Compiled);

        public static string Apply(string text)
        {
            text = MonthPattern.Replace(text, m =>
            {
                if (int.TryParse(m.Groups[1].Value, out int n) && n >= 1 && n <= 12)
                    return MonthReadings[n - 1];
                return m.Value;
            });

            text = text.Replace("%", "パーセント");

            return text;
        }
    }
}
