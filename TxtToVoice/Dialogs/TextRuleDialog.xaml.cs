using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TxtToVoice.Models;
using TxtToVoice.Services;

namespace TxtToVoice.Dialogs
{
    public partial class TextRuleDialog : Window
    {
        private readonly string _rulesPath;
        private readonly ObservableCollection<TextRuleViewModel> _viewModels = new();
        private CancellationTokenSource? _previewCts;

        public TextRuleDialog(string rulesPath)
        {
            _rulesPath = rulesPath;
            InitializeComponent();

            foreach (var rule in TextRuleLoader.LoadRaw(rulesPath))
                _viewModels.Add(new TextRuleViewModel(rule));

            RuleGrid.ItemsSource = _viewModels;
            TxtRulesPathLabel.Text = $"設定ファイル: {rulesPath}";

            // ダイアログ終了時に必ず CTS をクリーンアップする (#72)
            Closed += (_, _) => { _previewCts?.Cancel(); _previewCts?.Dispose(); _previewCts = null; };
        }

        // ── ツールバー ──────────────────────────────────────────

        private void RuleGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = RuleGrid.SelectedItem != null;
            BtnEditRule.IsEnabled    = hasSelection;
            BtnDeleteRule.IsEnabled  = hasSelection;
            BtnMoveRuleUp.IsEnabled  = hasSelection;
            BtnMoveRuleDown.IsEnabled = hasSelection;
        }

        private void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new TextRuleEntryDialog { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;

            var vm = new TextRuleViewModel(dlg.Result);
            _viewModels.Add(vm);
            RuleGrid.SelectedItem = vm;
            RuleGrid.ScrollIntoView(vm);
        }

        private void BtnEditRule_Click(object sender, RoutedEventArgs e)
        {
            int idx = GetSelectedRuleIndex();
            if (idx < 0) return;

            var vm  = _viewModels[idx];
            var dlg = new TextRuleEntryDialog(vm.ToModel()) { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;

            vm.Pattern     = dlg.Result.Pattern;
            vm.Replacement = dlg.Result.Replacement;
            vm.Description = dlg.Result.Description;
            vm.Enabled     = dlg.Result.Enabled;
        }

        private void BtnDeleteRule_Click(object sender, RoutedEventArgs e)
        {
            int idx = GetSelectedRuleIndex();
            if (idx < 0) return;

            _viewModels.RemoveAt(idx);
            if (_viewModels.Count > 0)
                RuleGrid.SelectedIndex = Math.Min(idx, _viewModels.Count - 1);
        }

        private void BtnMoveRuleUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = GetSelectedRuleIndex();
            if (idx <= 0) return;
            _viewModels.Move(idx, idx - 1);
            RuleGrid.SelectedIndex = idx - 1;
            RuleGrid.ScrollIntoView(_viewModels[idx - 1]);
        }

        private void BtnMoveRuleDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = GetSelectedRuleIndex();
            if (idx < 0 || idx >= _viewModels.Count - 1) return;
            _viewModels.Move(idx, idx + 1);
            RuleGrid.SelectedIndex = idx + 1;
            RuleGrid.ScrollIntoView(_viewModels[idx + 1]);
        }

        private void RuleGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift)) return;
            if (e.Key == Key.Up)
            {
                BtnMoveRuleUp_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                BtnMoveRuleDown_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private int GetSelectedRuleIndex()
        {
            if (RuleGrid.SelectedItem is not TextRuleViewModel selected) return -1;
            for (int i = 0; i < _viewModels.Count; i++)
                if (ReferenceEquals(_viewModels[i], selected)) return i;
            return -1;
        }

        // ── テスト入力プレビュー ────────────────────────────────

        private void TxtTestInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            _previewCts?.Cancel();
            _previewCts?.Dispose();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            string input = TxtTestInput.Text;
            if (string.IsNullOrEmpty(input))
            {
                TxtTestResult.Text  = string.Empty;
                TxtDiagnostic.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }

            // スナップショットを取ってバックグラウンドで評価し、300ms デバウンス
            var snapshot = new List<(string Pattern, string Replacement, bool Enabled)>(_viewModels.Count);
            foreach (var vm in _viewModels)
                snapshot.Add((vm.Pattern, vm.Replacement, vm.Enabled));

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token).ConfigureAwait(false);

                    string result = input;
                    var skipped = new List<string>();
                    foreach (var (pattern, replacement, enabled) in snapshot)
                    {
                        if (!enabled || string.IsNullOrEmpty(pattern)) continue;
                        try
                        {
                            var rx = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(500));
                            result = rx.Replace(result, replacement);
                        }
                        catch { skipped.Add(pattern); }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        TxtTestResult.Text = result;
                        if (skipped.Count > 0)
                        {
                            TxtDiagnostic.Text = skipped.Count == 1
                                ? $"パターンエラーによりスキップ: 「{skipped[0]}」"
                                : $"パターンエラーによりスキップ: {skipped.Count}件（「{skipped[0]}」ほか）";
                            TxtDiagnostic.Visibility = System.Windows.Visibility.Visible;
                        }
                        else
                        {
                            TxtDiagnostic.Visibility = System.Windows.Visibility.Collapsed;
                        }
                    });
                }
                catch (OperationCanceledException) { /* デバウンスキャンセル — 無視 */ }
            }, token);
        }

        // ── 保存 / キャンセル ───────────────────────────────────

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var models = new List<TextRule>(_viewModels.Count);
                foreach (var vm in _viewModels)
                    models.Add(vm.ToModel());

                // EXE 配置先が書き込み不可のとき DataDirectory 下にフォールバック
                string savePath = _rulesPath;
                try
                {
                    TextRuleLoader.SaveRaw(savePath, models);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is System.IO.IOException)
                {
                    savePath = PathConfig.UserTextRulesPath;
                    TextRuleLoader.SaveRaw(savePath, models);
                    Logger.Warn($"読みルール: EXE 配下への書き込み不可、DataDirectory に保存: {savePath}");
                }

                Logger.Info($"読みルール保存: {models.Count}件");
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"保存に失敗しました。\n\n{ex.Message}",
                    "保存エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }

    /// <summary>DataGrid バインディング用ViewModel。全フィールド編集可。</summary>
    internal sealed class TextRuleViewModel : INotifyPropertyChanged
    {
        private string _pattern;
        private string _replacement;
        private string _description;
        private bool   _enabled;

        public string Pattern
        {
            get => _pattern;
            set { _pattern = value; OnPropertyChanged(nameof(Pattern)); }
        }

        public string Replacement
        {
            get => _replacement;
            set { _replacement = value; OnPropertyChanged(nameof(Replacement)); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(nameof(Description)); }
        }

        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(nameof(Enabled)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public TextRuleViewModel(TextRule rule)
        {
            _pattern     = rule.Pattern;
            _replacement = rule.Replacement;
            _description = rule.Description;
            _enabled     = rule.Enabled;
        }

        public TextRule ToModel() => new()
        {
            Pattern     = _pattern,
            Replacement = _replacement,
            Description = _description,
            Enabled     = _enabled,
        };
    }
}
