using System;
using System.IO;

namespace TxtToVoice.Services
{
    /// <summary>
    /// 音声保存ファイル名の命名テンプレートを解決するユーティリティ。
    ///
    /// サポート変数:
    ///   {prefix}   — SaveFilePrefix の値
    ///   {date}     — yyyyMMdd 形式の日付
    ///   {time}     — HHmmss 形式の時刻
    ///   {datetime} — yyyyMMdd_HHmmss 形式の日時
    ///   {title}    — 原稿テキストの先頭行（最大 30 文字）
    /// </summary>
    public static class FileNameBuilder
    {
        public static string Build(string template, string prefix, string inputText, DateTimeOffset now)
        {
            string title = ExtractTitle(inputText);
            string result = template
                .Replace("{prefix}",   SanitizeFileName(prefix),            StringComparison.Ordinal)
                .Replace("{datetime}", now.ToString("yyyyMMdd_HHmmss"),      StringComparison.Ordinal)
                .Replace("{date}",     now.ToString("yyyyMMdd"),             StringComparison.Ordinal)
                .Replace("{time}",     now.ToString("HHmmss"),               StringComparison.Ordinal)
                .Replace("{title}",    SanitizeFileName(title),              StringComparison.Ordinal);

            // テンプレートが空になった場合のフォールバック
            return string.IsNullOrWhiteSpace(result)
                ? $"{SanitizeFileName(prefix)}_{now:yyyyMMdd_HHmmss}"
                : result;
        }

        /// <summary>テンプレートプレビュー文字列を返す（現在時刻・空テキスト想定）。</summary>
        public static string Preview(string template, string prefix)
            => Build(template, prefix, string.Empty, DateTimeOffset.Now);

        private static string ExtractTitle(string text)
        {
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r').Trim();
                if (!string.IsNullOrEmpty(line))
                    return line.Length > 30 ? line[..30] : line;
            }
            return string.Empty;
        }

        private static string SanitizeFileName(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }
    }
}
