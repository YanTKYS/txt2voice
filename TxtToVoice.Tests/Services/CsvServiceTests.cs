using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TxtToVoice.Models;
using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// CsvService のインポート / エクスポートロジックを検証するテスト。
    /// ファイル I/O は一時ファイルを使用し、テスト後に自動削除する。
    /// </summary>
    public class CsvServiceTests : IDisposable
    {
        private readonly List<string> _tempFiles = new();

        public CsvServiceTests()
        {
            // テストランナーは App.OnStartup を経由しないため、ここで明示的に登録する
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public void Dispose()
        {
            foreach (var f in _tempFiles)
                try { File.Delete(f); } catch { /* 無視 */ }
        }

        private string WriteTempCsv(string content, Encoding? encoding = null)
        {
            string path = Path.GetTempFileName();
            _tempFiles.Add(path);
            File.WriteAllText(path, content, encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return path;
        }

        // ================================================================
        // インポート — 基本
        // ================================================================

        [Fact]
        public void Import_最小構成でエントリを読み込める()
        {
            string path = WriteTempCsv("表記,読み\n市役所,しやくしょ\n");
            var result = CsvService.Import(path);
            Assert.Single(result);
            Assert.Equal("市役所", result[0].Display);
            Assert.Equal("しやくしょ", result[0].Reading);
        }

        [Fact]
        public void Import_ヘッダー行を自動スキップする()
        {
            string path = WriteTempCsv("表記,読み,備考,優先順位\n市役所,しやくしょ,役所,80\n");
            var result = CsvService.Import(path);
            Assert.Single(result);
        }

        [Fact]
        public void Import_ヘッダーなしでも読み込める()
        {
            string path = WriteTempCsv("市役所,しやくしょ\n令和,れいわ\n");
            var result = CsvService.Import(path);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Import_全4列を読み込める()
        {
            string path = WriteTempCsv("市役所,しやくしょ,役職説明,80\n");
            var result = CsvService.Import(path);
            Assert.Single(result);
            Assert.Equal("市役所",   result[0].Display);
            Assert.Equal("しやくしょ", result[0].Reading);
            Assert.Equal("役職説明", result[0].Remarks);
            Assert.Equal(80,        result[0].Priority);
        }

        [Fact]
        public void Import_優先順位が省略された場合デフォルト50になる()
        {
            string path = WriteTempCsv("市役所,しやくしょ\n");
            var result = CsvService.Import(path);
            Assert.Equal(50, result[0].Priority);
        }

        [Fact]
        public void Import_備考が省略された場合は空文字になる()
        {
            string path = WriteTempCsv("市役所,しやくしょ\n");
            var result = CsvService.Import(path);
            Assert.Equal(string.Empty, result[0].Remarks);
        }

        // ================================================================
        // インポート — クォート処理
        // ================================================================

        [Fact]
        public void Import_カンマを含むフィールドをクォートで読み込める()
        {
            string path = WriteTempCsv("\"市,役所\",しやくしょ\n");
            var result = CsvService.Import(path);
            Assert.Single(result);
            Assert.Equal("市,役所", result[0].Display);
        }

        [Fact]
        public void Import_ダブルクォートのエスケープを処理できる()
        {
            // "" → " のエスケープ
            string path = WriteTempCsv("\"市\"\"役所\",しやくしょ\n");
            var result = CsvService.Import(path);
            Assert.Single(result);
            Assert.Equal("市\"役所", result[0].Display);
        }

        // ================================================================
        // インポート — 異常系
        // ================================================================

        [Fact]
        public void Import_列が1列以下の行はスキップする()
        {
            string path = WriteTempCsv("表記だけの行\n市役所,しやくしょ\n");
            var result = CsvService.Import(path);
            Assert.Single(result);
        }

        [Fact]
        public void Import_空行はスキップする()
        {
            string path = WriteTempCsv("市役所,しやくしょ\n\n令和,れいわ\n");
            var result = CsvService.Import(path);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Import_表記または読みが空の行はスキップする()
        {
            string path = WriteTempCsv(",読みなし\n表記なし,\n市役所,しやくしょ\n");
            var result = CsvService.Import(path);
            Assert.Single(result);
            Assert.Equal("市役所", result[0].Display);
        }

        [Fact]
        public void Import_優先順位が数字でない場合はデフォルト50になる()
        {
            string path = WriteTempCsv("市役所,しやくしょ,備考,高め\n");
            var result = CsvService.Import(path);
            Assert.Equal(50, result[0].Priority);
        }

        // ================================================================
        // インポート — エンコード
        // ================================================================

        [Fact]
        public void Import_BOM付きUTF8を読み込める()
        {
            string path = WriteTempCsv("市役所,しやくしょ\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            var result = CsvService.Import(path);
            Assert.Single(result);
            Assert.Equal("市役所", result[0].Display);
        }

        [Fact]
        public void Import_ShiftJISを読み込める()
        {
            string path = WriteTempCsv("市役所,しやくしょ\n", Encoding.GetEncoding("shift_jis"));
            var result = CsvService.Import(path);
            Assert.Single(result);
            Assert.Equal("市役所", result[0].Display);
        }

        // ================================================================
        // インポート — 複数行セル（RFC 4180 §2.6）
        // ================================================================

        [Fact]
        public void Import_改行を含むフィールドをクォートで読み込める()
        {
            // 引用符内の LF を含むフィールド
            string path = WriteTempCsv("\"市の花\nバラ\",しのはなばら\n");
            var result = CsvService.Import(path);
            Assert.Single(result);
            Assert.Equal("市の花\nバラ", result[0].Display);
            Assert.Equal("しのはなばら", result[0].Reading);
        }

        [Fact]
        public void Import_改行を含む複数エントリを正しく読み込める()
        {
            // 1つ目は複数行フィールド、2つ目は通常
            string path = WriteTempCsv("\"市の花\nバラ\",しのはなばら\n令和,れいわ\n");
            var result = CsvService.Import(path);
            Assert.Equal(2, result.Count);
            Assert.Equal("市の花\nバラ", result[0].Display);
            Assert.Equal("令和",         result[1].Display);
        }

        // ================================================================
        // エクスポート
        // ================================================================

        [Fact]
        public void Export_基本的なエントリを書き出せる()
        {
            string path = Path.GetTempFileName();
            _tempFiles.Add(path);
            var entries = new List<DictionaryEntry>
            {
                new() { Display = "市役所", Reading = "しやくしょ", Remarks = "役所", Priority = 80 }
            };
            CsvService.Export(path, entries);

            string content = File.ReadAllText(path, new UTF8Encoding(true));
            Assert.Contains("市役所", content);
            Assert.Contains("しやくしょ", content);
            Assert.Contains("80", content);
        }

        [Fact]
        public void Export_カンマを含むフィールドはクォートされる()
        {
            string path = Path.GetTempFileName();
            _tempFiles.Add(path);
            var entries = new List<DictionaryEntry>
            {
                new() { Display = "市,役所", Reading = "しやくしょ" }
            };
            CsvService.Export(path, entries);

            string content = File.ReadAllText(path);
            Assert.Contains("\"市,役所\"", content);
        }

        [Fact]
        public void Export_ダブルクォートを含むフィールドはエスケープされる()
        {
            string path = Path.GetTempFileName();
            _tempFiles.Add(path);
            var entries = new List<DictionaryEntry>
            {
                new() { Display = "市\"役所", Reading = "しやくしょ" }
            };
            CsvService.Export(path, entries);

            string content = File.ReadAllText(path);
            Assert.Contains("\"市\"\"役所\"", content);
        }

        // ================================================================
        // ラウンドトリップ
        // ================================================================

        [Fact]
        public void ExportImport_ラウンドトリップで同一データが復元される()
        {
            string path = Path.GetTempFileName();
            _tempFiles.Add(path);
            var original = new List<DictionaryEntry>
            {
                new() { Display = "市役所",   Reading = "しやくしょ", Remarks = "役所",  Priority = 80 },
                new() { Display = "令和",     Reading = "れいわ",     Remarks = string.Empty, Priority = 50 },
                new() { Display = "A,B\"C",  Reading = "エービーシー", Remarks = string.Empty, Priority = 50 }
            };
            CsvService.Export(path, original);
            var restored = CsvService.Import(path);

            Assert.Equal(original.Count, restored.Count);
            for (int i = 0; i < original.Count; i++)
            {
                Assert.Equal(original[i].Display,  restored[i].Display);
                Assert.Equal(original[i].Reading,  restored[i].Reading);
                Assert.Equal(original[i].Remarks,  restored[i].Remarks);
                Assert.Equal(original[i].Priority, restored[i].Priority);
            }
        }
    }
}
