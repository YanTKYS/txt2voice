using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TxtToVoice.Dialogs;
using TxtToVoice.Models;
using TxtToVoice.Services;

namespace TxtToVoice
{
    /// <summary>
    /// 声の広報 テキスト読み上げツール — メインウィンドウ。
    /// </summary>
    public partial class MainWindow : Window
    {
        // ----------------------------------------------------------------
        // フィールド
        // ----------------------------------------------------------------

        private readonly SpeechService    _speechService;
        private readonly DictionaryService _dictService;
        private readonly ObservableCollection<DictionaryEntry> _entries = new();

        private static readonly string DictionaryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TxtToVoice", "dictionary.json");

        private static readonly string SampleDictionaryPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "sample_dictionary.json");

        // 再生状態フラグ
        private bool _isSpeaking  = false;
        private bool _isPaused    = false;

        // プレビューモード: true=アノテーション付き, false=クリーン
        private bool _annotatedPreview = true;

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        public MainWindow()
        {
            InitializeComponent();

            Logger.Info("アプリケーション起動");

            _speechService = new SpeechService();
            _dictService   = new DictionaryService(DictionaryPath);

            // DataGrid のデータソースを設定
            DgDictionary.ItemsSource = _entries;

            // 音声合成イベント
            _speechService.SpeakStarted   += OnSpeakStarted;
            _speechService.SpeakCompleted += OnSpeakCompleted;
            _speechService.SpeakError     += OnSpeakError;

            // 初期化
            InitializeVoiceCombo();
            LoadDictionary();
            SetStatus("準備完了。原稿を入力して「辞書を適用してプレビュー更新」を押してください。");
        }

        // ----------------------------------------------------------------
        // 初期化
        // ----------------------------------------------------------------

        private void InitializeVoiceCombo()
        {
            CmbVoice.ItemsSource = _speechService.GetAvailableVoices().ToList();
            if (CmbVoice.Items.Count > 0)
                CmbVoice.SelectedIndex = 0;
            else
                SetStatus("利用可能な音声エンジンが見つかりません。Windows の音声設定を確認してください。");
        }

        private void LoadDictionary()
        {
            try
            {
                _dictService.Load();

                // 辞書が空ならサンプルを自動読み込み
                if (_dictService.Entries.Count == 0 && File.Exists(SampleDictionaryPath))
                {
                    int added = _dictService.LoadSampleDictionary(SampleDictionaryPath);
                    if (added > 0)
                    {
                        _dictService.Save();
                        SetStatus($"サンプル辞書 {added} 件を自動読み込みしました。");
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                MessageBox.Show(
                    $"辞書ファイルの読み込みに失敗しました。\n\n{ex.Message}",
                    "辞書読み込みエラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Logger.Error($"辞書読み込み失敗: {ex.Message}");
            }
            finally
            {
                RefreshDictionaryList();
            }
        }

        private void RefreshDictionaryList()
        {
            _entries.Clear();
            foreach (var e in _dictService.Entries)
                _entries.Add(e);
            TxtDictCount.Text = $"辞書: {_entries.Count} 件";
        }

        // ----------------------------------------------------------------
        // キーボードショートカット
        // ----------------------------------------------------------------

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Ctrl 系
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.O: OpenFile();           e.Handled = true; break;
                    case Key.P: ApplyAndPreview();    e.Handled = true; break;
                    case Key.S: SaveWav();            e.Handled = true; break;
                }
                return;
            }

            // ファンクションキー
            switch (e.Key)
            {
                case Key.F5: StartSpeech();  e.Handled = true; break;
                case Key.F6: PauseSpeech();  e.Handled = true; break;
                case Key.F7: ResumeSpeech(); e.Handled = true; break;
                case Key.F8: StopSpeech();   e.Handled = true; break;
            }
        }

        // ----------------------------------------------------------------
        // ファイル操作
        // ----------------------------------------------------------------

        private void MenuOpenFile_Click(object sender, RoutedEventArgs e) => OpenFile();
        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)  => OpenFile();

        private void OpenFile()
        {
            var dlg = new OpenFileDialog
            {
                Title  = "テキストファイルを開く",
                Filter = "テキストファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*",
                DefaultExt = "txt"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                // エンコード自動判別（BOM → UTF-8 → Shift_JIS）
                string text = ReadTextFileWithFallback(dlg.FileName);
                TxtInput.Text = text;
                SetStatus($"ファイルを読み込みました: {Path.GetFileName(dlg.FileName)}");
                Logger.Info($"ファイル読み込み: {dlg.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"ファイルの読み込みに失敗しました。\n\n{ex.Message}",
                    "読み込みエラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Logger.Error($"ファイル読み込みエラー: {dlg.FileName} / {ex.Message}");
            }
        }

        private static string ReadTextFileWithFallback(string path)
        {
            // BOM 付き UTF-8 はそのまま読める
            try
            {
                return File.ReadAllText(path, new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true));
            }
            catch (DecoderFallbackException) { }

            // Shift_JIS を試みる
            try
            {
                return File.ReadAllText(path, System.Text.Encoding.GetEncoding("shift_jis"));
            }
            catch
            {
                // 最後は UTF-8（エラーを無視）
                return File.ReadAllText(path, System.Text.Encoding.UTF8);
            }
        }

        private void BtnClearText_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtInput.Text)) return;
            var result = MessageBox.Show(
                "入力テキストをすべて消去しますか？",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                TxtInput.Clear();
                TxtPreview.Clear();
                SetStatus("テキストをクリアしました。");
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

        // ----------------------------------------------------------------
        // テキスト入力
        // ----------------------------------------------------------------

        private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            int len = TxtInput.Text.Length;
            TxtCharCount.Text = $"{len:N0} 文字";
        }

        // ----------------------------------------------------------------
        // 辞書適用・プレビュー
        // ----------------------------------------------------------------

        private void BtnApplyDictionary_Click(object sender, RoutedEventArgs e) => ApplyAndPreview();

        private void ApplyAndPreview()
        {
            string input = TxtInput.Text;
            if (string.IsNullOrWhiteSpace(input))
            {
                SetStatus("プレビューするテキストがありません。");
                return;
            }

            if (_annotatedPreview)
                TxtPreview.Text = _dictService.ApplyDictionaryWithAnnotation(input);
            else
                TxtPreview.Text = _dictService.ApplyDictionary(input);

            SetStatus("辞書を適用してプレビューを更新しました。");
        }

        private void PreviewMode_Changed(object sender, RoutedEventArgs e)
        {
            _annotatedPreview = RbPreviewAnnotated.IsChecked == true;
            if (!string.IsNullOrEmpty(TxtInput.Text))
                ApplyAndPreview();
        }

        // ----------------------------------------------------------------
        // 読み上げ
        // ----------------------------------------------------------------

        private void BtnPlay_Click(object  sender, RoutedEventArgs e) => StartSpeech();
        private void BtnPause_Click(object sender, RoutedEventArgs e) => PauseSpeech();
        private void BtnResume_Click(object sender, RoutedEventArgs e) => ResumeSpeech();
        private void BtnStop_Click(object  sender, RoutedEventArgs e) => StopSpeech();

        private void StartSpeech()
        {
            // 読み上げテキストの決定:
            // 選択範囲があれば選択範囲のみ、なければ全テキストに辞書を適用
            string rawText;
            bool hasSelection = TxtInput.SelectionLength > 0;

            if (hasSelection)
                rawText = TxtInput.SelectedText;
            else
                rawText = TxtInput.Text;

            if (string.IsNullOrWhiteSpace(rawText))
            {
                MessageBox.Show("読み上げるテキストがありません。",
                    "読み上げ", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string speechText = _dictService.ApplyDictionary(rawText);
            _speechService.SpeakAsync(speechText);
        }

        private void PauseSpeech()
        {
            if (_isSpeaking && !_isPaused)
            {
                _speechService.Pause();
                _isPaused = true;
                UpdatePlaybackButtons();
                SetStatus("一時停止中。「再開」で読み上げを続けます。");
            }
        }

        private void ResumeSpeech()
        {
            if (_isSpeaking && _isPaused)
            {
                _speechService.Resume();
                _isPaused = false;
                UpdatePlaybackButtons();
                SetStatus("読み上げ再開中...");
            }
        }

        private void StopSpeech()
        {
            _speechService.Stop();
            // SpeakCompleted イベントで状態リセットが行われる
        }

        // ----------------------------------------------------------------
        // 音声合成イベント（UI スレッドで呼ばれる）
        // ----------------------------------------------------------------

        private void OnSpeakStarted(object? sender, EventArgs e)
        {
            _isSpeaking = true;
            _isPaused   = false;
            UpdatePlaybackButtons();
            SetStatus("読み上げ中...");
        }

        private void OnSpeakCompleted(object? sender, EventArgs e)
        {
            _isSpeaking = false;
            _isPaused   = false;
            UpdatePlaybackButtons();
            SetStatus("読み上げ完了。");
        }

        private void OnSpeakError(object? sender, string message)
        {
            _isSpeaking = false;
            _isPaused   = false;
            UpdatePlaybackButtons();
            MessageBox.Show(
                $"読み上げ中にエラーが発生しました。\n\n{message}",
                "読み上げエラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            SetStatus($"エラー: {message}");
        }

        private void UpdatePlaybackButtons()
        {
            BtnPlay.IsEnabled   = !_isSpeaking;
            BtnPause.IsEnabled  =  _isSpeaking && !_isPaused;
            BtnResume.IsEnabled =  _isSpeaking &&  _isPaused;
            BtnStop.IsEnabled   =  _isSpeaking;
            BtnSaveWav.IsEnabled = !_isSpeaking;
        }

        // ----------------------------------------------------------------
        // 音声パラメータ変更
        // ----------------------------------------------------------------

        private void CmbVoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbVoice.SelectedItem is string voiceName)
                _speechService.SetVoice(voiceName);
        }

        private void SldRate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int rate = (int)SldRate.Value;
            TxtRateVal.Text = rate.ToString("+0;-0;0");
            _speechService.SetRate(rate);
        }

        private void SldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int vol = (int)SldVolume.Value;
            TxtVolumeVal.Text = vol.ToString();
            _speechService.SetVolume(vol);
        }

        // ----------------------------------------------------------------
        // WAV 保存
        // ----------------------------------------------------------------

        private void BtnSaveWav_Click(object sender, RoutedEventArgs e) => SaveWav();

        private void SaveWav()
        {
            string rawText = TxtInput.Text;
            if (string.IsNullOrWhiteSpace(rawText))
            {
                MessageBox.Show("保存するテキストがありません。",
                    "WAV 保存", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string speechText = _dictService.ApplyDictionary(rawText);

            var dlg = new SaveFileDialog
            {
                Title      = "WAV ファイルとして保存",
                Filter     = "WAV ファイル (*.wav)|*.wav",
                DefaultExt = "wav",
                FileName   = $"kouhou_{DateTime.Now:yyyyMMdd_HHmmss}.wav"
            };
            if (dlg.ShowDialog() != true) return;

            SetStatus("WAV 保存中...");
            BtnSaveWav.IsEnabled = false;

            try
            {
                _speechService.SaveToWav(speechText, dlg.FileName);
                SetStatus($"WAV 保存完了: {Path.GetFileName(dlg.FileName)}");
                MessageBox.Show(
                    $"WAV ファイルを保存しました。\n\n{dlg.FileName}",
                    "保存完了",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error($"WAV 保存エラー: {ex.Message}");
                MessageBox.Show(
                    $"WAV ファイルの保存に失敗しました。\n\n{ex.Message}",
                    "保存エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                SetStatus("WAV 保存に失敗しました。");
            }
            finally
            {
                BtnSaveWav.IsEnabled = true;
            }
        }

        // ----------------------------------------------------------------
        // 辞書操作
        // ----------------------------------------------------------------

        private void BtnAddEntry_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new DictionaryEntryDialog { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                _dictService.AddEntry(dlg.Result);
                SaveDictionaryAndRefresh();
                SetStatus($"辞書に追加しました: 「{dlg.Result.Display}」→「{dlg.Result.Reading}」");
            }
        }

        private void BtnEditEntry_Click(object sender, RoutedEventArgs e) => EditSelectedEntry();
        private void DgDictionary_MouseDoubleClick(object sender, MouseButtonEventArgs e) => EditSelectedEntry();

        private void EditSelectedEntry()
        {
            int idx = DgDictionary.SelectedIndex;
            if (idx < 0 || idx >= _dictService.Entries.Count) return;

            var entry = _dictService.Entries[idx];
            var dlg   = new DictionaryEntryDialog(entry.Clone()) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                _dictService.UpdateEntry(idx, dlg.Result);
                SaveDictionaryAndRefresh();
                SetStatus($"辞書を更新しました: 「{dlg.Result.Display}」→「{dlg.Result.Reading}」");
            }
        }

        private void BtnDeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            int idx = DgDictionary.SelectedIndex;
            if (idx < 0) return;

            var entry = _dictService.Entries[idx];
            var result = MessageBox.Show(
                $"「{entry.Display}」→「{entry.Reading}」を辞書から削除しますか？",
                "削除確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _dictService.RemoveEntry(idx);
                SaveDictionaryAndRefresh();
                SetStatus($"辞書から削除しました: 「{entry.Display}」");
            }
        }

        private void DgDictionary_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Insert: BtnAddEntry_Click(sender, e);    e.Handled = true; break;
                case Key.F2:     EditSelectedEntry();              e.Handled = true; break;
                case Key.Delete: BtnDeleteEntry_Click(sender, e); e.Handled = true; break;
            }
        }

        // ----------------------------------------------------------------
        // CSV インポート / エクスポート
        // ----------------------------------------------------------------

        private void MenuImportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "CSV インポート",
                Filter = "CSV ファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*",
                DefaultExt = "csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var imported = CsvService.Import(dlg.FileName);
                if (imported.Count == 0)
                {
                    MessageBox.Show("インポートできるエントリがありませんでした。\nCSVの形式（表記,読み,備考,優先順位）を確認してください。",
                        "インポート", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var answer = MessageBox.Show(
                    $"{imported.Count} 件のエントリをインポートします。\n\n" +
                    $"「はい」: 現在の辞書に追加します\n" +
                    $"「いいえ」: 現在の辞書を置き換えます",
                    "インポート確認",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (answer == MessageBoxResult.Cancel) return;

                if (answer == MessageBoxResult.No)
                    _dictService.ReplaceAll(imported);
                else
                    foreach (var e2 in imported) _dictService.AddEntry(e2);

                SaveDictionaryAndRefresh();
                SetStatus($"CSV インポート完了: {imported.Count} 件");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"CSV インポートに失敗しました。\n\n{ex.Message}",
                    "インポートエラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Logger.Error($"CSV インポートエラー: {ex.Message}");
            }
        }

        private void MenuExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title      = "CSV エクスポート",
                Filter     = "CSV ファイル (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName   = $"dictionary_{DateTime.Now:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                CsvService.Export(dlg.FileName, _dictService.Entries);
                SetStatus($"CSV エクスポート完了: {dlg.FileName}");
                MessageBox.Show($"CSV を保存しました。\n\n{dlg.FileName}",
                    "エクスポート完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"CSV エクスポートに失敗しました。\n\n{ex.Message}",
                    "エクスポートエラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Logger.Error($"CSV エクスポートエラー: {ex.Message}");
            }
        }

        private void MenuLoadSample_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(SampleDictionaryPath))
            {
                MessageBox.Show(
                    $"サンプル辞書ファイルが見つかりません。\n{SampleDictionaryPath}",
                    "サンプル辞書", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int added = _dictService.LoadSampleDictionary(SampleDictionaryPath);
                SaveDictionaryAndRefresh();
                SetStatus($"サンプル辞書を読み込みました: {added} 件追加");
                MessageBox.Show(
                    $"サンプル辞書から {added} 件を追加しました。\n（既存エントリと重複するものはスキップしました）",
                    "サンプル辞書読み込み",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"サンプル辞書の読み込みに失敗しました。\n{ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ----------------------------------------------------------------
        // ヘルプ
        // ----------------------------------------------------------------

        private void MenuShortcuts_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "【ショートカットキー一覧】\n\n" +
                "Ctrl+O  : テキストファイルを開く\n" +
                "Ctrl+P  : 辞書を適用してプレビュー更新\n" +
                "Ctrl+S  : WAV ファイルとして保存\n\n" +
                "F5      : 読み上げ開始（選択中は選択範囲のみ）\n" +
                "F6      : 一時停止\n" +
                "F7      : 再開\n" +
                "F8      : 停止\n\n" +
                "辞書一覧にフォーカスがある場合：\n" +
                "Ins     : エントリ追加\n" +
                "F2      : 選択エントリ編集\n" +
                "Del     : 選択エントリ削除\n" +
                "ダブルクリック : 選択エントリ編集",
                "ショートカットキー一覧",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "声の広報 テキスト読み上げツール v1.0\n\n" +
                "自治体職員向けの読み上げ補助ツールです。\n" +
                "Windows の音声合成エンジンを使用します。\n\n" +
                "辞書ファイル: " + DictionaryPath + "\n" +
                "ログファイル: %LOCALAPPDATA%\\TxtToVoice\\logs\\",
                "バージョン情報",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ----------------------------------------------------------------
        // ユーティリティ
        // ----------------------------------------------------------------

        private void SaveDictionaryAndRefresh()
        {
            _dictService.Save();
            RefreshDictionaryList();
            // プレビューが表示されていれば自動更新
            if (!string.IsNullOrEmpty(TxtInput.Text))
                ApplyAndPreview();
        }

        private void SetStatus(string message)
        {
            TxtStatus.Text = message;
            Logger.Info($"[ステータス] {message}");
        }

        // ----------------------------------------------------------------
        // ウィンドウクローズ
        // ----------------------------------------------------------------

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _speechService.Stop();
            _speechService.Dispose();
            Logger.Info("アプリケーション終了");
        }
    }
}
