using System.Collections.Generic;
using TxtToVoice.Models;
using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// AppSettingsBuilder.Build() の保存ポリシー分岐を検証するテスト。
    /// UI コントロール非依存のため WPF ランタイムなしで実行できる。
    /// </summary>
    public class BuildAppSettingsTests
    {
        private static AppSettings Build(
            bool isExit                   = false,
            int rate                      = 0,
            int volume                    = 100,
            string? voiceName             = "TestVoice",
            string? voiceId               = "test-voice-id",
            bool ssmlEnabled              = false,
            bool showHighlight            = true,
            bool saveLastInputText        = true,
            bool saveRecentFiles          = true,
            bool clearSensitiveDataOnExit = false,
            bool deleteLogOnExit          = false,
            string speechEngineType       = "SystemSpeech",
            string lastText               = "テスト本文",
            IReadOnlyList<string>? recentFiles = null,
            int auditRetentionMonths      = 13,
            bool autoPreviewEnabled       = false,
            bool annotatedPreviewMode     = true,
            int ssmlPauseStrength         = 1,
            string saveFilePrefix         = "kouhou")
            => AppSettingsBuilder.Build(
                isExit, rate, volume, voiceName, voiceId,
                ssmlEnabled, showHighlight,
                saveLastInputText, saveRecentFiles,
                clearSensitiveDataOnExit, deleteLogOnExit,
                speechEngineType, lastText,
                recentFiles ?? new List<string> { "file1.txt", "file2.txt" },
                auditRetentionMonths, autoPreviewEnabled, annotatedPreviewMode,
                ssmlPauseStrength, saveFilePrefix);

        // ================================================================
        // 非終了時の挙動
        // ================================================================

        [Fact]
        public void 非終了時はLastInputTextを保存しない()
        {
            var s = Build(isExit: false, lastText: "保存しないはず");
            Assert.Equal(string.Empty, s.LastInputText);
        }

        [Fact]
        public void 非終了時はRecentFilesを保存する()
        {
            var s = Build(isExit: false, recentFiles: new List<string> { "a.txt" });
            Assert.Contains("a.txt", s.RecentFiles);
        }

        // ================================================================
        // 終了時の挙動（clearSensitiveDataOnExit = false）
        // ================================================================

        [Fact]
        public void 終了時_クリアなし_LastInputTextを保存する()
        {
            var s = Build(isExit: true, clearSensitiveDataOnExit: false, lastText: "保存する本文");
            Assert.Equal("保存する本文", s.LastInputText);
        }

        [Fact]
        public void 終了時_クリアなし_RecentFilesを保存する()
        {
            var s = Build(isExit: true, clearSensitiveDataOnExit: false,
                recentFiles: new List<string> { "a.txt" });
            Assert.Contains("a.txt", s.RecentFiles);
        }

        // ================================================================
        // 終了時の挙動（clearSensitiveDataOnExit = true）
        // ================================================================

        [Fact]
        public void 終了時_クリアあり_LastInputTextを空にする()
        {
            var s = Build(isExit: true, clearSensitiveDataOnExit: true, lastText: "消えるはず");
            Assert.Equal(string.Empty, s.LastInputText);
        }

        [Fact]
        public void 終了時_クリアあり_RecentFilesを空にする()
        {
            var s = Build(isExit: true, clearSensitiveDataOnExit: true,
                recentFiles: new List<string> { "a.txt" });
            Assert.Empty(s.RecentFiles);
        }

        // ================================================================
        // テキスト長上限
        // ================================================================

        [Fact]
        public void 終了時_テキストが上限を超えたら保存しない()
        {
            string longText = new('あ', AppSettingsBuilder.MaxPersistedInputTextLength + 1);
            var s = Build(isExit: true, clearSensitiveDataOnExit: false, lastText: longText);
            Assert.Equal(string.Empty, s.LastInputText);
        }

        [Fact]
        public void 終了時_テキストが上限以内なら保存する()
        {
            string text = new('あ', AppSettingsBuilder.MaxPersistedInputTextLength);
            var s = Build(isExit: true, clearSensitiveDataOnExit: false, lastText: text);
            Assert.Equal(text, s.LastInputText);
        }

        // ================================================================
        // saveLastInputText = false のとき
        // ================================================================

        [Fact]
        public void 終了時_saveLastInputTextがfalseならLastInputTextを保存しない()
        {
            var s = Build(isExit: true, saveLastInputText: false,
                clearSensitiveDataOnExit: false, lastText: "保存しない");
            Assert.Equal(string.Empty, s.LastInputText);
        }

        // ================================================================
        // 常に保存されるフィールド
        // ================================================================

        [Fact]
        public void VoiceIdは常に保存される()
        {
            var s = Build(voiceId: "my-voice-id");
            Assert.Equal("my-voice-id", s.VoiceId);
        }

        [Fact]
        public void 全ポリシーフィールドは常に保存される()
        {
            var s = Build(
                saveLastInputText: false,
                saveRecentFiles: false,
                clearSensitiveDataOnExit: true,
                deleteLogOnExit: true,
                speechEngineType: "WinRT");

            Assert.False(s.SaveLastInputText);
            Assert.False(s.SaveRecentFiles);
            Assert.True(s.ClearSensitiveDataOnExit);
            Assert.True(s.DeleteLogOnExit);
            Assert.Equal("WinRT", s.SpeechEngineType);
        }

        [Fact]
        public void RateとVolumeは常に保存される()
        {
            var s = Build(rate: -5, volume: 80);
            Assert.Equal(-5, s.Rate);
            Assert.Equal(80, s.Volume);
        }

        // ================================================================
        // v0.6.3〜0.6.7 追加フィールド
        // ================================================================

        [Fact]
        public void AuditRetentionMonthsは指定値が保存される()
        {
            var s = Build(auditRetentionMonths: 6);
            Assert.Equal(6, s.AuditRetentionMonths);
        }

        [Fact]
        public void AutoPreviewEnabledは指定値が保存される()
        {
            var s = Build(autoPreviewEnabled: true);
            Assert.True(s.AutoPreviewEnabled);
        }

        [Fact]
        public void AnnotatedPreviewModeは指定値が保存される()
        {
            var s = Build(annotatedPreviewMode: false);
            Assert.False(s.AnnotatedPreviewMode);
        }

        [Fact]
        public void SsmlPauseStrengthは指定値が保存される()
        {
            var s = Build(ssmlPauseStrength: 2);
            Assert.Equal(2, s.SsmlPauseStrength);
        }

        [Fact]
        public void SaveFilePrefixは指定値が保存される()
        {
            var s = Build(saveFilePrefix: "city");
            Assert.Equal("city", s.SaveFilePrefix);
        }

        [Fact]
        public void SaveFilePrefix空白はkouhouにフォールバックする()
        {
            var s = Build(saveFilePrefix: "   ");
            Assert.Equal("kouhou", s.SaveFilePrefix);
        }

        [Fact]
        public void SaveFilePrefixは前後空白をトリムする()
        {
            var s = Build(saveFilePrefix: "  town  ");
            Assert.Equal("town", s.SaveFilePrefix);
        }
    }
}
