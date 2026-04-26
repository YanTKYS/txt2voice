using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TxtToVoice.Models;

namespace TxtToVoice.Services
{
    /// <summary>
    /// text_rules.json から読み込んだ変換ルールを保持する。
    /// Apply() でテキストに対してパターン置換を実行する。
    /// </summary>
    public sealed class CompiledTextRule
    {
        private readonly Regex _pattern;
        private readonly string _replacement;

        internal CompiledTextRule(Regex pattern, string replacement)
        {
            _pattern     = pattern;
            _replacement = replacement;
        }

        public string Apply(string text)
        {
            try
            {
                return _pattern.Replace(text, _replacement);
            }
            catch (RegexMatchTimeoutException)
            {
                Logger.Warn($"テキストルール 正規表現タイムアウト（スキップ）: pattern={_pattern}");
                return text;
            }
        }
    }

    /// <summary>
    /// <c>text_rules.json</c> を読み込み、有効なルールをコンパイルして返す。
    /// ファイルが存在しない・JSON エラー・無効な正規表現はすべてスキップしてログに記録する。
    /// </summary>
    public static class TextRuleLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static IReadOnlyList<CompiledTextRule> Load(string filePath)
        {
            if (!File.Exists(filePath))
                return Array.Empty<CompiledTextRule>();

            List<TextRule> rules;
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                rules = JsonSerializer.Deserialize<List<TextRule>>(json, JsonOptions)
                        ?? new List<TextRule>();
            }
            catch (Exception ex)
            {
                Logger.Error($"テキストルール JSON 解析エラー: {filePath} / {ex.Message}");
                return Array.Empty<CompiledTextRule>();
            }

            var compiled = new List<CompiledTextRule>(rules.Count);
            foreach (var rule in rules)
            {
                if (!rule.Enabled) continue;
                if (string.IsNullOrEmpty(rule.Pattern)) continue;

                try
                {
                    var regex = new Regex(rule.Pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(1));
                    compiled.Add(new CompiledTextRule(regex, rule.Replacement));
                }
                catch (ArgumentException ex)
                {
                    Logger.Error($"テキストルール 正規表現エラー（スキップ）: pattern={rule.Pattern} / {ex.Message}");
                }
            }
            return compiled;
        }

        /// <summary>
        /// text_rules.json を生の <see cref="TextRule"/> リスト（disabled 含む）として読み込む。
        /// ファイル不在・JSON エラー時は空リストを返す。
        /// </summary>
        public static List<TextRule> LoadRaw(string filePath)
        {
            if (!File.Exists(filePath)) return new List<TextRule>();
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                return JsonSerializer.Deserialize<List<TextRule>>(json, JsonOptions)
                       ?? new List<TextRule>();
            }
            catch (Exception ex)
            {
                Logger.Error($"テキストルール JSON 解析エラー (LoadRaw): {filePath} / {ex.Message}");
                return new List<TextRule>();
            }
        }

        /// <summary>
        /// <see cref="TextRule"/> リストを text_rules.json に書き出す（インデント整形）。
        /// </summary>
        public static void SaveRaw(string filePath, IEnumerable<TextRule> rules)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(rules.ToList(), options);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }
    }
}
