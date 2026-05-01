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
            if (_speechQueue.Exists(e2 => e2.Text == text))
            {
                SetStatus("同じテキストがすでにキューにあります（スキップ）。");
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

        private void LstQueue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selCount = LstQueue.SelectedItems.Count;
            BtnQueueDelete.IsEnabled   = selCount > 0;
            BtnQueueMoveUp.IsEnabled   = selCount == 1 && LstQueue.SelectedIndex > 0;
            BtnQueueMoveDown.IsEnabled = selCount == 1 && LstQueue.SelectedIndex < _speechQueue.Count - 1;
        }

        private void BtnQueueDelete_Click(object sender, RoutedEventArgs e)
        {
            var toRemove = new List<QueueEntry>();
            foreach (var item in LstQueue.SelectedItems)
                if (item is QueueEntry qe) toRemove.Add(qe);
            if (toRemove.Count == 0) return;
            foreach (var qe in toRemove) _speechQueue.Remove(qe);
            RefreshQueuePanel();
            SaveQueue();
            SetStatus($"選択した {toRemove.Count} 件を削除しました。（残り {_speechQueue.Count} 件）");
        }

        private void BtnQueueMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstQueue.SelectedIndex;
            if (idx <= 0) return;
            (_speechQueue[idx - 1], _speechQueue[idx]) = (_speechQueue[idx], _speechQueue[idx - 1]);
            RefreshQueuePanel();
            SaveQueue();
            LstQueue.SelectedIndex = idx - 1;
        }

        private void BtnQueueMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstQueue.SelectedIndex;
            if (idx < 0 || idx >= _speechQueue.Count - 1) return;
            (_speechQueue[idx], _speechQueue[idx + 1]) = (_speechQueue[idx + 1], _speechQueue[idx]);
            RefreshQueuePanel();
            SaveQueue();
            LstQueue.SelectedIndex = idx + 1;
        }

        private void LstQueue_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                BtnQueueDelete_Click(sender, e);
                e.Handled = true;
            }
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
                    AddHistoryEntry(entry.Text);

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
            catch (Exception ex) { Logger.Error($"[{TtvErrorCode.IoQueueSaveFailed}] キュー保存エラー: {ex.Message}"); }
        }

        // ----------------------------------------------------------------
        // ヘルパー
        // ----------------------------------------------------------------

        private void RefreshQueuePanel()
        {
            LstQueue.ItemsSource = null;
            LstQueue.ItemsSource = _speechQueue;

            BrdQueue.Visibility    = _speechQueue.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtQueueHeader.Text    = $"読み上げキュー（{_speechQueue.Count} 件）";
            BtnPlayQueue.IsEnabled = _speechQueue.Count > 0;

            BtnQueueDelete.IsEnabled   = false;
            BtnQueueMoveUp.IsEnabled   = false;
            BtnQueueMoveDown.IsEnabled = false;
        }

        private static string BuildQueueLabel(string text)
        {
            const int MaxLen = 40;
            string flat = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return flat.Length <= MaxLen ? flat : flat.Substring(0, MaxLen) + "…";
        }
    }
}
