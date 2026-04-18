using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// SpeechEngineFactory の定数・IsKnown・GetLabel を検証するテスト。
    /// Create() は OS リソース（WinRT ランタイム / SAPI）を必要とするため本ファイルでは対象外とする。
    /// </summary>
    public class SpeechEngineFactoryTests
    {
        // ================================================================
        // 定数
        // ================================================================

        [Fact]
        public void 定数_DefaultはSystemSpeechと同じ値である()
        {
            Assert.Equal(SpeechEngineFactory.SystemSpeech, SpeechEngineFactory.Default);
        }

        // ================================================================
        // IsKnown
        // ================================================================

        [Theory]
        [InlineData("SystemSpeech")]
        [InlineData("WinRT")]
        public void IsKnown_既知値はtrueを返す(string engineType)
        {
            Assert.True(SpeechEngineFactory.IsKnown(engineType));
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknown")]
        [InlineData("winrt")]          // 大文字小文字は区別する
        [InlineData("systemspeech")]
        [InlineData("SAPI")]
        [InlineData(null)]
        public void IsKnown_未知値はfalseを返す(string? engineType)
        {
            Assert.False(SpeechEngineFactory.IsKnown(engineType));
        }

        // ================================================================
        // GetLabel
        // ================================================================

        [Fact]
        public void GetLabel_WinRtはOneCore表記を返す()
        {
            Assert.Equal("WinRT (OneCore)", SpeechEngineFactory.GetLabel(SpeechEngineFactory.WinRt));
        }

        [Fact]
        public void GetLabel_SystemSpeechはSAPI表記を返す()
        {
            Assert.Equal("SAPI (System.Speech)", SpeechEngineFactory.GetLabel(SpeechEngineFactory.SystemSpeech));
        }

        [Fact]
        public void GetLabel_未知値はSystemSpeechと同じラベルを返す()
        {
            Assert.Equal(
                SpeechEngineFactory.GetLabel(SpeechEngineFactory.SystemSpeech),
                SpeechEngineFactory.GetLabel("unknown"));
        }

        [Fact]
        public void GetLabel_prefixWindowsをtrueにするとWindowsプレフィクスが付く()
        {
            string label = SpeechEngineFactory.GetLabel(SpeechEngineFactory.WinRt, prefixWindows: true);
            Assert.StartsWith("Windows ", label);
        }

        [Fact]
        public void GetLabel_prefixWindowsがfalseのときはプレフィクスなし()
        {
            string label = SpeechEngineFactory.GetLabel(SpeechEngineFactory.WinRt, prefixWindows: false);
            Assert.DoesNotContain("Windows ", label);
        }

        [Theory]
        [InlineData("SystemSpeech", false, "SAPI (System.Speech)")]
        [InlineData("SystemSpeech", true,  "Windows SAPI (System.Speech)")]
        [InlineData("WinRT",        false, "WinRT (OneCore)")]
        [InlineData("WinRT",        true,  "Windows WinRT (OneCore)")]
        public void GetLabel_組み合わせパターンが期待値と一致する(
            string engineType, bool prefix, string expected)
        {
            Assert.Equal(expected, SpeechEngineFactory.GetLabel(engineType, prefix));
        }
    }
}
