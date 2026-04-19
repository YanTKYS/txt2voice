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
    }
}
