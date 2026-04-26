using System.Collections.Generic;

namespace TxtToVoice.Services
{
    /// <summary>
    /// Aho-Corasick 多パターン文字列検索オートマトン。
    ///
    /// Build()  — パターン集合から O(Σ|p|) でオートマトンを構築する。
    /// Search() — テキストを O(n + 出力数) の 1 パスで走査し、全マッチを返す。
    ///
    /// 戻り値の PatternIndex は Build() に渡した patterns 配列のインデックスに対応する。
    /// </summary>
    internal sealed class AhoCorasick
    {
        private readonly Dictionary<char, int>[] _goto;
        private readonly int[] _fail;
        private readonly int[]?[] _output;
        private readonly int[] _patternLengths;

        private AhoCorasick(
            Dictionary<char, int>[] @goto,
            int[] fail,
            int[]?[] output,
            int[] patternLengths)
        {
            _goto = @goto;
            _fail = fail;
            _output = output;
            _patternLengths = patternLengths;
        }

        /// <summary>パターン配列からオートマトンを構築する。</summary>
        public static AhoCorasick Build(string[] patterns)
        {
            int maxStates = 1;
            foreach (var p in patterns)
                maxStates += p.Length;

            var goto_ = new Dictionary<char, int>[maxStates];
            var rawOutput = new List<int>?[maxStates];
            for (int i = 0; i < maxStates; i++)
                goto_[i] = new Dictionary<char, int>();

            int stateCount = 1; // 状態 0 = ルート

            // フェーズ1: トライ木構築
            for (int pi = 0; pi < patterns.Length; pi++)
            {
                string pat = patterns[pi];
                if (string.IsNullOrEmpty(pat)) continue;

                int cur = 0;
                foreach (char c in pat)
                {
                    if (!goto_[cur].TryGetValue(c, out int next))
                    {
                        next = stateCount++;
                        goto_[cur][c] = next;
                    }
                    cur = next;
                }
                (rawOutput[cur] ??= new List<int>()).Add(pi);
            }

            // フェーズ2: 失敗リンク構築（BFS）
            var fail = new int[stateCount];
            var queue = new Queue<int>();

            foreach (int s in goto_[0].Values)
            {
                fail[s] = 0;
                queue.Enqueue(s);
            }

            while (queue.Count > 0)
            {
                int r = queue.Dequeue();
                foreach (var (c, s) in goto_[r])
                {
                    queue.Enqueue(s);

                    // fail[s] = delta*(fail[r], c)
                    int f = fail[r];
                    while (f != 0 && !goto_[f].ContainsKey(c))
                        f = fail[f];
                    fail[s] = goto_[f].TryGetValue(c, out int fs) && fs != s ? fs : 0;

                    // 失敗リンク先の出力をマージ（辞書式サフィックス包含）
                    if (rawOutput[fail[s]] != null)
                    {
                        rawOutput[s] ??= new List<int>();
                        rawOutput[s]!.AddRange(rawOutput[fail[s]]!);
                    }
                }
            }

            // List → 配列に変換してメモリアクセスを効率化
            var output = new int[]?[stateCount];
            for (int i = 0; i < stateCount; i++)
                output[i] = rawOutput[i]?.ToArray();

            var gotoTrimmed = new Dictionary<char, int>[stateCount];
            for (int i = 0; i < stateCount; i++)
                gotoTrimmed[i] = goto_[i];

            var patLengths = new int[patterns.Length];
            for (int i = 0; i < patterns.Length; i++)
                patLengths[i] = patterns[i].Length;

            return new AhoCorasick(gotoTrimmed, fail, output, patLengths);
        }

        /// <summary>
        /// テキストを 1 パスで走査し、全マッチを (Start, PatternIndex) で返す。
        /// 重複・重複位置のマッチを含む全候補を返すため、呼び出し元で優先順位選択を行う。
        /// </summary>
        public IEnumerable<(int Start, int PatternIndex)> Search(string text)
        {
            int cur = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                while (cur != 0 && !_goto[cur].ContainsKey(c))
                    cur = _fail[cur];

                if (_goto[cur].TryGetValue(c, out int next))
                    cur = next;

                if (_output[cur] != null)
                {
                    foreach (int pi in _output[cur]!)
                        yield return (i - _patternLengths[pi] + 1, pi);
                }
            }
        }
    }
}
