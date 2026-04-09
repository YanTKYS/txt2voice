using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TxtToVoice.Models;

namespace TxtToVoice.Services
{
    /// <summary>
    /// 辞書の CSV インポート / エクスポートを担当するサービス。
    ///
    /// CSV 列順: 表記, 読み, 備考, 優先順位
    /// 1行目はヘッダー（「表記」で始まる場合はスキップ）。
    /// RFC 4180 に準じたクォート処理をサポートする。
    /// </summary>
    public static class CsvService
    {
        private static readonly Encoding CsvEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        // ----------------------------------------------------------------
        // インポート
        // ----------------------------------------------------------------

        /// <summary>
        /// CSV ファイルから辞書エントリのリストを読み込む。
        /// </summary>
        public static List<DictionaryEntry> Import(string filePath)
        {
            var entries = new List<DictionaryEntry>();

            // BOM 付き UTF-8 / UTF-8 / Shift_JIS を自動判別
            Encoding enc = DetectEncoding(filePath);
            string[] lines = File.ReadAllLines(filePath, enc);

            int startLine = 0;
            if (lines.Length > 0 && lines[0].TrimStart().StartsWith("表記", StringComparison.Ordinal))
                startLine = 1;

            for (int i = startLine; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                List<string> cols = ParseCsvLine(line);
                if (cols.Count < 2) continue;

                string display = cols[0].Trim();
                string reading = cols[1].Trim();
                if (string.IsNullOrEmpty(display) || string.IsNullOrEmpty(reading)) continue;

                entries.Add(new DictionaryEntry
                {
                    Display  = display,
                    Reading  = reading,
                    Remarks  = cols.Count > 2 ? cols[2].Trim() : string.Empty,
                    Priority = cols.Count > 3 && int.TryParse(cols[3].Trim(), out int p) ? p : 50
                });
            }

            Logger.Info($"CSV インポート: {entries.Count}件 ← {filePath}");
            return entries;
        }

        // ----------------------------------------------------------------
        // エクスポート
        // ----------------------------------------------------------------

        /// <summary>
        /// 辞書エントリのリストを CSV ファイルに書き出す。
        /// </summary>
        public static void Export(string filePath, IEnumerable<DictionaryEntry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("表記,読み,備考,優先順位");
            int count = 0;
            foreach (var e in entries)
            {
                sb.AppendLine(
                    $"{Escape(e.Display)},{Escape(e.Reading)},{Escape(e.Remarks)},{e.Priority}");
                count++;
            }
            File.WriteAllText(filePath, sb.ToString(), CsvEncoding);
            Logger.Info($"CSV エクスポート: {count}件 → {filePath}");
        }

        // ----------------------------------------------------------------
        // ヘルパー
        // ----------------------------------------------------------------

        private static string Escape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            int i = 0;
            while (i <= line.Length)
            {
                if (i == line.Length)
                {
                    result.Add(string.Empty);
                    break;
                }
                if (line[i] == '"')
                {
                    i++; // 開始クォートをスキップ
                    var sb = new StringBuilder();
                    while (i < line.Length)
                    {
                        if (line[i] == '"')
                        {
                            if (i + 1 < line.Length && line[i + 1] == '"')
                            {
                                sb.Append('"');
                                i += 2;
                            }
                            else
                            {
                                i++; // 終端クォートをスキップ
                                break;
                            }
                        }
                        else
                        {
                            sb.Append(line[i++]);
                        }
                    }
                    result.Add(sb.ToString());
                    if (i < line.Length && line[i] == ',') i++;
                }
                else
                {
                    int start = i;
                    while (i < line.Length && line[i] != ',') i++;
                    result.Add(line.Substring(start, i - start));
                    if (i < line.Length) i++; // カンマをスキップ
                }
            }
            return result;
        }

        /// <summary>ファイル先頭バイトからエンコードを推測する。</summary>
        private static Encoding DetectEncoding(string filePath)
        {
            // BOM チェック（ストリームを独立して開いてすぐ閉じる）
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] bom = new byte[3];
                if (fs.Read(bom, 0, 3) >= 3
                    && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                    return Encoding.UTF8; // BOM 付き UTF-8
            }

            // BOM なし: UTF-8 として厳格に読めるか試みる
            try
            {
                File.ReadAllText(filePath, new UTF8Encoding(false, throwOnInvalidBytes: true));
                return new UTF8Encoding(false);
            }
            catch (DecoderFallbackException)
            {
                // UTF-8 として不正なバイト列 → Shift_JIS とみなす
                return Encoding.GetEncoding("shift_jis");
            }
            // IOException 等は呼び出し元へ伝播させる
        }
    }
}
