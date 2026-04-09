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
                Logger.Error($"未処理例外: {ex.Exception.Message}\n{ex.Exception.StackTrace}");
                MessageBox.Show(
                    $"予期しないエラーが発生しました。\n\n{ex.Exception.Message}\n\nアプリケーションを続行します。",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                ex.Handled = true;
            };
        }
    }
}
