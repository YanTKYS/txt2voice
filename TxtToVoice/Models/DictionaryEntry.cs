using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TxtToVoice.Models
{
    /// <summary>
    /// 読み上げ辞書の1エントリ。
    /// 表記 → 読みの変換ルールを保持する。
    /// </summary>
    public class DictionaryEntry : INotifyPropertyChanged
    {
        private string _display = string.Empty;
        private string _reading = string.Empty;
        private string _remarks = string.Empty;
        private int _priority = 50;

        /// <summary>元テキスト内の表記（変換前）</summary>
        [JsonPropertyName("display")]
        public string Display
        {
            get => _display;
            set { _display = value ?? string.Empty; OnPropertyChanged(nameof(Display)); }
        }

        /// <summary>読み仮名（変換後）</summary>
        [JsonPropertyName("reading")]
        public string Reading
        {
            get => _reading;
            set { _reading = value ?? string.Empty; OnPropertyChanged(nameof(Reading)); }
        }

        /// <summary>備考・補足説明</summary>
        [JsonPropertyName("remarks")]
        public string Remarks
        {
            get => _remarks;
            set { _remarks = value ?? string.Empty; OnPropertyChanged(nameof(Remarks)); }
        }

        /// <summary>
        /// 優先順位（1〜100、大きいほど優先）。
        /// 同じ表記長の場合にこの値で優先順位を決定する。
        /// </summary>
        [JsonPropertyName("priority")]
        public int Priority
        {
            get => _priority;
            set { _priority = value; OnPropertyChanged(nameof(Priority)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// 深いコピーを返す（編集ダイアログ用）
        /// </summary>
        public DictionaryEntry Clone() => new()
        {
            Display  = Display,
            Reading  = Reading,
            Remarks  = Remarks,
            Priority = Priority
        };
    }
}
