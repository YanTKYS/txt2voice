using System.IO;
using System;
using TxtToVoice.Models;
using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// DictionaryService の CSV 重複マージメソッド（HasDisplay / UpdateByDisplay）を検証するテスト（#27/#39）。
    /// </summary>
    public class DictionaryServiceMergeTests : IDisposable
    {
        private readonly string _tempPath;
        private readonly DictionaryService _svc;

        public DictionaryServiceMergeTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"TxtToVoiceMergeTest_{Guid.NewGuid()}.json");
            _svc = new DictionaryService(_tempPath);
        }

        public void Dispose()
        {
            try { File.Delete(_tempPath); } catch { /* 無視 */ }
        }

        private void Add(string display, string reading, int priority = 50) =>
            _svc.AddEntry(new DictionaryEntry { Display = display, Reading = reading, Priority = priority });

        // ================================================================
        // HasDisplay
        // ================================================================

        [Fact]
        public void HasDisplay_存在する表記はtrueを返す()
        {
            Add("市役所", "しやくしょ");
            Assert.True(_svc.HasDisplay("市役所"));
        }

        [Fact]
        public void HasDisplay_存在しない表記はfalseを返す()
        {
            Add("市役所", "しやくしょ");
            Assert.False(_svc.HasDisplay("図書館"));
        }

        [Fact]
        public void HasDisplay_辞書が空のときはfalseを返す()
        {
            Assert.False(_svc.HasDisplay("市役所"));
        }

        [Fact]
        public void HasDisplay_大文字小文字を区別する()
        {
            Add("ABC", "えびーしー");
            Assert.True(_svc.HasDisplay("ABC"));
            Assert.False(_svc.HasDisplay("abc"));
        }

        [Fact]
        public void HasDisplay_部分一致はfalseを返す()
        {
            Add("市役所", "しやくしょ");
            Assert.False(_svc.HasDisplay("市役"));
            Assert.False(_svc.HasDisplay("役所"));
        }

        // ================================================================
        // UpdateByDisplay
        // ================================================================

        [Fact]
        public void UpdateByDisplay_一致する表記の読みを上書きする()
        {
            Add("市役所", "しやくしょ");
            _svc.UpdateByDisplay(new DictionaryEntry { Display = "市役所", Reading = "シヤクショ" });
            Assert.Equal("シヤクショ", _svc.ApplyDictionary("市役所"));
        }

        [Fact]
        public void UpdateByDisplay_一致がない場合はエントリを追加しない()
        {
            Add("市役所", "しやくしょ");
            _svc.UpdateByDisplay(new DictionaryEntry { Display = "図書館", Reading = "としょかん" });
            Assert.Single(_svc.Entries);
            Assert.False(_svc.HasDisplay("図書館"));
        }

        [Fact]
        public void UpdateByDisplay_優先度も更新される()
        {
            Add("市役所", "しやくしょ", priority: 50);
            _svc.UpdateByDisplay(new DictionaryEntry { Display = "市役所", Reading = "シヤクショ", Priority = 80 });
            Assert.Equal(80, _svc.Entries[0].Priority);
        }

        [Fact]
        public void UpdateByDisplay後にキャッシュが無効化されて新しい読みが使われる()
        {
            Add("市役所", "しやくしょ");
            // 1 回 ApplyDictionary を呼んでキャッシュを構築
            Assert.Equal("しやくしょ", _svc.ApplyDictionary("市役所"));
            // 上書き後もキャッシュが正しく無効化される
            _svc.UpdateByDisplay(new DictionaryEntry { Display = "市役所", Reading = "シヤクショ" });
            Assert.Equal("シヤクショ", _svc.ApplyDictionary("市役所"));
        }
    }
}
