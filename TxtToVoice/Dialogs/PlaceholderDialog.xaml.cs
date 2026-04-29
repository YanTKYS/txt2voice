using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace TxtToVoice.Dialogs
{
    public partial class PlaceholderDialog : Window
    {
        // 予約変数 → 現在時刻に基づく初期値ファクトリ (#121)
        private static readonly Dictionary<string, Func<string>> ReservedVariables
            = new(StringComparer.OrdinalIgnoreCase)
            {
                ["today"]   = () => DateTime.Now.ToString("yyyy年M月d日"),
                ["now"]     = () => DateTime.Now.ToString("H:mm"),
                ["month"]   = () => DateTime.Now.ToString("M月"),
                ["year"]    = () => DateTime.Now.ToString("yyyy年"),
                ["weekday"] = () => DateTime.Now.ToString("dddd", CultureInfo.GetCultureInfo("ja-JP")),
            };

        private readonly List<PlaceholderItem> _items;

        public PlaceholderDialog(IEnumerable<string> placeholderNames)
        {
            InitializeComponent();
            _items = placeholderNames.Select(name =>
            {
                string initial = ReservedVariables.TryGetValue(name, out var factory) ? factory() : "";
                return new PlaceholderItem(name, initial);
            }).ToList();
            ItemsPlaceholders.ItemsSource = _items;
        }

        /// <summary>テンプレート文字列に値を適用して返す。</summary>
        public string Apply(string template)
            => _items.Aggregate(template,
                (current, item) => current.Replace($"{{{item.Name}}}", item.Value));

        private void BtnOk_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }

    internal sealed class PlaceholderItem : INotifyPropertyChanged
    {
        private string _value;

        public string Name  { get; }
        public string Label => $"{{{Name}}}";

        public string Value
        {
            get => _value;
            set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public PlaceholderItem(string name, string initialValue = "")
        {
            Name   = name;
            _value = initialValue;
        }
    }
}
