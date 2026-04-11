using System;
using System.IO;

namespace TxtToVoice.Services
{
    /// <summary>
    /// データ保存先パスを一元管理する静的クラス。
    ///
    /// ポータブルモード:
    ///   EXE と同じフォルダに portable.flag ファイルが存在する場合に有効。
    ///   辞書・設定・ログをすべて EXE フォルダ配下に保存する。
    ///
    /// 通常モード:
    ///   %LOCALAPPDATA%\TxtToVoice\ に保存する（デフォルト）。
    /// </summary>
    public static class PathConfig
    {
        /// <summary>ポータブルモードで動作中かどうか。</summary>
        public static readonly bool IsPortable;

        /// <summary>辞書・設定ファイルの保存ディレクトリ。</summary>
        public static readonly string DataDirectory;

        /// <summary>ログファイルの保存ディレクトリ。</summary>
        public static readonly string LogDirectory;

        static PathConfig()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string flagFile = Path.Combine(exeDir, "portable.flag");
            IsPortable = File.Exists(flagFile);

            if (IsPortable)
            {
                DataDirectory = exeDir;
                LogDirectory  = Path.Combine(exeDir, "logs");
            }
            else
            {
                DataDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TxtToVoice");
                LogDirectory = Path.Combine(DataDirectory, "logs");
            }
        }

        /// <summary>辞書 JSON ファイルのフルパス。</summary>
        public static string DictionaryPath => Path.Combine(DataDirectory, "dictionary.json");

        /// <summary>設定 JSON ファイルのフルパス。</summary>
        public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    }
}
