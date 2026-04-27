using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

            TemplateGrid.ItemsSource = _viewModels;
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
                MessageBox.Show(
                    $"テンプレートの保存に失敗しました。\n\n{ex.Message}",
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

        public string Preview => _content.Length > 60 ? _content[..57] + "…" : _content;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public TemplateViewModel(Template t) { _title = t.Title; _content = t.Content; }

        public Template ToModel() => new() { Title = _title, Content = _content };
    }
}
