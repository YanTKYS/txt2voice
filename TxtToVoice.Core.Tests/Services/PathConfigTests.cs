using System;
using System.IO;
using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// PathConfig の保存先パスロジックを検証するテスト。
    ///
    /// テスト環境には portable.flag が存在しないため、通常モード（%LOCALAPPDATA%）で動作する。
    /// ポータブルモード・フォールバックのロジックはテスト環境で再現できないため、
    /// 通常モードの出力値を通じて設定の正確性を確認する。
    /// </summary>
    public class PathConfigTests
    {
        // ================================================================
        // 通常モード（テスト環境での動作確認）
        // ================================================================

        [Fact]
        public void 通常モードでは_IsPortable_は_false()
        {
            // テスト環境に portable.flag は存在しない
            Assert.False(PathConfig.IsPortable);
        }

        [Fact]
        public void 通常モードでは_PortableFallbackApplied_は_false()
        {
            // portable.flag が存在しないため、フォールバックも発生しない
            Assert.False(PathConfig.PortableFallbackApplied);
        }

        [Fact]
        public void DataDirectory_は_LocalAppData_配下の_TxtToVoice_フォルダ()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string expected = Path.Combine(localAppData, "TxtToVoice");
            Assert.Equal(expected, PathConfig.DataDirectory);
        }

        [Fact]
        public void LogDirectory_は_DataDirectory_配下の_logs_フォルダ()
        {
            string expected = Path.Combine(PathConfig.DataDirectory, "logs");
            Assert.Equal(expected, PathConfig.LogDirectory);
        }

        [Fact]
        public void DictionaryPath_は_DataDirectory_配下の_dictionary_json()
        {
            string expected = Path.Combine(PathConfig.DataDirectory, "dictionary.json");
            Assert.Equal(expected, PathConfig.DictionaryPath);
        }

        [Fact]
        public void SettingsPath_は_DataDirectory_配下の_settings_json()
        {
            string expected = Path.Combine(PathConfig.DataDirectory, "settings.json");
            Assert.Equal(expected, PathConfig.SettingsPath);
        }

        // ================================================================
        // パス整合性
        // ================================================================

        [Fact]
        public void LogDirectory_は_DataDirectory_の_サブフォルダ()
        {
            Assert.StartsWith(PathConfig.DataDirectory, PathConfig.LogDirectory,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DictionaryPath_と_SettingsPath_は_同じフォルダ()
        {
            Assert.Equal(
                Path.GetDirectoryName(PathConfig.DictionaryPath),
                Path.GetDirectoryName(PathConfig.SettingsPath),
                StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void IsPortable_と_PortableFallbackApplied_は_同時にtrue_にならない()
        {
            // ポータブルモードが成功しているときはフォールバックは false
            // フォールバックが発生しているときはポータブルモードは false
            Assert.False(PathConfig.IsPortable && PathConfig.PortableFallbackApplied);
        }

        // ================================================================
        // TextRules パス（v0.5.9 追加・v0.6.2 テスト拡充）
        // ================================================================

        [Fact]
        public void TextRulesPath_はData配下のtext_rules_json()
        {
            Assert.EndsWith(Path.Combine("Data", "text_rules.json"), PathConfig.TextRulesPath);
        }

        [Fact]
        public void UserTextRulesPath_はDataDirectory下のtext_rules_json()
        {
            Assert.StartsWith(PathConfig.DataDirectory, PathConfig.UserTextRulesPath,
                StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("text_rules.json", PathConfig.UserTextRulesPath);
        }

        [Fact]
        public void UserTextRulesPath_とTextRulesPath_は異なるパス()
        {
            Assert.NotEqual(PathConfig.UserTextRulesPath, PathConfig.TextRulesPath,
                StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void EffectiveTextRulesPath_はTextRulesPathまたはUserTextRulesPathのいずれか()
        {
            string result = PathConfig.EffectiveTextRulesPath;
            bool isValid = string.Equals(result, PathConfig.TextRulesPath,  StringComparison.OrdinalIgnoreCase)
                        || string.Equals(result, PathConfig.UserTextRulesPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(isValid, $"想定外のパス: {result}");
        }

        [Fact]
        public void EffectiveTextRulesPath_ユーザーファイル不在時はTextRulesPathを返す()
        {
            if (File.Exists(PathConfig.UserTextRulesPath))
                return; // ユーザーファイルが存在する環境はスキップ

            Assert.Equal(PathConfig.TextRulesPath, PathConfig.EffectiveTextRulesPath,
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
