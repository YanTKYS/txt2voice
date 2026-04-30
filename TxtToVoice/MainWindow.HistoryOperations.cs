using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using TxtToVoice.Models;

namespace TxtToVoice
{
    public partial class MainWindow
    {
        // ----------------------------------------------------------------
        // フィールド（履歴専用）
        // ----------------------------------------------------------------

        private const int MaxHistoryCount = 20;
        private readonly List<QueueEntry> _history = new();

        // ----------------------------------------------------------------
        // 履歴追加（再生開始時に呼ぶ）
        // ----------------------------------------------------------------

        internal void AddHistoryEntry(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            // 同一テキストが先頭にある場合はスキップ（連続重複防止）
            if (_history.Count > 0 && _history[0].Text == text) return;

            _history.Insert(0, new QueueEntry
            {
                Label     = BuildQueueLabel(text),
                Text      = text,
                CreatedAt = DateTimeOffset.Now
            });
            if (_history.Count > MaxHistoryCount)
                _history.RemoveRange(MaxHistoryCount, _history.Count - MaxHistoryCount);

            RefreshHistoryPanel();
        }

        // ----------------------------------------------------------------
        // 履歴操作ボタン
        // ----------------------------------------------------------------

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            _history.Clear();
            RefreshHistoryPanel();
            SetStatus("履歴をクリアしました。");
        }

        private void BtnRestoreHistory_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstHistory.SelectedIndex;
            if (idx < 0 || idx >= _history.Count) return;
            TxtInput.Text = _history[idx].Text;
            SetStatus($"履歴を原稿に復元しました。（{_history[idx].Label}）");
        }

        private void BtnAddHistoryToQueue_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstHistory.SelectedIndex;
            if (idx < 0 || idx >= _history.Count) return;
            var entry = _history[idx];
            _speechQueue.Add(new QueueEntry
            {
                Label     = entry.Label,
                Text      = entry.Text,
                CreatedAt = DateTimeOffset.Now
            });
            RefreshQueuePanel();
            SaveQueue();
            SetStatus($"履歴をキューに追加しました。（{_speechQueue.Count} 件）");
        }

        private void LstHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool has = LstHistory.SelectedIndex >= 0;
            BtnRestoreHistory.IsEnabled    = has;
            BtnAddHistoryToQueue.IsEnabled = has;
        }

        // ----------------------------------------------------------------
        // パネル更新
        // ----------------------------------------------------------------

        private void RefreshHistoryPanel()
        {
            var labels = new List<string>(_history.Count);
            foreach (var entry in _history)
                labels.Add($"[{entry.CreatedAt:HH:mm}] {entry.Label}");
            LstHistory.ItemsSource = labels;

            BrdHistory.Visibility      = _history.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtHistoryHeader.Text      = $"読み上げ履歴（{_history.Count} 件）";
            BtnRestoreHistory.IsEnabled    = false;
            BtnAddHistoryToQueue.IsEnabled = false;
        }
    }
}
