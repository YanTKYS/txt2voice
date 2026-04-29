using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using TxtToVoice.Models;

namespace TxtToVoice.Services
{
    /// <summary>辞書・テンプレ・設定をまとめた運用パック（ZIP）のエクスポート/インポート。</summary>
    public static class OperationalPackService
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        // ZIPに含めるファイル名（エントリ名として使用）
        public const string EntryDictionary  = "dictionary.json";
        public const string EntryTemplates   = "templates.json";
        public const string EntrySettings    = "settings.json";
        public const string EntryTextRules   = "text_rules.json";
        public const string EntryPresets     = "save_presets.json";

        /// <summary>
        /// 指定パスのファイルをまとめて ZIP に書き出す。
        /// settings.json は機微フィールド（LastInputText / RecentFiles）を除去してエクスポート。
        /// </summary>
        public static void Export(
            string outputZipPath,
            string dictionaryPath,
            string templatesPath,
            string settingsPath,
            string? textRulesPath   = null,
            string? savePresetsPath = null)
        {
            using var zip = ZipFile.Open(outputZipPath, ZipArchiveMode.Create);

            AddFileIfExists(zip, dictionaryPath, EntryDictionary);
            AddFileIfExists(zip, templatesPath,  EntryTemplates);
            AddSanitizedSettings(zip, settingsPath);

            if (textRulesPath != null)
                AddFileIfExists(zip, textRulesPath, EntryTextRules);
            if (savePresetsPath != null)
                AddFileIfExists(zip, savePresetsPath, EntryPresets);
        }

        /// <summary>
        /// ZIP から各ファイルを指定パスへ展開して上書きコピーする。
        /// 戻り値: ZIP に含まれていたエントリ名のリスト（有効なもののみ）。
        /// </summary>
        public static List<string> Import(
            string zipPath,
            string dictionaryPath,
            string templatesPath,
            string settingsPath,
            string? textRulesPath   = null,
            string? savePresetsPath = null)
        {
            var imported = new List<string>();
            using var zip = ZipFile.OpenRead(zipPath);

            ExtractEntry(zip, EntryDictionary, dictionaryPath, imported);
            ExtractEntry(zip, EntryTemplates,  templatesPath,  imported);
            ExtractEntry(zip, EntrySettings,   settingsPath,   imported);

            if (textRulesPath != null)
                ExtractEntry(zip, EntryTextRules, textRulesPath, imported);
            if (savePresetsPath != null)
                ExtractEntry(zip, EntryPresets, savePresetsPath, imported);

            return imported;
        }

        /// <summary>ZIP に含まれるエントリ名（既知ファイルのみ）を返す（インポート前プレビュー用）。</summary>
        public static List<string> ListContents(string zipPath)
        {
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { EntryDictionary, EntryTemplates, EntrySettings, EntryTextRules, EntryPresets };
            var result = new List<string>();
            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var entry in zip.Entries)
                if (known.Contains(entry.Name)) result.Add(entry.Name);
            return result;
        }

        private static void AddFileIfExists(ZipArchive zip, string filePath, string entryName)
        {
            if (!File.Exists(filePath)) return;
            zip.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
        }

        private static void AddSanitizedSettings(ZipArchive zip, string settingsPath)
        {
            if (!File.Exists(settingsPath)) return;
            try
            {
                string json = File.ReadAllText(settingsPath, Encoding.UTF8);
                var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
                s.LastInputText = string.Empty;
                s.RecentFiles   = new List<string>();
                string sanitized = JsonSerializer.Serialize(s, JsonOpts);
                var entry = zip.CreateEntry(EntrySettings, CompressionLevel.Optimal);
                using var stream = entry.Open();
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                writer.Write(sanitized);
            }
            catch
            {
                // 読み取り失敗時は元ファイルをそのまま追加
                zip.CreateEntryFromFile(settingsPath, EntrySettings, CompressionLevel.Optimal);
            }
        }

        private static void ExtractEntry(ZipArchive zip, string entryName, string destPath, List<string> imported)
        {
            var entry = zip.GetEntry(entryName);
            if (entry == null) return;
            string? dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            entry.ExtractToFile(destPath, overwrite: true);
            imported.Add(entryName);
        }
    }
}
