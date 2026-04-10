using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using TxtToVoice.Models;

namespace TxtToVoice.Services
{
    /// <summary>
    /// アプリケーション設定の読み書きを担当するサービス。
    /// デフォルト保存先: %LOCALAPPDATA%\TxtToVoice\settings.json
    /// テスト時はコンストラクタで任意パスを指定できる。
    /// 失敗時は例外をスローせずログに記録し、デフォルト値を返す。
    /// </summary>
    public class AppSettingsService
    {
        private static readonly string DefaultSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TxtToVoice", "settings.json");

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly string _settingsPath;

        /// <param name="customPath">テスト用パス。null のとき %LOCALAPPDATA%\TxtToVoice\settings.json を使う。</param>
        public AppSettingsService(string? customPath = null)
        {
            _settingsPath = customPath ?? DefaultSettingsPath;
        }

        public AppSettings Load()
        {
            if (!File.Exists(_settingsPath)) return new AppSettings();
            try
            {
                string json = File.ReadAllText(_settingsPath, Encoding.UTF8);
                return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Logger.Warn($"設定ファイルの読み込みに失敗しました: {ex.Message}");
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            string? dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string tmp = _settingsPath + ".tmp";
            try
            {
                string json = JsonSerializer.Serialize(settings, Options);
                File.WriteAllText(tmp, json, Encoding.UTF8);
                File.Move(tmp, _settingsPath, overwrite: true);
            }
            catch (Exception ex)
            {
                try { File.Delete(tmp); } catch { }
                Logger.Warn($"設定の保存に失敗しました: {ex.Message}");
            }
        }
    }
}
