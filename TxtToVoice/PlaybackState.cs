namespace TxtToVoice
{
    /// <summary>再生状態を表す値型。_isSpeaking / _isPaused の代わりに使用する（#56-b）。</summary>
    internal sealed record PlaybackState(bool IsSpeaking, bool IsPaused)
    {
        internal static readonly PlaybackState Idle   = new(false, false);
        internal static readonly PlaybackState Active = new(true,  false);
        internal static readonly PlaybackState Paused = new(true,  true);
    }
}
