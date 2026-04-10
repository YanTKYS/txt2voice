using System;

namespace TxtToVoice.Services
{
    /// <summary>
    /// テキストを SSML（Speech Synthesis Markup Language）に変換するビルダー。
    /// 句読点・改行に &lt;break&gt; タグを自動挿入して自然なポーズを付与する。
    ///
    /// ポーズ設定:
    ///   。！？  → 600 ms
    ///   、・    → 200 ms
    ///   改行    → 400 ms
    ///   空行    → 800 ms
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
        public static string Build(string text)
        {
            if (string.IsNullOrEmpty(text)) return Header + Footer;

            string content = EscapeXml(text);

            // 空行（段落区切り）を先に処理
            content = content
                .Replace("\r\n\r\n", "<break time=\"800ms\"/>")
                .Replace("\n\n",     "<break time=\"800ms\"/>")
                .Replace("\r\n",     "<break time=\"400ms\"/>")
                .Replace("\n",       "<break time=\"400ms\"/>");

            // 終止符・感嘆符・疑問符の後に 600ms ポーズ
            foreach (char c in "。！？")
                content = content.Replace(c.ToString(), c + "<break time=\"600ms\"/>");

            // 読点・中点の後に 200ms ポーズ
            foreach (char c in "、・")
                content = content.Replace(c.ToString(), c + "<break time=\"200ms\"/>");

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
