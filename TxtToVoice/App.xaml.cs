using System.Windows;
using TxtToVoice.Services;

namespace TxtToVoice
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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
    }
}
