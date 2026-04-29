using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TxtToVoice
{
    public partial class MainWindow
    {
        // ----------------------------------------------------------------
        // フィールド（表示設定）
        // ----------------------------------------------------------------

        private bool _showLineNumbers;
        private ScrollViewer? _inputScrollViewer;

        // ----------------------------------------------------------------
        // プレビュー比較ペイン (#123)
        // ----------------------------------------------------------------

        private bool _compareMode;
        private bool _syncingScroll;
        private ScrollViewer? _svCompareLeft;
        private ScrollViewer? _svPreview;

        private void ChkCompare_Changed(object sender, RoutedEventArgs e)
            => ToggleCompareMode(ChkCompare.IsChecked == true);

        internal void ToggleCompareMode(bool on)
        {
            _compareMode = on;
            var star    = new GridLength(1, GridUnitType.Star);
            var zero    = new GridLength(0);
            var five    = new GridLength(5);

            GrdPreviewArea.ColumnDefinitions[0].Width = on ? star : zero;
            GrdPreviewArea.ColumnDefinitions[1].Width = on ? five : zero;

            if (on)
            {
                TxtCompareLeft.Text = TxtInput.Text;
                EnsureCompareScrollHook();
            }
        }

        private void EnsureCompareScrollHook()
        {
            if (_svCompareLeft is not null && _svPreview is not null) return;

            TxtCompareLeft.ApplyTemplate();
            TxtPreview.ApplyTemplate();
            _svCompareLeft = FindScrollViewer(TxtCompareLeft);
            _svPreview     = FindScrollViewer(TxtPreview);

            if (_svCompareLeft is not null)
                _svCompareLeft.ScrollChanged += OnCompareLeftScrollChanged;
            if (_svPreview is not null)
                _svPreview.ScrollChanged += OnPreviewScrollChanged;
        }

        private void OnCompareLeftScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_syncingScroll || e.VerticalChange == 0) return;
            _syncingScroll = true;
            _svPreview?.ScrollToVerticalOffset(e.VerticalOffset);
            _syncingScroll = false;
        }

        private void OnPreviewScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_syncingScroll || !_compareMode || e.VerticalChange == 0) return;
            _syncingScroll = true;
            _svCompareLeft?.ScrollToVerticalOffset(e.VerticalOffset);
            _syncingScroll = false;
        }

        // ----------------------------------------------------------------
        // 行番号表示 (#124)
        // ----------------------------------------------------------------

        internal void ToggleLineNumbers(bool show)
        {
            _showLineNumbers = show;
            BrdLineNumbers.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                EnsureScrollViewerHook();
                ScheduleLineNumberRefresh();
            }
        }

        private void EnsureScrollViewerHook()
        {
            if (_inputScrollViewer is not null) return;
            TxtInput.ApplyTemplate();
            _inputScrollViewer = FindScrollViewer(TxtInput);
            if (_inputScrollViewer is not null)
                _inputScrollViewer.ScrollChanged += (_, _) => RefreshLineNumbers();
        }

        // TextChanged では レイアウトパスが完了してから描画する
        private void ScheduleLineNumberRefresh()
            => Dispatcher.InvokeAsync(RefreshLineNumbers, DispatcherPriority.Render);

        private void RefreshLineNumbers()
        {
            if (!_showLineNumbers) return;
            if (!TxtInput.IsLoaded || CvsLineNumbers.ActualHeight <= 0) return;

            string text = TxtInput.Text;
            CvsLineNumbers.Children.Clear();
            if (text.Length == 0) return;

            // 各論理行（\n 区切り）の先頭文字インデックスを収集
            var lineStarts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n')
                    lineStarts.Add(i + 1);

            double canvasH   = CvsLineNumbers.ActualHeight;
            double fontSize  = TxtInput.FontSize - 1;
            double labelW    = BrdLineNumbers.Width - 6; // 左右 3px マージン

            int lineNum = 1;
            foreach (int charIdx in lineStarts)
            {
                // text.Length を超えない安全なインデックスに丸める
                int safeIdx = Math.Min(charIdx, text.Length);

                Rect r;
                try { r = TxtInput.GetRectFromCharacterIndex(safeIdx); }
                catch { lineNum++; continue; }

                double y = r.Y;
                // ビューポート外（上下）はスキップ
                if (y < -fontSize || y > canvasH) { lineNum++; continue; }

                var tb = new TextBlock
                {
                    Text           = lineNum.ToString(),
                    FontFamily     = TxtInput.FontFamily,
                    FontSize       = fontSize,
                    Foreground     = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                    Width          = labelW,
                    TextAlignment  = TextAlignment.Right,
                };
                Canvas.SetTop(tb,  y);
                Canvas.SetLeft(tb, 3);
                CvsLineNumbers.Children.Add(tb);
                lineNum++;
            }
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject parent)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer sv) return sv;
                var result = FindScrollViewer(child);
                if (result is not null) return result;
            }
            return null;
        }
    }
}
