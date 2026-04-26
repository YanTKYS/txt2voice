using System;

namespace TxtToVoice.Services
{
    /// <summary>
    /// テキストを SSML（Speech Synthesis Markup Language）に変換するビルダー。
    /// 句読点・改行に &lt;break&gt; タグを自動挿入して自然なポーズを付与する。
    ///
    /// ポーズ強度（pauseStrength）:
    ///   0 = 短め  : 句点300ms / 読点100ms / 改行200ms / 空行400ms
    ///   1 = 標準  : 句点600ms / 読点200ms / 改行400ms / 空行800ms（既定）
    ///   2 = 長め  : 句点900ms / 読点300ms / 改行600ms / 空行1200ms
    /// </summary>
    public static class SsmlBuilder
    {
        private const string Header =
            "<speak version=\"1.0\" " +
            "xmlns=\"http://www.w3.org/2001/10/synthesis\" " +
            "xml:lang=\"ja-JP\">";
        private const string Footer = "</speak>";

        /// <summary>
        /// テキストを SSML 文字列に変換する。
        /// XML 特殊文字をエスケープした後、句読点・改行の後に &lt;break&gt; タグを挿入する。
        /// </summary>
        /// <param name="text">変換元テキスト</param>
        /// <param name="pauseStrength">ポーズ強度 0=短め / 1=標準（既定）/ 2=長め</param>
        public static string Build(string text, int pauseStrength = 1)
        {
            if (string.IsNullOrEmpty(text)) return Header + Footer;

            double m = pauseStrength switch { 0 => 0.5, 2 => 1.5, _ => 1.0 };
            int pStop   = (int)Math.Round(600 * m);  // 句点・感嘆・疑問
            int pComma  = (int)Math.Round(200 * m);  // 読点・中点
            int pLine   = (int)Math.Round(400 * m);  // 改行
            int pPara   = (int)Math.Round(800 * m);  // 空行（段落）

            string content = EscapeXml(text);

            // 空行（段落区切り）を先に処理
            content = content
                .Replace("\r\n\r\n", $"<break time=\"{pPara}ms\"/>")
                .Replace("\n\n",     $"<break time=\"{pPara}ms\"/>")
                .Replace("\r\n",     $"<break time=\"{pLine}ms\"/>")
                .Replace("\n",       $"<break time=\"{pLine}ms\"/>");

            // 終止符・感嘆符・疑問符の後にポーズ
            foreach (char c in "。！？")
                content = content.Replace(c.ToString(), $"{c}<break time=\"{pStop}ms\"/>");

            // 読点・中点の後にポーズ
            foreach (char c in "、・")
                content = content.Replace(c.ToString(), $"{c}<break time=\"{pComma}ms\"/>");

            return Header + content + Footer;
        }

        private static string EscapeXml(string text) =>
            text.Replace("&",  "&amp;")
                .Replace("<",  "&lt;")
                .Replace(">",  "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'",  "&apos;");
    }
}
