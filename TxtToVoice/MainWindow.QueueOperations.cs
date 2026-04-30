using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TxtToVoice.Models;
using TxtToVoice.Services;

namespace TxtToVoice
{
    public partial class MainWindow
    {
        // ----------------------------------------------------------------
        // フィールド（キュー専用）
        // ----------------------------------------------------------------

        private readonly List<QueueEntry> _speechQueue = new();
        private CancellationTokenSource? _queueCts;
        private bool _queuePlaying;
        private bool _persistQueue;

        // ----------------------------------------------------------------
        // キュー操作ボタン
        // ----------------------------------------------------------------

        private void BtnAddToQueue_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtInput.SelectionLength > 0 ? TxtInput.SelectedText : TxtInput.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus("追加するテキストがありません。");
                return;
            }
            _speechQueue.Add(new QueueEntry
            {
                Label     = BuildQueueLabel(text),
                Text      = text,
                CreatedAt = DateTimeOffset.Now
            });
            RefreshQueuePanel();
            SaveQueue();
            SetStatus($"キューに追加しました。（{_speechQueue.Count} 件）");
        }

        private void BtnClearQueue_Click(object sender, RoutedEventArgs e)
        {
            _queueCts?.Cancel();
            _speechQueue.Clear();
            RefreshQueuePanel();
            SaveQueue();
            SetStatus("キューをクリアしました。");
        }

        private async void BtnPlayQueue_Click(object sender, RoutedEventArgs e)
        {
            if (_speechQueue.Count == 0 || _playback.IsSpeaking || _queuePlaying) return;
            await PlayQueueAsync();
        }

        private void BtnStopQueue_Click(object sender, RoutedEventArgs e)
        {
            _queueCts?.Cancel();
            _speechService.Stop();
        }

        // ----------------------------------------------------------------
        // 順次再生
        // ----------------------------------------------------------------

        private async Task PlayQueueAsync()
        {
            _queuePlaying = true;
            _queueCts?.Dispose();
            _queueCts = new CancellationTokenSource();
            var ct = _queueCts.Token;

            BtnPlayQueue.Visibility  = Visibility.Collapsed;
            BtnStopQueue.Visibility  = Visibility.Visible;
            BtnAddToQueue.IsEnabled  = false;
            UpdatePlaybackButtons();

            int total = _speechQueue.Count;
            try
            {
                for (int i = 0; i < _speechQueue.Count && !ct.IsCancellationRequested; i++)
                {
                    var entry = _speechQueue[i];
                    LstQueue.SelectedIndex = i;
                    LstQueue.ScrollIntoView(LstQueue.SelectedItem);
                    SetStatus($"キュー再生中... ({i + 1}/{total}): {entry.Label}");

                    var (speechText, _) = _dictService.ApplyDictionaryForSpeech(entry.Text);
                    bool useSsml = ChkSsml.IsChecked == true;
                    string content = useSsml
                        ? SsmlBuilder.Build(speechText, CmbSsmlStrength.SelectedIndex)
                        : speechText;

                    try
                    {
                        if (useSsml) await _speechService.SpeakSsmlAsync(content, ct);
                        else         await _speechService.SpeakAsync(content, ct);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _playback = PlaybackState.Idle;
                        ClearReadingHighlight();
                        UpdatePlaybackButtons();
                        Logger.Error($"キュー再生エラー (item {i + 1}/{total}): {ex.Message}");
                        var ans = MessageBox.Show(
                            $"アイテム {i + 1}/{total} の読み上げでエラーが発生しました。\n{ex.Message}\n\n次のアイテムに進みますか？",
                            "キュー再生エラー", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (ans != MessageBoxResult.Yes) break;
                        continue;
                    }

                    _playback = PlaybackState.Idle;
                    ClearReadingHighlight();
                    UpdatePlaybackButtons();
                }
            }
            finally
            {
                _queuePlaying    = false;
                _playback        = PlaybackState.Idle;
                ClearReadingHighlight();
                BtnPlayQueue.Visibility = Visibility.Visible;
                BtnStopQueue.Visibility = Visibility.Collapsed;
                BtnAddToQueue.IsEnabled = true;
                LstQueue.SelectedIndex  = -1;
                UpdatePlaybackButtons();
                SetStatus(ct.IsCancellationRequested
                    ? "キュー再生を停止しました。"
                    : $"キュー再生完了。（{total} 件）");
            }
        }

        // ----------------------------------------------------------------
        // 永続化
        // ----------------------------------------------------------------

        internal void LoadQueue()
        {
            if (!_persistQueue) return;
            var loaded = QueuePersistenceService.Load(PathConfig.QueuePath);
            _speechQueue.Clear();
            _speechQueue.AddRange(loaded);
            RefreshQueuePanel();
        }

        private void SaveQueue()
        {
            if (!_persistQueue) return;
            try { QueuePersistenceService.Save(PathConfig.QueuePath, _speechQueue); }
            catch (Exception ex) { Logger.Error($"キュー保存エラー: {ex.Message}"); }
        }

        // ----------------------------------------------------------------
        // ヘルパー
        // ----------------------------------------------------------------

        private void RefreshQueuePanel()
        {
            var labels = new List<string>(_speechQueue.Count);
            foreach (var entry in _speechQueue)
                labels.Add(entry.Label);
            LstQueue.ItemsSource = labels;

            BrdQueue.Visibility    = _speechQueue.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtQueueHeader.Text    = $"読み上げキュー（{_speechQueue.Count} 件）";
            BtnPlayQueue.IsEnabled = _speechQueue.Count > 0;
        }

        private static string BuildQueueLabel(string text)
        {
            const int MaxLen = 40;
            string flat = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return flat.Length <= MaxLen ? flat : flat.Substring(0, MaxLen) + "…";
        }
    }
}
