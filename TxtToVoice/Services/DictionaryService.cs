using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TxtToVoice.Models;

namespace TxtToVoice.Services
{
    /// <summary>
    /// 読み上げ辞書の管理と文字列置換を担当するサービス。
    ///
    /// 置換ルール：
    ///   1. 表記の長い語句を優先（長い語句優先マッチング）
    ///   2. 同じ長さの場合は Priority 値の大きい方を優先
    ///   3. 一度置換された範囲は再置換しない（二重置換防止）
    /// </summary>
    public class DictionaryService
    {
        private List<DictionaryEntry> _entries = new();
        private readonly JsonPersistenceService _persistence;

        public IReadOnlyList<DictionaryEntry> Entries => _entries.AsReadOnly();

        public DictionaryService(string dictionaryPath)
        {
            _persistence = new JsonPersistenceService(dictionaryPath);
        }

        // ----------------------------------------------------------------
        // CRUD
        // ----------------------------------------------------------------

        /// <summary>ファイルから辞書を読み込む。</summary>
        public void Load()
        {
            _entries = _persistence.Load();
            Logger.Info($"辞書読み込み完了: {_entries.Count}件");
        }

        /// <summary>ファイルに辞書を保存する。</summary>
        public void Save() => _persistence.Save(_entries);

        public void AddEntry(DictionaryEntry entry)
        {
            _entries.Add(entry);
            Logger.Info($"辞書追加: 「{entry.Display}」→「{entry.Reading}」");
        }

        public void UpdateEntry(int index, DictionaryEntry entry)
        {
            if (index < 0 || index >= _entries.Count) return;
            _entries[index] = entry;
            Logger.Info($"辞書更新: index={index} 「{entry.Display}」→「{entry.Reading}」");
        }

        public void RemoveEntry(int index)
        {
            if (index < 0 || index >= _entries.Count) return;
            var removed = _entries[index];
            _entries.RemoveAt(index);
            Logger.Info($"辞書削除: 「{removed.Display}」");
        }

        /// <summary>全エントリを指定リストで置き換える（CSV インポート用）</summary>
        public void ReplaceAll(IEnumerable<DictionaryEntry> entries)
        {
            _entries = entries.ToList();
        }

        // ----------------------------------------------------------------
        // テキスト置換
        // ----------------------------------------------------------------

        /// <summary>
        /// テキストに辞書を適用し、読み上げ用テキストを返す。
        /// 元テキストは変更しない。
        /// </summary>
        public string ApplyDictionary(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 長い表記を優先、同長は Priority 降順
            var sorted = _entries
                .Where(e => !string.IsNullOrEmpty(e.Display) && !string.IsNullOrEmpty(e.Reading))
                .OrderByDescending(e => e.Display.Length)
                .ThenByDescending(e => e.Priority)
                .ToList();

            // 置換済みの文字インデックスを管理
            bool[] replaced = new bool[text.Length];
            // (開始位置, 元テキスト長, 置換後テキスト)
            var replacements = new List<(int Start, int Length, string Reading)>();

            foreach (var entry in sorted)
            {
                int searchFrom = 0;
                while (searchFrom < text.Length)
                {
                    int pos = text.IndexOf(entry.Display, searchFrom, StringComparison.Ordinal);
                    if (pos < 0) break;

                    // 既に置換済み範囲と重複するか確認
                    bool overlaps = false;
                    for (int i = pos; i < pos + entry.Display.Length; i++)
                    {
                        if (replaced[i]) { overlaps = true; break; }
                    }

                    if (!overlaps)
                    {
                        for (int i = pos; i < pos + entry.Display.Length; i++)
                            replaced[i] = true;
                        replacements.Add((pos, entry.Display.Length, entry.Reading));
                    }

                    searchFrom = pos + 1;
                }
            }

            // 位置順にソートして結果文字列を構築
            replacements.Sort((a, b) => a.Start.CompareTo(b.Start));
            var sb = new StringBuilder(text.Length);
            int cursor = 0;
            foreach (var (start, length, reading) in replacements)
            {
                if (start > cursor)
                    sb.Append(text, cursor, start - cursor);
                sb.Append(reading);
                cursor = start + length;
            }
            if (cursor < text.Length)
                sb.Append(text, cursor, text.Length - cursor);

            return sb.ToString();
        }

        /// <summary>
        /// 辞書適用のプレビュー表示用テキストを返す。
        /// 置換された箇所を「【元表記→読み】」形式で示す。
        /// </summary>
        public string ApplyDictionaryWithAnnotation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var sorted = _entries
                .Where(e => !string.IsNullOrEmpty(e.Display) && !string.IsNullOrEmpty(e.Reading))
                .OrderByDescending(e => e.Display.Length)
                .ThenByDescending(e => e.Priority)
                .ToList();

            bool[] replaced = new bool[text.Length];
            var replacements = new List<(int Start, int Length, string Display, string Reading)>();

            foreach (var entry in sorted)
            {
                int searchFrom = 0;
                while (searchFrom < text.Length)
                {
                    int pos = text.IndexOf(entry.Display, searchFrom, StringComparison.Ordinal);
                    if (pos < 0) break;

                    bool overlaps = false;
                    for (int i = pos; i < pos + entry.Display.Length; i++)
                    {
                        if (replaced[i]) { overlaps = true; break; }
                    }

                    if (!overlaps)
                    {
                        for (int i = pos; i < pos + entry.Display.Length; i++)
                            replaced[i] = true;
                        replacements.Add((pos, entry.Display.Length, entry.Display, entry.Reading));
                    }

                    searchFrom = pos + 1;
                }
            }

            replacements.Sort((a, b) => a.Start.CompareTo(b.Start));
            var sb = new StringBuilder(text.Length);
            int cursor = 0;
            foreach (var (start, length, display, reading) in replacements)
            {
                if (start > cursor)
                    sb.Append(text, cursor, start - cursor);
                // 変換箇所を【元表記→読み】で明示
                sb.Append($"【{display}→{reading}】");
                cursor = start + length;
            }
            if (cursor < text.Length)
                sb.Append(text, cursor, text.Length - cursor);

            return sb.ToString();
        }

        /// <summary>
        /// サンプル辞書ファイルを読み込んで現在の辞書にマージする。
        /// </summary>
        public int LoadSampleDictionary(string samplePath)
        {
            if (!File.Exists(samplePath)) return 0;
            var sampleService = new JsonPersistenceService(samplePath);
            var samples = sampleService.Load();
            int added = 0;
            foreach (var sample in samples)
            {
                bool exists = _entries.Any(e =>
                    e.Display.Equals(sample.Display, StringComparison.Ordinal));
                if (!exists)
                {
                    _entries.Add(sample);
                    added++;
                }
            }
            Logger.Info($"サンプル辞書読み込み: {added}件追加");
            return added;
        }
    }
}
