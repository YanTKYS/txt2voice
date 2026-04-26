using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using TxtToVoice.Models;
using TxtToVoice.Services;

namespace TxtToVoice.Dialogs
{
    /// <summary>
    /// text_rules.json のルール一覧を DataGrid で表示し、有効/無効を切替・保存するダイアログ。
    /// テスト入力欄で変換結果をリアルタイムプレビューできる。
    /// </summary>
    public partial class TextRuleDialog : Window
    {
        private readonly string _rulesPath;
        private readonly ObservableCollection<TextRuleViewModel> _viewModels = new();

        public TextRuleDialog(string rulesPath)
        {
            _rulesPath = rulesPath;
            InitializeComponent();

            foreach (var rule in TextRuleLoader.LoadRaw(rulesPath))
                _viewModels.Add(new TextRuleViewModel(rule));

            RuleGrid.ItemsSource = _viewModels;
        }

        private void TxtTestInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            string input = TxtTestInput.Text;
            if (string.IsNullOrEmpty(input)) { TxtTestResult.Text = string.Empty; return; }

            string result = input;
            foreach (var vm in _viewModels)
            {
                if (!vm.Enabled || string.IsNullOrEmpty(vm.Pattern)) continue;
                try
                {
                    var rx = new Regex(vm.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(500));
                    result = rx.Replace(result, vm.Replacement);
                }
                catch { /* 無効なパターンまたはタイムアウト — スキップ */ }
            }
            TxtTestResult.Text = result;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var models = new List<TextRule>(_viewModels.Count);
                foreach (var vm in _viewModels)
                    models.Add(vm.ToModel());

                TextRuleLoader.SaveRaw(_rulesPath, models);
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
