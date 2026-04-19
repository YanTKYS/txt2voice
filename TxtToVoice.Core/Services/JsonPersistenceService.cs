using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using TxtToVoice.Models;

namespace TxtToVoice.Services
{
    /// <summary>
    /// 辞書データの JSON 読み書きを担当するサービス。
    /// </summary>
    public class JsonPersistenceService
    {
        private readonly string _filePath;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            // 日本語をエスケープしない
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public JsonPersistenceService(string filePath)
        {
            _filePath = filePath;
        }

        /// <summary>
        /// JSON ファイルから辞書エントリを読み込む。
        /// ファイルが存在しない場合は空リストを返す。
        /// </summary>
        /// <exception cref="InvalidDataException">ファイルが破損している場合</exception>
        public List<DictionaryEntry> Load()
        {
            if (!File.Exists(_filePath))
            {
                Logger.Info($"辞書ファイルが見つかりません（新規作成します）: {_filePath}");
                return new List<DictionaryEntry>();
            }

            try
            {
                string json = File.ReadAllText(_filePath, Encoding.UTF8);
                var result = JsonSerializer.Deserialize<List<DictionaryEntry>>(json, SerializerOptions);
                return result ?? new List<DictionaryEntry>();
            }
            catch (JsonException ex)
            {
                Logger.Error($"辞書 JSON 解析エラー: {_filePath} / {ex.Message}");
                throw new InvalidDataException(
                    $"辞書ファイルの形式が正しくありません。\n" +
                    $"ファイル: {_filePath}\n\n" +
                    $"バックアップを確認するか、「Data\\sample_dictionary.json」を\n" +
                    $"コピーして辞書を初期化してください。\n\n詳細: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error($"辞書ファイル読み込み失敗: {_filePath} / {ex.Message}");
                throw new InvalidDataException(
                    $"辞書ファイルを読み込めませんでした。\n" +
                    $"ファイル: {_filePath}\n詳細: {ex.Message}");
            }
        }

        /// <summary>
        /// 辞書エントリを JSON ファイルに保存する。
        /// </summary>
        public void Save(List<DictionaryEntry> entries)
        {
            string? dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // 一時ファイルに書いてからリネーム（書き込み中断による破損を防ぐ）
            string tmp = _filePath + ".tmp";
            try
            {
                string json = JsonSerializer.Serialize(entries, SerializerOptions);
                File.WriteAllText(tmp, json, Encoding.UTF8);
                File.Move(tmp, _filePath, overwrite: true);
            }
            catch
            {
                // Move 失敗などで tmp が残った場合は削除して例外を伝播
                try { File.Delete(tmp); } catch { /* 無視 */ }
                throw;
            }

            Logger.Info($"辞書保存完了: {entries.Count}件 → {_filePath}");
        }
    }
}
