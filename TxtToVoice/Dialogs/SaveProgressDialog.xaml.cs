using System.ComponentModel;
using System.Threading;
using System.Windows;

namespace TxtToVoice.Dialogs
{
    /// <summary>
    /// 音声保存の進捗を表示し、キャンセル操作を受け付けるダイアログ。
    /// Show()（非モーダル）で表示し、保存完了後に呼び出し元が Close() する。
    /// </summary>
    public partial class SaveProgressDialog : Window
    {
        private readonly CancellationTokenSource _cts;
        private bool _completed;

        public SaveProgressDialog(CancellationTokenSource cts)
        {
            InitializeComponent();
            _cts = cts;
        }

        /// <summary>保存が正常完了したことをマークする。ウィンドウを閉じてもキャンセルしない。</summary>
        public void MarkCompleted() => _completed = true;

        /// <summary>フェーズラベルを更新する（UI / バックグラウンドスレッド両対応）。</summary>
        public void UpdatePhase(string message)
        {
            if (!Dispatcher.CheckAccess())
                Dispatcher.Invoke(() => TxtPhase.Text = message);
            else
                TxtPhase.Text = message;
        }

        /// <summary>進捗率（0〜100）を更新する（UI / バックグラウンドスレッド両対応）。</summary>
        public void UpdateProgress(int percent)
        {
            if (!Dispatcher.CheckAccess())
                Dispatcher.Invoke(() => ApplyProgress(percent));
            else
                ApplyProgress(percent);
        }

        private void ApplyProgress(int percent)
        {
            ProgBar.Value = percent;
            TxtPercent.Text = $"{percent}%";
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            BtnCancel.IsEnabled = false;
            BtnCancel.Content = "キャンセル中...";
            _cts.Cancel();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // X ボタン等で閉じる場合も、未完了ならキャンセルを発行する
            if (!_completed && !_cts.IsCancellationRequested)
                _cts.Cancel();
        }
    }
}
