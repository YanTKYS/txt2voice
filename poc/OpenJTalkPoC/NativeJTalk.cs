using System.Runtime.InteropServices;
using System.Text;

namespace OpenJTalkPoC;

/// <summary>
/// jtalk.dll (jtalkdll: https://github.com/rosmarinus/jtalkdll) の P/Invoke ラッパー。
///
/// jtalkdll は .NET 向けに UTF-16 変種（U16 サフィックス）の関数を公開しており、
/// CharSet.Unicode で .NET の string をそのまま渡せる。
/// 文字列を取らない関数（Clear / SetSpeed 等）は通常版を使用する。
/// </summary>
internal static class NativeJTalk
{
    private const string Dll = "jtalk";

    // ---- 初期化・解放 --------------------------------------------------------

    /// <summary>
    /// OpenJTalk を初期化してハンドルを返す。失敗時は IntPtr.Zero。
    /// </summary>
    /// <param name="voice">HTS voice ファイルのフルパス（.htsvoice）</param>
    /// <param name="dic">MeCab UTF-8 辞書ディレクトリのフルパス</param>
    /// <param name="voiceDir">追加ボイスディレクトリ（不要なら空文字）</param>
    [DllImport(Dll, EntryPoint = "openjtalk_initializeU16",
        CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr openjtalk_initialize(string voice, string dic, string voiceDir);

    /// <summary>ハンドルを解放する。</summary>
    [DllImport(Dll, EntryPoint = "openjtalk_clear",
        CallingConvention = CallingConvention.StdCall)]
    private static extern void openjtalk_clear(IntPtr handle);

    // ---- 音声合成 ------------------------------------------------------------

    /// <summary>テキストを WAV ファイルに合成する。成功時 true。</summary>
    [DllImport(Dll, EntryPoint = "openjtalk_speakToFileU16",
        CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern bool openjtalk_speakToFile(IntPtr handle, string text, string file);

    // ---- パラメータ設定 -------------------------------------------------------

    /// <summary>読み上げ速度を設定する（既定: 1.0、範囲: 0.5〜4.0）。</summary>
    [DllImport(Dll, EntryPoint = "openjtalk_setSpeed",
        CallingConvention = CallingConvention.StdCall)]
    private static extern void openjtalk_setSpeed(IntPtr handle, double speed);

    /// <summary>音量を設定する（既定: 0.0、単位: dB）。</summary>
    [DllImport(Dll, EntryPoint = "openjtalk_setVolume",
        CallingConvention = CallingConvention.StdCall)]
    private static extern void openjtalk_setVolume(IntPtr handle, double volume);

    /// <summary>ピッチ半音オフセットを設定する（既定: 0.0）。</summary>
    [DllImport(Dll, EntryPoint = "openjtalk_setAdditionalHalfTone",
        CallingConvention = CallingConvention.StdCall)]
    private static extern void openjtalk_setAdditionalHalfTone(IntPtr handle, double halfTone);

    // ---- 状態確認 ------------------------------------------------------------

    [DllImport(Dll, EntryPoint = "openjtalk_getVoicePathU16",
        CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr openjtalk_getVoicePath(IntPtr handle, StringBuilder buf);

    // =========================================================================
    // Public wrapper
    // =========================================================================

    public static IntPtr Initialize(string voicePath, string dicPath, string voiceDir = "")
        => openjtalk_initialize(voicePath, dicPath, voiceDir);

    public static void Clear(IntPtr handle) => openjtalk_clear(handle);

    public static bool SpeakToFile(IntPtr handle, string text, string outputWavPath)
        => openjtalk_speakToFile(handle, text, outputWavPath);

    public static void SetSpeed(IntPtr handle, double speed)
        => openjtalk_setSpeed(handle, speed);

    public static void SetVolume(IntPtr handle, double volumeDb)
        => openjtalk_setVolume(handle, volumeDb);

    public static void SetPitch(IntPtr handle, double halfTone)
        => openjtalk_setAdditionalHalfTone(handle, halfTone);

    /// <summary>jtalk.dll が exe と同じディレクトリに存在するか確認する。</summary>
    public static bool IsDllPresent()
    {
        string dllPath = Path.Combine(AppContext.BaseDirectory, "jtalk.dll");
        return File.Exists(dllPath);
    }
}
