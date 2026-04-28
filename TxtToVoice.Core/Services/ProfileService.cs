using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TxtToVoice.Models;

namespace TxtToVoice.Services
{
    /// <summary>再生プロファイル（profiles.json）を読み書きする静的サービス。</summary>
    public static class ProfileService
    {
        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

        public static List<PlaybackProfile> Load(string filePath)
        {
            if (!File.Exists(filePath)) return new List<PlaybackProfile>();
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                return JsonSerializer.Deserialize<List<PlaybackProfile>>(json, ReadOptions) ?? new List<PlaybackProfile>();
            }
            catch (Exception ex)
            {
                Logger.Error($"プロファイル JSON 解析エラー: {filePath} / {ex.Message}");
                return new List<PlaybackProfile>();
            }
        }

        public static void Save(string filePath, IEnumerable<PlaybackProfile> profiles)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(profiles.ToList(), options);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }
    }
}
