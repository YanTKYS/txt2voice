using System.Collections.Generic;
using TxtToVoice.Models;

namespace TxtToVoice.Services
{
    /// <summary>
    /// AppSettings の組み立てロジックを UI から分離した純粋関数ユーティリティ。
    /// BuildAppSettings(isExit) の UI 非依存部分をここに集約することで単体テストを可能にする。
    /// </summary>
    public static class AppSettingsBuilder
    {
        /// <summary>LastInputText を保存対象に含める上限文字数</summary>
        public const int MaxPersistedInputTextLength = 10_000;

        public static AppSettings Build(
            bool isExit,
            int rate,
            int volume,
            string? voiceName,
            string? voiceId,
            bool ssmlEnabled,
            bool showHighlight,
            bool saveLastInputText,
            bool saveRecentFiles,
            bool clearSensitiveDataOnExit,
            bool deleteLogOnExit,
            string speechEngineType,
            string lastText,
            IReadOnlyList<string> recentFiles,
            int auditRetentionMonths = 13,
            bool autoPreviewEnabled = false,
            bool annotatedPreviewMode = true)
        {
            bool persistText   = isExit && saveLastInputText && !clearSensitiveDataOnExit;
            bool persistRecent = isExit
                ? saveRecentFiles && !clearSensitiveDataOnExit
                : saveRecentFiles;

            return new AppSettings
            {
                Rate             = rate,
                Volume           = volume,
                VoiceName        = voiceName ?? string.Empty,
                VoiceId          = voiceId   ?? string.Empty,
                SsmlPauseEnabled = ssmlEnabled,
                LastInputText    = persistText && lastText.Length <= MaxPersistedInputTextLength
                                     ? lastText
                                     : string.Empty,
                RecentFiles      = persistRecent ? new List<string>(recentFiles) : new List<string>(),
                SaveLastInputText        = saveLastInputText,
                SaveRecentFiles          = saveRecentFiles,
                ClearSensitiveDataOnExit = clearSensitiveDataOnExit,
                DeleteLogOnExit          = deleteLogOnExit,
                ShowReadingHighlight     = showHighlight,
                SpeechEngineType         = speechEngineType,
                AuditRetentionMonths     = auditRetentionMonths,
                AutoPreviewEnabled       = autoPreviewEnabled,
                AnnotatedPreviewMode     = annotatedPreviewMode
            };
        }
    }
}
