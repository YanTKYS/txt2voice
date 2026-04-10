using System.IO;
using TxtToVoice.Models;
using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// DictionaryService のロジック（テキスト置換・CRUD）を検証するテスト。
    /// ファイル I/O は一時ディレクトリを使用し、テスト後に自動削除する。
    /// </summary>
    public class DictionaryServiceTests : IDisposable
    {
        private readonly string _tempPath;
        private readonly DictionaryService _svc;

        public DictionaryServiceTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"TxtToVoiceTest_{Guid.NewGuid()}.json");
            _svc = new DictionaryService(_tempPath);
        }

        public void Dispose()
        {
            try { File.Delete(_tempPath); } catch { /* 無視 */ }
        }

        // ----------------------------------------------------------------
        // ヘルパー
        // ----------------------------------------------------------------

        private void Add(string display, string reading, int priority = 50) =>
            _svc.AddEntry(new DictionaryEntry { Display = display, Reading = reading, Priority = priority });

        // ================================================================
        // ApplyDictionary — 基本置換
        // ================================================================

        [Fact]
        public void ApplyDictionary_空テキストはそのまま返す()
        {
            Add("市役所", "しやくしょ");
            Assert.Equal("", _svc.ApplyDictionary(""));
        }

        [Fact]
        public void ApplyDictionary_辞書にない語句は変換しない()
        {
            Add("市役所", "しやくしょ");
            Assert.Equal("公民館へようこそ", _svc.ApplyDictionary("公民館へようこそ"));
        }

        [Fact]
        public void ApplyDictionary_辞書語句を読みに変換する()
        {
            Add("市役所", "しやくしょ");
            Assert.Equal("しやくしょへお越しください", _svc.ApplyDictionary("市役所へお越しください"));
        }

        [Fact]
        public void ApplyDictionary_複数の語句を変換する()
        {
            Add("市役所", "しやくしょ");
            Add("令和", "れいわ");
            Assert.Equal("れいわ7年にしやくしょが移転", _svc.ApplyDictionary("令和7年に市役所が移転"));
        }

        // ================================================================
        // ApplyDictionary — 長い語句優先（longest-match）
        // ================================================================

        [Fact]
        public void ApplyDictionary_長い語句を短い語句より優先する()
        {
            Add("市",    "いち");
            Add("市役所", "しやくしょ");  // こちらが優先されるべき

            Assert.Equal("しやくしょ", _svc.ApplyDictionary("市役所"));
        }

        [Fact]
        public void ApplyDictionary_長い語句が先にマッチした後は短い語句と重複しない()
        {
            Add("市役所", "しやくしょ");
            Add("市",    "いち");

            // 「市役所」がまるごと変換され、残った「役所」には「市」はヒットしない
            Assert.Equal("しやくしょへどうぞ", _svc.ApplyDictionary("市役所へどうぞ"));
        }

        // ================================================================
        // ApplyDictionary — 優先度（同じ文字数）
        // ================================================================

        [Fact]
        public void ApplyDictionary_同じ長さなら優先度の高い方を使う()
        {
            // 同じ表記・同じ長さのエントリが2つあるケースはないが、
            // 異なる表記で同じ長さのエントリが異なる位置に存在する場合を検証
            Add("AB", "エービー", priority: 10);
            Add("CD", "シーディー", priority: 90);

            Assert.Equal("エービーシーディー", _svc.ApplyDictionary("ABCD"));
        }

        // ================================================================
        // ApplyDictionary — 二重置換防止
        // ================================================================

        [Fact]
        public void ApplyDictionary_置換済み範囲を再置換しない()
        {
            Add("しやくしょ", "再変換");   // 読み仮名を再度変換しようとするエントリ
            Add("市役所",    "しやくしょ");

            // 「市役所」→「しやくしょ」に変換後、「しやくしょ」が再変換されてはならない
            Assert.Equal("しやくしょ", _svc.ApplyDictionary("市役所"));
        }

        // ================================================================
        // ApplyDictionary — 複数回出現
        // ================================================================

        [Fact]
        public void ApplyDictionary_同じ語句が複数回出現しても全て変換する()
        {
            Add("市役所", "しやくしょ");
            Assert.Equal("しやくしょとしやくしょ", _svc.ApplyDictionary("市役所と市役所"));
        }

        // ================================================================
        // ApplyDictionaryWithAnnotation
        // ================================================================

        [Fact]
        public void ApplyDictionaryWithAnnotation_変換箇所をアノテーション形式で示す()
        {
            Add("市役所", "しやくしょ");
            string result = _svc.ApplyDictionaryWithAnnotation("市役所へどうぞ");
            Assert.Equal("【市役所→しやくしょ】へどうぞ", result);
        }

        [Fact]
        public void ApplyDictionaryWithAnnotation_変換なしならそのまま返す()
        {
            string result = _svc.ApplyDictionaryWithAnnotation("変換対象なし");
            Assert.Equal("変換対象なし", result);
        }

        // ================================================================
        // CRUD
        // ================================================================

        [Fact]
        public void AddEntry_エントリを追加できる()
        {
            Add("市役所", "しやくしょ");
            Assert.Single(_svc.Entries);
            Assert.Equal("市役所", _svc.Entries[0].Display);
        }

        [Fact]
        public void RemoveEntry_指定インデックスのエントリを削除できる()
        {
            Add("市役所", "しやくしょ");
            Add("令和",   "れいわ");

            _svc.RemoveEntry(0);

            Assert.Single(_svc.Entries);
            Assert.Equal("令和", _svc.Entries[0].Display);
        }

        [Fact]
        public void RemoveEntry_範囲外インデックスは何もしない()
        {
            Add("市役所", "しやくしょ");
            _svc.RemoveEntry(99); // 例外を出さないこと
            Assert.Single(_svc.Entries);
        }

        [Fact]
        public void UpdateEntry_指定インデックスのエントリを更新できる()
        {
            Add("市役所", "しやくしょ");
            _svc.UpdateEntry(0, new DictionaryEntry { Display = "市役所", Reading = "シヤクショ" });
            Assert.Equal("シヤクショ", _svc.Entries[0].Reading);
        }

        [Fact]
        public void ReplaceAll_全エントリを置き換える()
        {
            Add("市役所", "しやくしょ");
            Add("令和",   "れいわ");

            _svc.ReplaceAll(new[]
            {
                new DictionaryEntry { Display = "新語", Reading = "しんご" }
            });

            Assert.Single(_svc.Entries);
            Assert.Equal("新語", _svc.Entries[0].Display);
        }

        // ================================================================
        // 永続化（Save / Load）
        // ================================================================

        [Fact]
        public void SaveLoad_エントリを保存して読み込める()
        {
            Add("市役所", "しやくしょ");
            _svc.Save();

            var svc2 = new DictionaryService(_tempPath);
            svc2.Load();

            Assert.Single(svc2.Entries);
            Assert.Equal("市役所", svc2.Entries[0].Display);
            Assert.Equal("しやくしょ", svc2.Entries[0].Reading);
        }

        [Fact]
        public void Load_ファイルが存在しない場合は空リストになる()
        {
            // _tempPath はまだ存在しない（Save していない）
            _svc.Load();
            Assert.Empty(_svc.Entries);
        }
    }
}
