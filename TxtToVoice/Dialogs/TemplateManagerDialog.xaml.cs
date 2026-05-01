using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TxtToVoice.Models;
using TxtToVoice.Services;

namespace TxtToVoice.Dialogs
{
    public partial class TemplateManagerDialog : Window
    {
        private readonly string _templatesPath;
        private readonly ObservableCollection<TemplateViewModel> _viewModels = new();

        /// <summary>「挿入して閉じる」で選ばれたテンプレートの内容（DialogResult = true のとき有効）。</summary>
        public string? Result { get; private set; }

        public TemplateManagerDialog(string templatesPath)
        {
            _templatesPath = templatesPath;
            InitializeComponent();

            foreach (var t in TemplateService.Load(templatesPath))
                _viewModels.Add(new TemplateViewModel(t));

            var view = CollectionViewSource.GetDefaultView(_viewModels);
            view.Filter = FilterTemplate;
            TemplateGrid.ItemsSource = view;

            // デフォルトは「最近使った順」
            CmbSortOrder.SelectedIndex = 0;
            ApplySort();
        }

        // ── 検索フィルター (#109) ──────────────────────────────

        private bool FilterTemplate(object obj)
        {
            if (obj is not TemplateViewModel vm) return true;
            string filter = TxtFilter.Text.Trim();
            if (filter.Length == 0) return true;
            return vm.Title.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                || vm.Content.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
            => CollectionViewSource.GetDefaultView(_viewModels).Refresh();

        private void BtnFilterClear_Click(object sender, RoutedEventArgs e)
            => TxtFilter.Clear();

        // ── ソート切替 (#120) ─────────────────────────────────

        private void CmbSortOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ItemsSource 未設定の初期化タイミングは無視
            if (TemplateGrid?.ItemsSource == null) return;
            ApplySort();
        }

        private void ApplySort()
        {
            var view = CollectionViewSource.GetDefaultView(_viewModels);
            view.SortDescriptions.Clear();
            // ピン留め項目を常に先頭グループに固定
            view.SortDescriptions.Add(new SortDescription(nameof(TemplateViewModel.IsPinnedSortKey), ListSortDirection.Descending));
            switch (CmbSortOrder.SelectedIndex)
            {
                case 0: // 最近使った順（未使用は末尾）
                    view.SortDescriptions.Add(new SortDescription(nameof(TemplateViewModel.LastUsedAtSortKey), ListSortDirection.Descending));
                    view.SortDescriptions.Add(new SortDescription(nameof(TemplateViewModel.Title), ListSortDirection.Ascending));
                    break;
                case 1: // タイトル順
                    view.SortDescriptions.Add(new SortDescription(nameof(TemplateViewModel.Title), ListSortDirection.Ascending));
                    break;
                case 2: // 使用回数順
                    view.SortDescriptions.Add(new SortDescription(nameof(TemplateViewModel.UsageCount), ListSortDirection.Descending));
                    view.SortDescriptions.Add(new SortDescription(nameof(TemplateViewModel.Title), ListSortDirection.Ascending));
                    break;
            }
            view.Refresh();
        }

        private void BtnPinCell_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.DataContext is not TemplateViewModel vm) return;
            vm.TogglePin();
            AutoSave();
            CollectionViewSource.GetDefaultView(_viewModels).Refresh();
        }

        // ── 選択変更 ──────────────────────────────────────────

        private void TemplateGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = TemplateGrid.SelectedItem != null;
            BtnEditTemplate.IsEnabled   = hasSelection;
            BtnDeleteTemplate.IsEnabled = hasSelection;
            BtnInsert.IsEnabled         = hasSelection;
        }

        private void TemplateGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TemplateGrid.SelectedItem == null) return;
            BtnInsert_Click(sender, e);
        }

        // ── ツールバー ────────────────────────────────────────

        private void BtnAddTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new TemplateEntryDialog { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;

            var vm = new TemplateViewModel(dlg.Result);
            _viewModels.Add(vm);
            TemplateGrid.SelectedItem = vm;
            TemplateGrid.ScrollIntoView(vm);
            AutoSave();
        }

        private void BtnEditTemplate_Click(object sender, RoutedEventArgs e)
        {
            int idx = GetSelectedIndex();
            if (idx < 0) return;

            var vm  = _viewModels[idx];
            var dlg = new TemplateEntryDialog(vm.ToModel()) { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;

            vm.Title   = dlg.Result.Title;
            vm.Content = dlg.Result.Content;
            AutoSave();
        }

        private void BtnDeleteTemplate_Click(object sender, RoutedEventArgs e)
        {
            int idx = GetSelectedIndex();
            if (idx < 0) return;

            _viewModels.RemoveAt(idx);
            if (_viewModels.Count > 0)
                TemplateGrid.SelectedIndex = Math.Min(idx, _viewModels.Count - 1);
            AutoSave();
        }

        // ── 挿入 / 閉じる ─────────────────────────────────────

        private void BtnInsert_Click(object sender, RoutedEventArgs e)
        {
            int idx = GetSelectedIndex();
            if (idx < 0) return;

            // 利用履歴を更新して保存 (#120)
            _viewModels[idx].RecordUsage();
            AutoSave();

            Result = _viewModels[idx].Content;
            DialogResult = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        // ── ヘルパー ──────────────────────────────────────────

        private int GetSelectedIndex()
        {
            if (TemplateGrid.SelectedItem is not TemplateViewModel selected) return -1;
            for (int i = 0; i < _viewModels.Count; i++)
                if (ReferenceEquals(_viewModels[i], selected)) return i;
            return -1;
        }

        private void AutoSave()
        {
            try
            {
                TemplateService.Save(_templatesPath, _viewModels.Select(vm => vm.ToModel()));
                Logger.Info($"テンプレート保存: {_viewModels.Count}件");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{TtvErrorCode.IoTemplateSaveFailed}] テンプレート保存失敗: {ex.Message}");
                MessageBox.Show(
                    $"[{TtvErrorCode.IoTemplateSaveFailed}] テンプレートの保存に失敗しました。\n\n{ex.Message}",
                    "保存エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    internal sealed class TemplateViewModel : INotifyPropertyChanged
    {
        private string _title;
        private string _content;
        private DateTimeOffset? _lastUsedAt;
        private int _usageCount;
        private bool _isPinned;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(nameof(Content)); OnPropertyChanged(nameof(Preview)); }
        }

        public DateTimeOffset? LastUsedAt
        {
            get => _lastUsedAt;
            private set
            {
                _lastUsedAt = value;
                OnPropertyChanged(nameof(LastUsedAt));
                OnPropertyChanged(nameof(LastUsedAtSortKey));
                OnPropertyChanged(nameof(LastUsedDisplay));
            }
        }

        public int UsageCount
        {
            get => _usageCount;
            private set { _usageCount = value; OnPropertyChanged(nameof(UsageCount)); }
        }

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                _isPinned = value;
                OnPropertyChanged(nameof(IsPinned));
                OnPropertyChanged(nameof(PinIcon));
                OnPropertyChanged(nameof(IsPinnedSortKey));
            }
        }

        public string PinIcon       => _isPinned ? "★" : "☆";
        public int    IsPinnedSortKey => _isPinned ? 1 : 0;

        public void TogglePin() => IsPinned = !_isPinned;

        // ソート用キー: null（未使用）は DateTimeOffset.MinValue 扱いで末尾に
        public DateTimeOffset LastUsedAtSortKey => _lastUsedAt ?? DateTimeOffset.MinValue;

        public string LastUsedDisplay => _lastUsedAt.HasValue
            ? _lastUsedAt.Value.LocalDateTime.ToString("M/d HH:mm")
            : "—";

        public string Preview
        {
            get
            {
                var c = _content ?? string.Empty;
                return c.Length > 60 ? c[..57] + "…" : c;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public TemplateViewModel(Template t)
        {
            _title      = t.Title   ?? string.Empty;
            _content    = t.Content ?? string.Empty;
            _lastUsedAt = t.LastUsedAt;
            _usageCount = t.UsageCount;
            _isPinned   = t.IsPinned;
        }

        public void RecordUsage()
        {
            LastUsedAt = DateTimeOffset.Now;
            UsageCount = _usageCount + 1;
        }

        public Template ToModel() => new()
        {
            Title      = _title,
            Content    = _content,
            LastUsedAt = _lastUsedAt,
            UsageCount = _usageCount,
            IsPinned   = _isPinned,
        };
    }
}
