using System;
using System.IO;
using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    public class TextRuleLoaderTests
    {
        // ================================================================
        // ファイル不在・空
        // ================================================================

        [Fact]
        public void Load_ファイルが存在しない場合_空リストを返す()
        {
            var result = TextRuleLoader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
            Assert.Empty(result);
        }

        [Fact]
        public void Load_空配列JSON_空リストを返す()
        {
            string path = WriteTempJson("[]");
            try
            {
                var result = TextRuleLoader.Load(path);
                Assert.Empty(result);
            }
            finally { File.Delete(path); }
        }

        // ================================================================
        // 有効ルール
        // ================================================================

        [Fact]
        public void Load_有効ルール1件_Applyが機能する()
        {
            string path = WriteTempJson(
                @"[{""pattern"":""CPU"",""replacement"":""シーピーユー"",""description"":"""",""enabled"":true}]");
            try
            {
                var rules = TextRuleLoader.Load(path);
                Assert.Single(rules);
                Assert.Equal("シーピーユー搭載", TextPreprocessor.Apply("CPU搭載", rules));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Load_複数の有効ルール_順番に適用される()
        {
            string path = WriteTempJson(
                "[" +
                @"{""pattern"":""AI"",""replacement"":""エーアイ"",""description"":"""",""enabled"":true}," +
                @"{""pattern"":""SNS"",""replacement"":""エスエヌエス"",""description"":"""",""enabled"":true}" +
                "]");
            try
            {
                var rules = TextRuleLoader.Load(path);
                Assert.Equal(2, rules.Count);
                Assert.Equal("エーアイとエスエヌエス", TextPreprocessor.Apply("AIとSNS", rules));
            }
            finally { File.Delete(path); }
        }

        // ================================================================
        // disabled フィルタリング
        // ================================================================

        [Fact]
        public void Load_disabled_ルールはスキップされる()
        {
            string path = WriteTempJson(
                @"[{""pattern"":""CPU"",""replacement"":""シーピーユー"",""description"":"""",""enabled"":false}]");
            try
            {
                var rules = TextRuleLoader.Load(path);
                Assert.Empty(rules);
                Assert.Equal("CPU搭載", TextPreprocessor.Apply("CPU搭載", rules));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Load_有効と無効の混在_有効のみ返す()
        {
            string path = WriteTempJson(
                "[" +
                @"{""pattern"":""AI"",""replacement"":""エーアイ"",""description"":"""",""enabled"":true}," +
                @"{""pattern"":""CPU"",""replacement"":""シーピーユー"",""description"":"""",""enabled"":false}" +
                "]");
            try
            {
                var rules = TextRuleLoader.Load(path);
                Assert.Single(rules);
                Assert.Equal("エーアイとCPU", TextPreprocessor.Apply("AIとCPU", rules));
            }
            finally { File.Delete(path); }
        }

        // ================================================================
        // エラー耐性
        // ================================================================

        [Fact]
        public void Load_不正JSON_空リストを返す()
        {
            string path = WriteTempJson("{ invalid json }");
            try
            {
                var result = TextRuleLoader.Load(path);
                Assert.Empty(result);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Load_無効な正規表現_そのルールをスキップして続行する()
        {
            string path = WriteTempJson(
                "[" +
                @"{""pattern"":""[invalid"",""replacement"":""X"",""description"":"""",""enabled"":true}," +
                @"{""pattern"":""AI"",""replacement"":""エーアイ"",""description"":"""",""enabled"":true}" +
                "]");
            try
            {
                var rules = TextRuleLoader.Load(path);
                Assert.Single(rules);
                Assert.Equal("エーアイ", TextPreprocessor.Apply("AI", rules));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Load_空パターン_そのルールをスキップする()
        {
            string path = WriteTempJson(
                @"[{""pattern"":"""",""replacement"":""X"",""description"":"""",""enabled"":true}]");
            try
            {
                var result = TextRuleLoader.Load(path);
                Assert.Empty(result);
            }
            finally { File.Delete(path); }
        }

        // ================================================================
        // TextPreprocessor.Apply の rules パラメータ
        // ================================================================

        [Fact]
        public void Apply_rulesNull_既存フェーズのみ動作する()
        {
            Assert.Equal("いちがつ", TextPreprocessor.Apply("1月", null));
        }

        [Fact]
        public void Apply_rules省略_既存フェーズのみ動作する()
        {
            Assert.Equal("いちがつ", TextPreprocessor.Apply("1月"));
        }

        // ================================================================
        // ヘルパー
        // ================================================================

        private static string WriteTempJson(string content)
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
            return path;
        }
    }
}
