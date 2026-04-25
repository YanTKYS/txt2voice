using System.Collections.Generic;
using System.Text;
using TxtToVoice.Models;

namespace TxtToVoice.Services
{
    /// <summary>
    /// <see cref="CsvService.ImportWithReport"/> が返す CSV インポートバリデーション結果。
    /// </summary>
    public sealed class CsvImportReport
    {
        /// <summary>バリデーションを通過した有効エントリ</summary>
        public List<DictionaryEntry> ValidEntries { get; }

        /// <summary>表記が空だったためスキップされた行数</summary>
        public int SkippedEmptyDisplay { get; }

        /// <summary>読みが空だったためスキップされた行数</summary>
        public int SkippedEmptyReading { get; }

        /// <summary>優先順位が 1〜100 の範囲外だったため補正された行数</summary>
        public int PriorityClampedCount { get; }

        /// <summary>スキップ・補正が 1 件以上ある場合 true</summary>
        public bool HasIssues =>
            SkippedEmptyDisplay > 0 || SkippedEmptyReading > 0 || PriorityClampedCount > 0;

        public CsvImportReport(
            List<DictionaryEntry> validEntries,
            int skippedEmptyDisplay,
            int skippedEmptyReading,
            int priorityClampedCount)
        {
            ValidEntries        = validEntries;
            SkippedEmptyDisplay = skippedEmptyDisplay;
            SkippedEmptyReading = skippedEmptyReading;
            PriorityClampedCount = priorityClampedCount;
        }

        /// <summary>スキップ・補正の内訳を改行区切りの文字列で返す（MessageBox 表示用）</summary>
        public string FormatIssues()
        {
            var sb = new StringBuilder();
            if (SkippedEmptyDisplay > 0)
                sb.AppendLine($"  スキップ（表記が空）     : {SkippedEmptyDisplay} 件");
            if (SkippedEmptyReading > 0)
                sb.AppendLine($"  スキップ（読みが空）     : {SkippedEmptyReading} 件");
            if (PriorityClampedCount > 0)
                sb.AppendLine($"  優先順位を補正（範囲外→1〜100）: {PriorityClampedCount} 件");
            return sb.ToString().TrimEnd();
        }
    }
}
