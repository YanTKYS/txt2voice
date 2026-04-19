using System.IO;
using System;
using TxtToVoice.Models;
using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// DictionaryService のソートキャッシュ無効化を検証するテスト（#26/#39）。
    /// _sortedCache は private のため ApplyDictionary の結果を通じて間接的に検証する。
    /// </summary>
    public class DictionaryServiceCacheTests : IDisposable
    {
        private readonly string _tempPath;
        private readonly DictionaryService _svc;

        public DictionaryServiceCacheTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"TxtToVoiceCacheTest_{Guid.NewGuid()}.json");
            _svc = new DictionaryService(_tempPath);
        }

        public void Dispose()
        {
            try { File.Delete(_tempPath); } catch { /* 無視 */ }
        }

        private void Add(string display, string reading, int priority = 50) =>
            _svc.AddEntry(new DictionaryEntry { Display = display, Reading = reading, Priority = priority });

        // ================================================================
        // AddEntry 後にキャッシュが無効化される
        // ================================================================

        [Fact]
        public void AddEntry後に新しいエントリが置換に反映される()
        {
            Add("市役所", "しやくしょ");
            Assert.Equal("しやくしょへ行く", _svc.ApplyDictionary("市役所へ行く"));

            _svc.AddEntry(new DictionaryEntry { Display = "行く", Reading = "いく" });
            Assert.Equal("しやくしょへいく", _svc.ApplyDictionary("市役所へ行く"));
        }

        // ================================================================
        // UpdateEntry 後にキャッシュが無効化される
        // ================================================================

        [Fact]
        public void UpdateEntry後に変更が置換に反映される()
        {
            Add("市役所", "しやくしょ");
            Assert.Equal("しやくしょ", _svc.ApplyDictionary("市役所"));

            _svc.UpdateEntry(0, new DictionaryEntry { Display = "市役所", Reading = "シヤクショ" });
            Assert.Equal("シヤクショ", _svc.ApplyDictionary("市役所"));
        }

        // ================================================================
        // RemoveEntry 後にキャッシュが無効化される
        // ================================================================

        [Fact]
        public void RemoveEntry後に削除されたエントリが置換されなくなる()
        {
            Add("市役所", "しやくしょ");
            Assert.Equal("しやくしょ", _svc.ApplyDictionary("市役所"));

            _svc.RemoveEntry(0);
            Assert.Equal("市役所", _svc.ApplyDictionary("市役所"));
        }

        // ================================================================
        // ReplaceAll 後にキャッシュが無効化される
        // ================================================================

        [Fact]
        public void ReplaceAll後に新しいリストで置換される()
        {
            Add("市役所", "しやくしょ");
            Assert.Equal("しやくしょ", _svc.ApplyDictionary("市役所"));

            _svc.ReplaceAll(new[]
            {
                new DictionaryEntry { Display = "図書館", Reading = "としょかん" }
            });
            // 古いエントリは消え、新しいエントリが有効になる
            Assert.Equal("市役所", _svc.ApplyDictionary("市役所"));
            Assert.Equal("としょかん", _svc.ApplyDictionary("図書館"));
        }

        // ================================================================
        // Load 後にキャッシュが無効化される
        // ================================================================

        [Fact]
        public void Load後にファイルの内容で置換される()
        {
            Add("市役所", "しやくしょ");
            _svc.Save();

            var svc2 = new DictionaryService(_tempPath);
            svc2.Load();
            Assert.Equal("しやくしょ", svc2.ApplyDictionary("市役所"));
        }

        // ================================================================
        // 同一リストで複数回呼んでも正しい結果になる（キャッシュ再利用の確認）
        // ================================================================

        [Fact]
        public void キャッシュ再利用時も同じ結果を返す()
        {
            Add("市役所", "しやくしょ");
            string r1 = _svc.ApplyDictionary("市役所へ行く");
            string r2 = _svc.ApplyDictionary("市役所へ行く");
            Assert.Equal(r1, r2);
        }
    }
}
