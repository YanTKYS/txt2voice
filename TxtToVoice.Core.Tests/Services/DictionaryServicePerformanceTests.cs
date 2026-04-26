using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using TxtToVoice.Models;
using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// DictionaryService のパフォーマンス回帰テスト。
    /// 大規模辞書 × 長文テキストで処理時間が許容値以内であることを確認する。
    ///
    /// [Trait("Category", "Performance")] が付いているため、通常 CI では除外できる:
    ///   dotnet test --filter "Category!=Performance"
    ///
    /// 閾値（v0.5.7 改訂 — Aho-Corasick 導入後の実測ベースライン）:
    ///   v0.5.4 で O(n×m) → AC O(n+m) に刷新済み。実測では < 10ms が典型。
    ///   CI ランナーのばらつき（Azure VM、コンテナ起動オーバーヘッド等）を
    ///   考慮し実測値の ~100 倍の余裕係数を設けた:
    ///     500件 × 10,000文字 → 1,000ms（旧: 30,000ms）
    ///     100件 × 50,000文字 → 2,000ms（旧: 45,000ms）
    /// </summary>
    [Trait("Category", "Performance")]
    public class DictionaryServicePerformanceTests : IDisposable
    {
        private readonly string _tempPath;
        private readonly DictionaryService _svc;

        public DictionaryServicePerformanceTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"TxtToVoicePerfTest_{Guid.NewGuid()}.json");
            _svc = new DictionaryService(_tempPath);
        }

        public void Dispose()
        {
            try { File.Delete(_tempPath); } catch { /* 無視 */ }
        }

        // ================================================================
        // ベースライン計測
        // ================================================================

        /// <summary>
        /// 500件辞書 × 10,000文字テキストで ApplyDictionary が 1 秒以内に完了すること。
        /// AC 導入後の実測は &lt; 10ms 典型。閾値 1,000ms は CI ランナーばらつきを考慮した余裕値。
        /// </summary>
        [Fact]
        public void ApplyDictionary_500件辞書_10000文字を1秒以内に処理する()
        {
            // 500件の辞書エントリを登録
            for (int i = 0; i < 500; i++)
                _svc.AddEntry(new DictionaryEntry
                {
                    Display  = $"語句{i:D3}",
                    Reading  = $"ごく{i:D3}",
                    Priority = 50
                });

            // 10,000文字のテキスト（辞書に存在する語句を含む）
            var sb = new StringBuilder();
            int idx = 0;
            while (sb.Length < 10_000)
            {
                sb.Append($"語句{idx % 500:D3}の次は普通のテキストが続きます。");
                idx++;
            }
            string text = sb.ToString(0, Math.Min(sb.Length, 10_000));

            var sw = Stopwatch.StartNew();
            string result = _svc.ApplyDictionary(text);
            sw.Stop();

            Assert.NotEmpty(result);
            Assert.True(sw.ElapsedMilliseconds < 1_000,
                $"処理時間が上限を超えました: {sw.ElapsedMilliseconds} ms（上限: 1,000 ms）");
        }

        /// <summary>
        /// 100件辞書 × 50,000文字テキストで ApplyDictionaryForSpeech が 2 秒以内に完了すること。
        /// AC 導入後の実測は &lt; 5ms 典型。閾値 2,000ms は CI ランナーばらつきを考慮した余裕値。
        /// </summary>
        [Fact]
        public void ApplyDictionaryForSpeech_100件辞書_50000文字を2秒以内に処理する()
        {
            for (int i = 0; i < 100; i++)
                _svc.AddEntry(new DictionaryEntry
                {
                    Display  = $"単語{i:D2}",
                    Reading  = $"たんご{i:D2}",
                    Priority = 50
                });

            var sb = new StringBuilder();
            int idx = 0;
            while (sb.Length < 50_000)
            {
                sb.Append($"単語{idx % 100:D2}を使った文章です。それ以外のテキストが続きます。");
                idx++;
            }
            string text = sb.ToString(0, Math.Min(sb.Length, 50_000));

            var sw = Stopwatch.StartNew();
            var (speechText, map) = _svc.ApplyDictionaryForSpeech(text);
            sw.Stop();

            Assert.NotEmpty(speechText);
            Assert.NotNull(map);
            Assert.True(sw.ElapsedMilliseconds < 2_000,
                $"処理時間が上限を超えました: {sw.ElapsedMilliseconds} ms（上限: 2,000 ms）");
        }
    }
}
