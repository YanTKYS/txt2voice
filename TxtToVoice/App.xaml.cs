using System.Windows;
using NAudio.MediaFoundation;
using TxtToVoice.Services;

namespace TxtToVoice
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // NAudio が使用する Windows Media Foundation を初期化
            // MP3 / MP4(AAC) エンコードに必要。失敗しても WAV 保存・再生は使用可能
            try
            {
                MediaFoundationApi.Startup();
                Logger.Info("Media Foundation 初期化成功");
            }
            catch (System.Exception ex)
            {
                Logger.Warn($"Media Foundation 初期化失敗（MP3/MP4保存は使用不可）: {ex.Message}");
            }

            // ハンドルされない例外をログに記録
            DispatcherUnhandledException += (s, ex) =>
            {
                // TargetInvocationException は InnerException に本当の原因が入っている
                var actual = ex.Exception.InnerException ?? ex.Exception;
                Logger.Error(
                    $"未処理例外: [{actual.GetType().Name}] {actual.Message}\n" +
                    $"StackTrace: {actual.StackTrace}");

                string logPath = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    "TxtToVoice", "logs");

                MessageBox.Show(
                    $"予期しないエラーが発生しました。\n\n" +
                    $"種別: {actual.GetType().Name}\n" +
                    $"内容: {actual.Message}\n\n" +
                    $"詳細はログファイルを確認してください。\n{logPath}",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                ex.Handled = true;
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { MediaFoundationApi.Shutdown(); } catch { /* 無視 */ }
            base.OnExit(e);
        }
    }
}
