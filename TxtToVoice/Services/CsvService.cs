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
    /// RFC 4180 に準じたクォート処理をサポートする（改行を含むフィールドも対応）。
    /// </summary>
    public static class CsvService
    {
        private static readonly Encoding CsvEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        static CsvService()
        {
            // .NET Core/5+ では Shift_JIS 等のコードページエンコードを使う前に登録が必要
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        // ----------------------------------------------------------------
        // インポート
        // ----------------------------------------------------------------

        /// <summary>
        /// CSV ファイルから辞書エントリのリストを読み込む。
        /// RFC 4180 §2.6 の引用符内改行を含むフィールドに対応。
        /// </summary>
        public static List<DictionaryEntry> Import(string filePath)
        {
            var entries = new List<DictionaryEntry>();

            // BOM 付き UTF-8 / UTF-8 / Shift_JIS を自動判別
            Encoding enc = DetectEncoding(filePath);

            bool isFirstRecord = true;
            using var reader = new StreamReader(filePath, enc);
            foreach (var cols in ParseCsvRecords(reader))
            {
                // ヘッダー行スキップ（1行目が「表記」で始まる場合）
                if (isFirstRecord)
                {
                    isFirstRecord = false;
                    if (cols.Count > 0 && cols[0].TrimStart().StartsWith("表記", StringComparison.Ordinal))
                        continue;
                }

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

        /// <summary>
        /// StreamReader から RFC 4180 準拠の CSV レコードを逐次解析して返す。
        /// 引用符内の改行（CRLF / LF）を含むフィールドに対応する。
        /// </summary>
        private static IEnumerable<List<string>> ParseCsvRecords(StreamReader reader)
        {
            var record = new List<string>();
            var field  = new StringBuilder();
            bool inQuotes = false;

            while (true)
            {
                int ch = reader.Read();

                if (ch == -1)
                {
                    // EOF: 最後のフィールド・レコードをフラッシュ
                    if (field.Length > 0 || record.Count > 0)
                    {
                        record.Add(field.ToString());
                        yield return record;
                    }
                    yield break;
                }

                char c = (char)ch;

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // "" → " (エスケープ) か、閉じクォートか判定
                        if (reader.Peek() == '"')
                        {
                            reader.Read(); // 2つ目の " を消費
                            field.Append('"');
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else if (c == '\r')
                    {
                        // CRLF を LF に正規化してフィールドに取り込む
                        if (reader.Peek() == '\n') reader.Read();
                        field.Append('\n');
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        record.Add(field.ToString());
                        field.Clear();
                    }
                    else if (c == '\r')
                    {
                        if (reader.Peek() == '\n') reader.Read();
                        record.Add(field.ToString());
                        field.Clear();
                        if (record.Count > 0)
                        {
                            yield return record;
                            record = new List<string>();
                        }
                    }
                    else if (c == '\n')
                    {
                        record.Add(field.ToString());
                        field.Clear();
                        if (record.Count > 0)
                        {
                            yield return record;
                            record = new List<string>();
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
            }
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
