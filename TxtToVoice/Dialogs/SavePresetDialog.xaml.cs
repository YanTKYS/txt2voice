using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using TxtToVoice.Models;

namespace TxtToVoice.Dialogs
{
    public partial class SavePresetDialog : Window
    {
        public SavePreset? Result { get; private set; }

        public SavePresetDialog(string defaultTemplate, int defaultSsmlStrength, List<string> defaultBatchFormats)
        {
            InitializeComponent();
            TxtTemplate.Text              = defaultTemplate;
            CmbSsmlStrength.SelectedIndex = Math.Clamp(defaultSsmlStrength, 0, 2);
            ChkMp3.IsChecked = defaultBatchFormats.Contains("mp3");
            ChkWav.IsChecked = defaultBatchFormats.Contains("wav");
            ChkMp4.IsChecked = defaultBatchFormats.Contains("mp4");
        }

        private void RbMode_Checked(object sender, RoutedEventArgs e)
        {
            if (GrpBatchFormats == null) return;
            GrpBatchFormats.Visibility = RbBatch.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (BtnOk == null) return;
            BtnOk.IsEnabled = !string.IsNullOrWhiteSpace(TxtName.Text);
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowError("名前を入力してください。");
                return;
            }

            string mode = RbBatch.IsChecked == true ? "batch" : "single";

            var formats = new List<string>();
            if (ChkMp3.IsChecked == true) formats.Add("mp3");
            if (ChkWav.IsChecked == true) formats.Add("wav");
            if (ChkMp4.IsChecked == true) formats.Add("mp4");

            if (mode == "batch" && formats.Count == 0)
            {
                ShowError("一括保存形式を少なくとも1つ選択してください。");
                return;
            }

            string template = TxtTemplate.Text.Trim();
            if (string.IsNullOrEmpty(template)) template = "{prefix}_{datetime}";

            Result = new SavePreset
            {
                Name             = name,
                SaveMode         = mode,
                FileNameTemplate = template,
                BatchFormats     = formats,
                SsmlStrength     = CmbSsmlStrength.SelectedIndex,
            };
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void ShowError(string msg)
        {
            TxtError.Text       = msg;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}
