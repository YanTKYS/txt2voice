using System.Collections.Generic;

namespace TxtToVoice.Services
{
    /// <summary>
    /// 辞書適用後テキスト（音声合成用）の文字位置を、元テキストの文字位置に変換する。
    /// <see cref="DictionaryService.ApplyDictionaryForSpeech"/> と合わせて使用する。
    ///
    /// セグメント構造: 半開区間 [speechStart, speechEnd) → [origStart, origEnd)
    ///   - 辞書未置換区間: 1:1 対応
    ///   - 辞書置換区間: 読み全体 → 元の表記全体 に対応
    /// </summary>
    public sealed class SpeechPositionMap
    {
        private readonly (int SpeechStart, int SpeechEnd, int OrigStart, int OrigEnd)[] _segments;

        internal SpeechPositionMap(
            List<(int SpeechStart, int SpeechEnd, int OrigStart, int OrigEnd)> segments)
        {
            _segments = segments.ToArray();
        }

        /// <summary>
        /// 音声テキスト上の位置を元テキストの (開始位置, 長さ) に変換する。
        /// 対応する区間が見つからない場合は (-1, 0) を返す。
        /// </summary>
        public (int Start, int Length) MapToOriginal(int speechPos)
        {
            foreach (var (ss, se, os, oe) in _segments)
            {
                if (speechPos >= ss && speechPos < se)
                    return (os, oe - os);
            }
            return (-1, 0);
        }
    }
}
