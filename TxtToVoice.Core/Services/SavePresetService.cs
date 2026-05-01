using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TxtToVoice.Models;

namespace TxtToVoice.Services
{
    /// <summary>保存プリセット（save_presets.json）を読み書きする静的サービス。</summary>
    public static class SavePresetService
    {
        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

        public static List<SavePreset> Load(string filePath)
        {
            if (!File.Exists(filePath)) return new List<SavePreset>();
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                return JsonSerializer.Deserialize<List<SavePreset>>(json, ReadOptions) ?? new List<SavePreset>();
            }
            catch (Exception ex)
            {
                Logger.Error($"保存プリセット JSON 解析エラー: {filePath} / {ex.Message}");
                return new List<SavePreset>();
            }
        }

        public static void Save(string filePath, IEnumerable<SavePreset> presets)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(presets.ToList(), options);
                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(filePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error($"[{TtvErrorCode.IoPresetSaveFailed}] 保存プリセット保存失敗: {ex.Message}");
                throw;
            }
        }
    }
}
