using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TxtToVoice.Models;

namespace TxtToVoice.Services
{
    /// <summary>
    /// templates.json を読み書きする静的サービス。
    /// </summary>
    public static class TemplateService
    {
        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

        public static List<Template> Load(string filePath)
        {
            if (!File.Exists(filePath)) return new List<Template>();
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                return JsonSerializer.Deserialize<List<Template>>(json, ReadOptions) ?? new List<Template>();
            }
            catch (Exception ex)
            {
                Logger.Error($"テンプレート JSON 解析エラー: {filePath} / {ex.Message}");
                return new List<Template>();
            }
        }

        public static void Save(string filePath, IEnumerable<Template> templates)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(templates.ToList(), options);
            string? _dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(_dir)) Directory.CreateDirectory(_dir);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }
    }
}
