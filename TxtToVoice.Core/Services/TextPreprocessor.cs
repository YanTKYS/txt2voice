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
    /// フェーズ3（v0.4.4）: 慣用表現（Xか月、第X回、電話番号ハイフン）
    /// </summary>
    public static class TextPreprocessor
    {
        private static readonly string[] MonthReadings =
        {
            "いちがつ", "にがつ", "さんがつ", "しがつ", "ごがつ", "ろくがつ",
            "しちがつ", "はちがつ", "くがつ", "じゅうがつ", "じゅういちがつ", "じゅうにがつ"
        };

        private static readonly string[] SmallNumberReadings =
        {
            "", "いち", "に", "さん", "し", "ご", "ろく", "なな", "はち", "きゅう",
            "じゅう", "じゅういち", "じゅうに", "じゅうさん", "じゅうし", "じゅうご",
            "じゅうろく", "じゅうなな", "じゅうはち", "じゅうきゅう",
            "にじゅう", "にじゅういち", "にじゅうに", "にじゅうさん", "にじゅうし"
        };

        // \d{1,2}月 — 1〜12 の月表記のみ変換（半角数字前提; 全角正規化後に適用）
        private static readonly Regex MonthPattern =
            new(@"(\d{1,2})月", RegexOptions.Compiled);

        // フェーズ3: Xか月 / Xヶ月 / Xヵ月（1〜24）
        private static readonly Regex KagetsuPattern =
            new(@"(\d{1,2})[かヶヵ]月", RegexOptions.Compiled);

        // フェーズ3: 第X回（1〜24）
        private static readonly Regex DaiKaiPattern =
            new(@"第(\d{1,2})回", RegexOptions.Compiled);

        // フェーズ3: 電話番号のハイフン区切り（数字-数字）
        // 日本の市外局番・市内局番の区切りを想定。連続する数字群のみ対象。
        // 除外: 年月日の区切り（2024-01-01 等）は対象外とする
        private static readonly Regex PhonePattern =
            new(@"(?<!\d{4}-\d{2}-)(\d{2,5})-(\d{2,4})-(\d{4})", RegexOptions.Compiled);

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

            // フェーズ3: Xか月
            text = KagetsuPattern.Replace(text, m =>
            {
                if (int.TryParse(m.Groups[1].Value, out int n) && n >= 1 && n < SmallNumberReadings.Length)
                    return SmallNumberReadings[n] + "かげつ";
                return m.Value;
            });

            // フェーズ3: 第X回
            text = DaiKaiPattern.Replace(text, m =>
            {
                if (int.TryParse(m.Groups[1].Value, out int n) && n >= 1 && n < SmallNumberReadings.Length)
                    return "だい" + SmallNumberReadings[n] + "かい";
                return m.Value;
            });

            // フェーズ3: 電話番号ハイフン → 読み点「の」区切り
            text = PhonePattern.Replace(text, m =>
                m.Groups[1].Value + "の" + m.Groups[2].Value + "の" + m.Groups[3].Value);

            return text;
        }

        // 全角数字 ０-９ → 半角 0-9、全角パーセント ％ → 半角 %
        private static string NormalizeFullWidth(string text)
        {
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
