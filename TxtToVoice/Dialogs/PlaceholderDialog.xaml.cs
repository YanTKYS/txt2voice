using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace TxtToVoice.Dialogs
{
    public partial class PlaceholderDialog : Window
    {
        private readonly List<PlaceholderItem> _items;

        public PlaceholderDialog(IEnumerable<string> placeholderNames)
        {
            InitializeComponent();
            _items = placeholderNames.Select(name => new PlaceholderItem(name)).ToList();
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
        private string _value = string.Empty;

        public string Name  { get; }
        public string Label => $"{{{Name}}}";

        public string Value
        {
            get => _value;
            set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public PlaceholderItem(string name) => Name = name;
    }
}
