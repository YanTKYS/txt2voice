using System.Text;
using System.Text.RegularExpressions;

namespace TxtToVoice.Services
{
    /// <summary>
    /// OpenJTalk 向けテキスト前処理。
    /// MeCab が誤読しやすい表記パターンを読み仮名に変換する。
    ///
    /// フェーズ1（v0.4.2）: 月表記 X月（1〜12）の読み仮名変換、% → パーセント
    /// フェーズ2（v0.4.3）: 全角数字・全角パーセント正規化、〒 ℃ ㎡ 記号変換
    /// フェーズ3（未実装） : 慣用表現
    /// </summary>
    public static class TextPreprocessor
    {
        private static readonly string[] MonthReadings =
        {
            "いちがつ", "にがつ", "さんがつ", "しがつ", "ごがつ", "ろくがつ",
            "しちがつ", "はちがつ", "くがつ", "じゅうがつ", "じゅういちがつ", "じゅうにがつ"
        };

        // \d{1,2}月 — 1〜12 の月表記のみ変換（半角数字前提; 全角正規化後に適用）
        private static readonly Regex MonthPattern =
            new(@"(\d{1,2})月", RegexOptions.Compiled);

        public static string Apply(string text)
        {
            // フェーズ2: 全角数字・全角パーセントを半角に正規化
            text = NormalizeFullWidth(text);

            // フェーズ1: 月表記
            text = MonthPattern.Replace(text, m =>
            {
                if (int.TryParse(m.Groups[1].Value, out int n) && n >= 1 && n <= 12)
                    return MonthReadings[n - 1];
                return m.Value;
            });

            // フェーズ1: パーセント
            text = text.Replace("%", "パーセント");

            // フェーズ2: 記号読み
            text = text.Replace("〒", "ゆうびんばんごう");
            text = text.Replace("℃", "ど");
            text = text.Replace("㎡", "へいほうめーとる");

            return text;
        }

        // 全角数字 ０-９ → 半角 0-9、全角パーセント ％ → 半角 %
        private static string NormalizeFullWidth(string text)
        {
            // 変換対象文字がない場合はアロケーションを省く
            bool hasFullWidth = false;
            foreach (char c in text)
            {
                if ((c >= '０' && c <= '９') || c == '％') { hasFullWidth = true; break; }
            }
            if (!hasFullWidth) return text;

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c >= '０' && c <= '９')  sb.Append((char)(c - '０' + '0'));
                else if (c == '％')          sb.Append('%');
                else                         sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
