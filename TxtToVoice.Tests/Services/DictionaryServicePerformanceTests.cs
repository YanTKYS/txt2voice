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
    /// 許容値は CI 環境のスペック差を考慮して余裕を持たせてある。
    /// </summary>
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
        /// 500件辞書 × 10,000文字テキストで ApplyDictionary が 10 秒以内に完了すること。
        /// （ベースライン確認用。将来的にアルゴリズム改善後の比較に使う）
        /// </summary>
        [Fact]
        public void ApplyDictionary_500件辞書_10000文字を10秒以内に処理する()
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
            Assert.True(sw.ElapsedMilliseconds < 10_000,
                $"処理時間が上限を超えました: {sw.ElapsedMilliseconds} ms（上限: 10,000 ms）");
        }

        /// <summary>
        /// 100件辞書 × 50,000文字テキストで ApplyDictionaryForSpeech が 15 秒以内に完了すること。
        /// </summary>
        [Fact]
        public void ApplyDictionaryForSpeech_100件辞書_50000文字を15秒以内に処理する()
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
            Assert.True(sw.ElapsedMilliseconds < 15_000,
                $"処理時間が上限を超えました: {sw.ElapsedMilliseconds} ms（上限: 15,000 ms）");
        }
    }
}
