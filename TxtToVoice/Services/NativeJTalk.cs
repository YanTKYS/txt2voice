using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TxtToVoice.Services
{
    /// <summary>
    /// jtalk.dll (jtalkdll: https://github.com/rosmarinus/jtalkdll) の P/Invoke ラッパー。
    ///
    /// jtalkdll は .NET 向けに UTF-16 変種（U16 サフィックス）の関数を公開しており、
    /// CharSet.Unicode で .NET の string をそのまま渡せる。
    /// </summary>
    internal static class NativeJTalk
    {
        private const string Dll = "jtalk";

        [DllImport(Dll, EntryPoint = "openjtalk_initializeU16",
            CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr openjtalk_initialize(string voice, string dic, string voiceDir);

        [DllImport(Dll, EntryPoint = "openjtalk_clear",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void openjtalk_clear(IntPtr handle);

        [DllImport(Dll, EntryPoint = "openjtalk_speakToFileU16",
            CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        private static extern bool openjtalk_speakToFile(IntPtr handle, string text, string file);

        [DllImport(Dll, EntryPoint = "openjtalk_setSpeed",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void openjtalk_setSpeed(IntPtr handle, double speed);

        [DllImport(Dll, EntryPoint = "openjtalk_setVolume",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void openjtalk_setVolume(IntPtr handle, double volume);

        [DllImport(Dll, EntryPoint = "openjtalk_setAdditionalHalfTone",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void openjtalk_setAdditionalHalfTone(IntPtr handle, double halfTone);

        // ---- Public wrappers ------------------------------------------------

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

        public static bool IsDllPresent()
            => File.Exists(Path.Combine(AppContext.BaseDirectory, "jtalk.dll"));
    }
}
