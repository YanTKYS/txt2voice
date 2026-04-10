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
    /// 保存先: %LOCALAPPDATA%\TxtToVoice\settings.json
    /// 失敗時は例外をスローせずログに記録し、デフォルト値を返す。
    /// </summary>
    public class AppSettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TxtToVoice", "settings.json");

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public AppSettings Load()
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            try
            {
                string json = File.ReadAllText(SettingsPath, Encoding.UTF8);
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
            string? dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string tmp = SettingsPath + ".tmp";
            try
            {
                string json = JsonSerializer.Serialize(settings, Options);
                File.WriteAllText(tmp, json, Encoding.UTF8);
                File.Move(tmp, SettingsPath, overwrite: true);
            }
            catch (Exception ex)
            {
                try { File.Delete(tmp); } catch { }
                Logger.Warn($"設定の保存に失敗しました: {ex.Message}");
            }
        }
    }
}
