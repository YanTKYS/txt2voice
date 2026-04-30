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

        // FindReplacements で使うキャッシュ。エントリ変更時に InvalidateCache() で両方クリアする。
        private List<DictionaryEntry>? _sortedCache;
        private AhoCorasick? _acAutomaton;

        public IReadOnlyList<DictionaryEntry> Entries => _entries.AsReadOnly();

        public DictionaryService(string dictionaryPath)
        {
            _persistence = new JsonPersistenceService(dictionaryPath);
        }

        // ----------------------------------------------------------------
        // CRUD
        // ----------------------------------------------------------------

        public void Load()
        {
            _entries = _persistence.Load();
            InvalidateCache();
            Logger.Info($"辞書読み込み完了: {_entries.Count}件");
        }

        public void Save() => _persistence.Save(_entries);

        /// <summary>
        /// DataGrid インライン編集など、エントリのプロパティを直接変更した後にキャッシュを無効化する。
        /// 次回の ApplyDictionary 呼び出し時に Aho-Corasick を再構築する。
        /// </summary>
        public void Invalidate() => InvalidateCache();

        public void AddEntry(DictionaryEntry entry)
        {
            _entries.Add(entry);
            InvalidateCache();
            Logger.Info($"辞書追加: 「{entry.Display}」→「{entry.Reading}」");
        }

        public void UpdateEntry(int index, DictionaryEntry entry)
        {
            if (index < 0 || index >= _entries.Count) return;
            _entries[index] = entry;
            InvalidateCache();
            Logger.Info($"辞書更新: index={index} 「{entry.Display}」→「{entry.Reading}」");
        }

        public void RemoveEntry(int index)
        {
            if (index < 0 || index >= _entries.Count) return;
            var removed = _entries[index];
            _entries.RemoveAt(index);
            InvalidateCache();
            Logger.Info($"辞書削除: 「{removed.Display}」");
        }

        /// <summary>エントリを <paramref name="fromIndex"/> から <paramref name="toIndex"/> へ移動する。</summary>
        public void MoveEntry(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _entries.Count) return;
            if (toIndex < 0 || toIndex >= _entries.Count) return;
            if (fromIndex == toIndex) return;
            var entry = _entries[fromIndex];
            _entries.RemoveAt(fromIndex);
            _entries.Insert(toIndex, entry);
            InvalidateCache();
            Logger.Info($"辞書移動: index {fromIndex} → {toIndex} 「{entry.Display}」");
        }

        public void ReplaceAll(IEnumerable<DictionaryEntry> entries)
        {
            _entries = entries.ToList();
            InvalidateCache();
        }

        // ----------------------------------------------------------------
        // CSV インポート向けユーティリティ
        // ----------------------------------------------------------------

        /// <summary>指定した表記を持つエントリが既に存在するかどうかを返す。</summary>
        public bool HasDisplay(string display)
            => _entries.Any(e => e.Display.Equals(display, StringComparison.Ordinal));

        /// <summary>
        /// 表記が一致する既存エントリを <paramref name="entry"/> で上書きする。
        /// 一致がない場合は何もしない（追加しない）。
        /// </summary>
        public void UpdateByDisplay(DictionaryEntry entry)
        {
            int idx = _entries.FindIndex(
                e => e.Display.Equals(entry.Display, StringComparison.Ordinal));
            if (idx < 0) return;
            _entries[idx] = entry;
            InvalidateCache();
            Logger.Info($"辞書上書き（表記一致）: 「{entry.Display}」→「{entry.Reading}」");
        }

        // ----------------------------------------------------------------
        // テキスト置換
        // ----------------------------------------------------------------

        /// <summary>text 中で entries の Display 値が何回出現するかを単純文字列検索で返す。</summary>
        public int CountOccurrences(string text, IEnumerable<DictionaryEntry> entries)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int total = 0;
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Display)) continue;
                int start = 0;
                while ((start = text.IndexOf(entry.Display, start, StringComparison.Ordinal)) >= 0)
                {
                    total++;
                    start += entry.Display.Length;
                }
            }
            return total;
        }

        /// <summary>
        /// テキストに辞書を適用し、読み上げ用テキストを返す。
        /// </summary>
        public string ApplyDictionary(string text)
            => ApplyDictionaryForSpeech(text).SpeechText;

        /// <summary>
        /// テキストに辞書を適用し、読み上げ用テキストと位置マップを返す。
        /// 位置マップは読み上げ進捗ハイライトに使用する。
        /// </summary>
        public (string SpeechText, SpeechPositionMap PositionMap) ApplyDictionaryForSpeech(string text)
        {
            if (string.IsNullOrEmpty(text))
                return (text, new SpeechPositionMap(new()));

            var replacements = FindReplacements(text);

            var segments = new List<(int, int, int, int)>();
            var sb = new StringBuilder(text.Length);
            int cursor = 0, speechCursor = 0;

            foreach (var (start, length, _, reading) in replacements)
            {
                if (start > cursor)
                {
                    int spanLen = start - cursor;
                    segments.Add((speechCursor, speechCursor + spanLen, cursor, cursor + spanLen));
                    sb.Append(text, cursor, spanLen);
                    speechCursor += spanLen;
                }
                segments.Add((speechCursor, speechCursor + reading.Length, start, start + length));
                sb.Append(reading);
                speechCursor += reading.Length;
                cursor = start + length;
            }

            if (cursor < text.Length)
            {
                int spanLen = text.Length - cursor;
                segments.Add((speechCursor, speechCursor + spanLen, cursor, cursor + spanLen));
                sb.Append(text, cursor, spanLen);
            }

            return (sb.ToString(), new SpeechPositionMap(segments));
        }

        /// <summary>
        /// 辞書適用のプレビュー用テキストを返す。
        /// 置換箇所を「【元表記→読み】」形式で示す。
        /// </summary>
        public string ApplyDictionaryWithAnnotation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var replacements = FindReplacements(text);
            var sb = new StringBuilder(text.Length);
            int cursor = 0;

            foreach (var (start, length, display, reading) in replacements)
            {
                if (start > cursor)
                    sb.Append(text, cursor, start - cursor);
                sb.Append($"【{display}→{reading}】");
                cursor = start + length;
            }
            if (cursor < text.Length)
                sb.Append(text, cursor, text.Length - cursor);

            return sb.ToString();
        }

        /// <summary>
        /// 辞書適用結果をセグメント列として返す。
        /// 各要素は (テキスト, 置換済みかどうか) のタプル。
        /// 置換済みセグメントのテキストは「【元表記→読み】」形式。
        /// </summary>
        public IReadOnlyList<(string Text, bool IsReplacement)> ApplyDictionaryWithAnnotationSegments(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<(string, bool)>();

            var replacements = FindReplacements(text);
            var segments = new List<(string Text, bool IsReplacement)>(replacements.Count * 2 + 1);
            int cursor = 0;

            foreach (var (start, length, display, reading) in replacements)
            {
                if (start > cursor)
                    segments.Add((text.Substring(cursor, start - cursor), false));
                segments.Add(($"【{display}→{reading}】", true));
                cursor = start + length;
            }
            if (cursor < text.Length)
                segments.Add((text.Substring(cursor), false));

            return segments;
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
            if (added > 0) InvalidateCache();
            Logger.Info($"サンプル辞書読み込み: {added}件追加");
            return added;
        }

        // ----------------------------------------------------------------
        // プライベートヘルパー
        // ----------------------------------------------------------------

        private void InvalidateCache()
        {
            _sortedCache = null;
            _acAutomaton = null;
        }

        /// <summary>
        /// 初回アクセス時に _sortedCache と _acAutomaton を一括構築する。
        /// </summary>
        private void EnsureCache()
        {
            if (_sortedCache != null) return;

            _sortedCache = _entries
                .Where(e => !string.IsNullOrEmpty(e.Display) && !string.IsNullOrEmpty(e.Reading))
                .OrderByDescending(e => e.Display.Length)
                .ThenByDescending(e => e.Priority)
                .ToList();

            var patterns = new string[_sortedCache.Count];
            for (int i = 0; i < _sortedCache.Count; i++)
                patterns[i] = _sortedCache[i].Display;
            _acAutomaton = AhoCorasick.Build(patterns);
        }

        /// <summary>
        /// Aho-Corasick で全マッチを収集し、長い語句優先・二重置換防止・位置昇順で返す。
        /// </summary>
        private List<(int Start, int Length, string Display, string Reading)> FindReplacements(string text)
        {
            EnsureCache();
            var sortedEntries = _sortedCache!;
            var automaton     = _acAutomaton!;

            // AC で全マッチ候補を収集
            var allMatches = new List<(int Start, int Length, int Priority, string Display, string Reading)>();
            foreach (var (start, pi) in automaton.Search(text))
            {
                var e = sortedEntries[pi];
                allMatches.Add((start, e.Display.Length, e.Priority, e.Display, e.Reading));
            }

            // 長さ降順 → 優先度降順 → 位置昇順 でソート
            allMatches.Sort((a, b) =>
            {
                int c = b.Length.CompareTo(a.Length);     if (c != 0) return c;
                    c = b.Priority.CompareTo(a.Priority); if (c != 0) return c;
                return a.Start.CompareTo(b.Start);
            });

            // 貪欲に非重複選択
            bool[] replaced = new bool[text.Length];
            var selected = new List<(int Start, int Length, string Display, string Reading)>(allMatches.Count);

            foreach (var (start, length, _, display, reading) in allMatches)
            {
                bool overlaps = false;
                for (int i = start; i < start + length; i++)
                {
                    if (replaced[i]) { overlaps = true; break; }
                }
                if (!overlaps)
                {
                    for (int i = start; i < start + length; i++)
                        replaced[i] = true;
                    selected.Add((start, length, display, reading));
                }
            }

            selected.Sort((a, b) => a.Start.CompareTo(b.Start));
            return selected;
        }
    }
}
