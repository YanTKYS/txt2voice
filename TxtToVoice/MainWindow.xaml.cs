using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using TxtToVoice.Models;
using TxtToVoice.Services;

namespace TxtToVoice
{
    /// <summary>
    /// 声の広報 テキスト読み上げツール — メインウィンドウ（コア）。
    ///
    /// ファイル構成（partial class）:
    ///   MainWindow.xaml.cs            — フィールド・コンストラクタ・初期化・共通ユーティリティ
    ///   MainWindow.FileOperations.cs  — ファイル開く・クリア・テキスト入力
    ///   MainWindow.PlaybackOperations.cs — 読み上げ・音声保存・パラメータ操作
    ///   MainWindow.DictionaryOperations.cs — 辞書CRUD・プレビュー・CSV入出力
    /// </summary>
    public partial class MainWindow : Window
    {
        // ----------------------------------------------------------------
        // フィールド（全 partial ファイルから参照可能）
        // ----------------------------------------------------------------

        private readonly SpeechService     _speechService;
        private readonly DictionaryService _dictService;
        private readonly ObservableCollection<DictionaryEntry> _entries = new();

        private static readonly string DictionaryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TxtToVoice", "dictionary.json");

        private static readonly string SampleDictionaryPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "sample_dictionary.json");

        private bool _isSpeaking      = false;
        private bool _isPaused        = false;
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

            DgDictionary.ItemsSource = _entries;

            _speechService.SpeakStarted   += OnSpeakStarted;
            _speechService.SpeakCompleted += OnSpeakCompleted;
            _speechService.SpeakError     += OnSpeakError;

            InitializeVoiceCombo();
            LoadDictionary();

            if (_speechService.IsAvailable)
            {
                SetStatus("準備完了。原稿を入力して「辞書を適用してプレビュー更新」を押してください。");
            }
            else
            {
                SetStatus("警告: 音声エンジンを初期化できませんでした。テキスト編集・辞書管理は利用できます。");
                DisableSpeechControls();
                MessageBox.Show(
                    "音声エンジン（Windows SAPI）を初期化できませんでした。\n\n" +
                    $"詳細: {_speechService.InitializationError}\n\n" +
                    "テキスト編集・辞書管理は引き続き利用できます。\n" +
                    "音声機能を使うには、Windowsの「設定 → 時刻と言語 → 音声認識」から\n" +
                    "日本語音声パッケージを追加してください。",
                    "音声エンジン初期化エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
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

        // ----------------------------------------------------------------
        // キーボードショートカット
        // ----------------------------------------------------------------

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.O: OpenFile();        e.Handled = true; break;
                    case Key.P: ApplyAndPreview(); e.Handled = true; break;
                    case Key.S: SaveAudio();       e.Handled = true; break;
                }
                return;
            }

            switch (e.Key)
            {
                case Key.F5: StartSpeech();  e.Handled = true; break;
                case Key.F6: PauseSpeech();  e.Handled = true; break;
                case Key.F7: ResumeSpeech(); e.Handled = true; break;
                case Key.F8: StopSpeech();   e.Handled = true; break;
            }
        }

        // ----------------------------------------------------------------
        // ヘルプメニュー
        // ----------------------------------------------------------------

        private void MenuShortcuts_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "【ショートカットキー一覧】\n\n" +
                "Ctrl+O  : テキストファイルを開く\n" +
                "Ctrl+P  : 辞書を適用してプレビュー更新\n" +
                "Ctrl+S  : 音声ファイルとして保存（WAV/MP3/MP4）\n\n" +
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
                "声の広報 テキスト読み上げツール\n\n" +
                "自治体職員向けの読み上げ補助ツールです。\n" +
                "Windows の音声合成エンジン（SAPI）を使用します。\n\n" +
                "辞書ファイル: " + DictionaryPath + "\n" +
                "ログファイル: %LOCALAPPDATA%\\TxtToVoice\\logs\\",
                "バージョン情報",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ----------------------------------------------------------------
        // 共通ユーティリティ
        // ----------------------------------------------------------------

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
