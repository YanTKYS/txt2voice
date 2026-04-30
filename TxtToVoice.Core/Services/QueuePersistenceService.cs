using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TxtToVoice.Models;

namespace TxtToVoice.Services
{
    /// <summary>読み上げキュー（queue.json）を読み書きする静的サービス。</summary>
    public static class QueuePersistenceService
    {
        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

        public static List<QueueEntry> Load(string filePath)
        {
            if (!File.Exists(filePath)) return new List<QueueEntry>();
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                return JsonSerializer.Deserialize<List<QueueEntry>>(json, ReadOptions) ?? new List<QueueEntry>();
            }
            catch (Exception ex)
            {
                Logger.Error($"キュー JSON 解析エラー: {filePath} / {ex.Message}");
                return new List<QueueEntry>();
            }
        }

        public static void Save(string filePath, IEnumerable<QueueEntry> entries)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(entries.ToList(), options);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }
    }
}
