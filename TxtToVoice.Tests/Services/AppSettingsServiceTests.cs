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
    /// AppSettingsService の読み書きロジックを検証するテスト。
    /// 一時ファイルを使用し、実際の設定ファイルには影響を与えない。
    /// </summary>
    public class AppSettingsServiceTests : IDisposable
    {
        private readonly string _tempPath;
        private readonly AppSettingsService _svc;

        public AppSettingsServiceTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"TxtToVoiceSettingsTest_{Guid.NewGuid()}.json");
            _svc = new AppSettingsService(_tempPath);
        }

        public void Dispose()
        {
            try { File.Delete(_tempPath); }             catch { /* 無視 */ }
            try { File.Delete(_tempPath + ".tmp"); }    catch { /* 無視 */ }
        }

        // ================================================================
        // Load — ファイルなし
        // ================================================================

        [Fact]
        public void Load_ファイルが存在しない場合はデフォルト値を返す()
        {
            var s = _svc.Load();
            Assert.Equal(0,           s.Rate);
            Assert.Equal(100,         s.Volume);
            Assert.Equal(string.Empty, s.VoiceName);
            Assert.False(s.SsmlPauseEnabled);
            Assert.Equal(string.Empty, s.LastInputText);
            Assert.Empty(s.RecentFiles);
        }

        // ================================================================
        // Save / Load — ラウンドトリップ
        // ================================================================

        [Fact]
        public void SaveLoad_基本設定を保存して復元できる()
        {
            _svc.Save(new AppSettings
            {
                Rate             = 3,
                Volume           = 80,
                VoiceName        = "Microsoft Haruka Desktop",
                SsmlPauseEnabled = true
            });
            var s = _svc.Load();
            Assert.Equal(3,    s.Rate);
            Assert.Equal(80,   s.Volume);
            Assert.Equal("Microsoft Haruka Desktop", s.VoiceName);
            Assert.True(s.SsmlPauseEnabled);
        }

        [Fact]
        public void SaveLoad_LastInputTextを保存して復元できる()
        {
            _svc.Save(new AppSettings { LastInputText = "令和7年度の広報原稿" });
            var s = _svc.Load();
            Assert.Equal("令和7年度の広報原稿", s.LastInputText);
        }

        [Fact]
        public void SaveLoad_RecentFilesリストを保存して順序ごと復元できる()
        {
            _svc.Save(new AppSettings
            {
                RecentFiles = new List<string>
                {
                    @"C:\Users\user\Desktop\kouhou.txt",
                    @"C:\Users\user\Documents\draft.txt"
                }
            });
            var s = _svc.Load();
            Assert.Equal(2, s.RecentFiles.Count);
            Assert.Equal(@"C:\Users\user\Desktop\kouhou.txt",    s.RecentFiles[0]);
            Assert.Equal(@"C:\Users\user\Documents\draft.txt", s.RecentFiles[1]);
        }

        [Fact]
        public void SaveLoad_日本語パスや日本語テキストを正しく保存できる()
        {
            _svc.Save(new AppSettings
            {
                VoiceName     = "日本語音声エンジン",
                LastInputText = "市長の挨拶：令和7年度も皆様のご支援を…",
                RecentFiles   = new List<string> { @"C:\広報課\原稿\4月号.txt" }
            });
            var s = _svc.Load();
            Assert.Equal("日本語音声エンジン", s.VoiceName);
            Assert.Contains("令和7年度", s.LastInputText);
            Assert.Contains(@"C:\広報課\原稿\4月号.txt", s.RecentFiles);
        }

        // ================================================================
        // Load — 破損 JSON
        // ================================================================

        [Fact]
        public void Load_破損したJSONはデフォルト値にフォールバックする()
        {
            File.WriteAllText(_tempPath, "{ invalid json {{{{", Encoding.UTF8);
            var s = _svc.Load(); // 例外をスローしないこと
            Assert.Equal(0,   s.Rate);
            Assert.Equal(100, s.Volume);
        }

        [Fact]
        public void Load_空ファイルはデフォルト値にフォールバックする()
        {
            File.WriteAllText(_tempPath, string.Empty, Encoding.UTF8);
            var s = _svc.Load();
            Assert.Equal(0, s.Rate);
        }

        [Fact]
        public void Load_部分的なJSONは読み込めた範囲のみ反映しデフォルト値で補完する()
        {
            // Rate だけ含む部分的な JSON
            File.WriteAllText(_tempPath, "{\"rate\":5}", Encoding.UTF8);
            var s = _svc.Load();
            Assert.Equal(5,   s.Rate);
            Assert.Equal(100, s.Volume);    // デフォルトのまま
        }

        // ================================================================
        // 原子的保存（.tmp ファイルが残らない）
        // ================================================================

        [Fact]
        public void Save_保存後にtmpファイルが残らない()
        {
            _svc.Save(new AppSettings { Rate = 1 });
            Assert.False(File.Exists(_tempPath + ".tmp"));
            Assert.True(File.Exists(_tempPath));
        }

        [Fact]
        public void Save_複数回保存しても最後の値が読み込まれる()
        {
            _svc.Save(new AppSettings { Rate = 1 });
            _svc.Save(new AppSettings { Rate = 5 });
            _svc.Save(new AppSettings { Rate = -3 });
            Assert.Equal(-3, _svc.Load().Rate);
        }
    }
}
