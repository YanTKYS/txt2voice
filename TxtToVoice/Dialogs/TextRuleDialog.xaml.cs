using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TxtToVoice.Models;
using TxtToVoice.Services;

namespace TxtToVoice.Dialogs
{
    /// <summary>
    /// text_rules.json のルール一覧を DataGrid で表示し、有効/無効を切替・保存するダイアログ。
    /// テスト入力欄で変換結果をリアルタイムプレビューできる（300ms デバウンス）。
    /// </summary>
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

    /// <summary>DataGrid バインディング用ViewModel。Enabled のみ編集可。</summary>
    internal sealed class TextRuleViewModel : INotifyPropertyChanged
    {
        public string Pattern     { get; }
        public string Replacement { get; }
        public string Description { get; }

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public TextRuleViewModel(TextRule rule)
        {
            Pattern     = rule.Pattern;
            Replacement = rule.Replacement;
            Description = rule.Description;
            _enabled    = rule.Enabled;
        }

        public TextRule ToModel() => new()
        {
            Pattern     = Pattern,
            Replacement = Replacement,
            Description = Description,
            Enabled     = Enabled,
        };
    }
}
