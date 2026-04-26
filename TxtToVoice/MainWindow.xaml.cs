using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TxtToVoice.Models;
using TxtToVoice.Services;

namespace TxtToVoice
{
    /// <summary>
    /// 声の広報 テキスト読み上げツール — メインウィンドウ（コア）。
    ///
    /// ファイル構成（partial class）:
    ///   MainWindow.xaml.cs               — フィールド・コンストラクタ・初期化・共通ユーティリティ
    ///   MainWindow.FileOperations.cs     — ファイル開く・最近使ったファイル・クリア・テキスト入力
    ///   MainWindow.SettingsOperations.cs — 設定読み書き・SettingsDialog 呼び出し
    ///   MainWindow.PlaybackOperations.cs — 読み上げ・音声保存・パラメータ操作
    ///   MainWindow.DictionaryOperations.cs — 辞書CRUD・プレビュー・CSV入出力
    /// </summary>
    public partial class MainWindow : Window
    {
        // ----------------------------------------------------------------
        // フィールド（全 partial ファイルから参照可能）
        // ----------------------------------------------------------------

        private readonly SpeechService       _speechService;
        private readonly DictionaryService  _dictService;
        private readonly AppSettingsService _settingsService = new();
        private readonly ObservableCollection<DictionaryEntry> _entries = new();

        // パスは PathConfig で一元管理（ポータブルモード対応）
        private static readonly string DictionaryPath   = PathConfig.DictionaryPath;
        private static readonly string SampleDictionaryPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "sample_dictionary.json");

        private PlaybackState _playback = PlaybackState.Idle;
        private bool _annotatedPreview = true;
        private readonly List<string> _recentFiles = new();

        // 機微データ保存ポリシー（LoadSettings() で復元、SettingsDialog で変更可能）
        private bool _saveLastInputText       = true;
        private bool _saveRecentFiles          = true;
        private bool _clearSensitiveDataOnExit = false;
        private bool _deleteLogOnExit          = false;

        // 監査ログ保持期間（0 = 無制限）
        private int _auditRetentionMonths = 13;

        // 音声保存ファイル名プレフィックス
        private string _saveFilePrefix = "kouhou";

        // 自動プレビュー用デバウンスキャンセルトークン
        private CancellationTokenSource? _autoPreviewCts;

        // 音声エンジン種別（変更は次回起動時に適用）
        private string _speechEngineType = SpeechEngineFactory.Default;

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        public MainWindow()
        {
            InitializeComponent();

            string portableNote = PathConfig.IsPortable ? "（ポータブルモード）"
                                : PathConfig.PortableFallbackApplied ? "（ポータブル要求→書込不可→通常モードへ自動切替）"
                                : string.Empty;
            Logger.Info($"アプリケーション起動{portableNote}");

            // 設定から音声エンジン種別を先読みして適切なエンジンを生成する
            _speechEngineType = AppSettingsService.ReadEngineType();
            _speechService   = new SpeechService(SpeechEngineFactory.Create(_speechEngineType));
            _dictService   = new DictionaryService(DictionaryPath);

            DgDictionary.ItemsSource = _entries;

            _speechService.SpeakStarted   += OnSpeakStarted;
            _speechService.SpeakCompleted += OnSpeakCompleted;
            _speechService.SpeakProgress  += OnSpeakProgress;
            _speechService.SpeakError     += OnSpeakError;

            InitializeVoiceCombo();
            LoadSettings();                               // スライダー値・音声を復元（InitializeVoiceCombo の後）
            AuditLogger.PurgeOldLogs(_auditRetentionMonths); // 起動時に保持期間超過ファイルを削除
            LoadDictionary();

            // ポータブルモード書込不可フォールバックの通知
            if (PathConfig.PortableFallbackApplied)
            {
                string fallbackData = PathConfig.DataDirectory;
                MessageBox.Show(
                    "portable.flag が見つかりましたが、EXE フォルダへの書き込みができません。\n\n" +
                    "辞書・設定・ログは通常の保存先（%LOCALAPPDATA%\\TxtToVoice）に自動切替されました。\n\n" +
                    $"保存先: {fallbackData}\n\n" +
                    "ポータブルモードを使う場合はフォルダのアクセス権限を確認してください。",
                    "ポータブルモード — 通常モードへ自動切替",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Logger.Warn($"ポータブルモード書込不可: 通常モードへフォールバック。保存先={fallbackData}");
            }

            if (_speechService.IsAvailable)
            {
                SetStatus("準備完了。原稿を入力して「辞書を適用してプレビュー更新」を押してください。");
            }
            else
            {
                string engineLabel = SpeechEngineFactory.GetLabel(_speechEngineType);
                SetStatus($"警告: 音声エンジン（{engineLabel}）を初期化できませんでした。テキスト編集・辞書管理は利用できます。");
                DisableSpeechControls();

                string detail = BuildEngineErrorDetail();
                MessageBox.Show(
                    $"音声エンジン（{engineLabel}）を初期化できませんでした。\n\n" +
                    $"{detail}\n\n" +
                    "テキスト編集・辞書管理は引き続き利用できます。",
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
            string version = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "不明";
            string portableNote = PathConfig.IsPortable             ? "\n動作モード: ポータブルモード（EXEフォルダ内にデータ保存）"
                                : PathConfig.PortableFallbackApplied ? "\n動作モード: 通常モード（ポータブル要求→書込不可→自動切替）"
                                : string.Empty;
            string engineLabel = SpeechEngineFactory.GetLabel(_speechEngineType, prefixWindows: true);
            string creditNote  = _speechEngineType == SpeechEngineFactory.OpenJTalk
                ? "\n\n[オープンソース使用許諾]\n" +
                  "OpenJTalk エンジンには以下のコンポーネントを使用しています:\n" +
                  "  jtalkdll (MIT License)\n" +
                  "  Open JTalk / MeCab (Modified BSD License)\n" +
                  "  HTS Voice \"Mei\" © 2009-2015 名古屋工業大学 (CC BY 3.0)\n" +
                  "詳細: THIRD_PARTY_LICENSES.txt"
                : string.Empty;
            MessageBox.Show(
                $"声の広報 テキスト読み上げツール  v{version}\n\n" +
                "自治体職員向けの読み上げ補助ツールです。\n" +
                $"音声エンジン: {engineLabel}\n" +
                portableNote + "\n\n" +
                "辞書ファイル: " + PathConfig.DictionaryPath + "\n" +
                "ログファイル: " + PathConfig.LogDirectory +
                creditNote,
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

        private string BuildEngineErrorDetail()
        {
            if (_speechEngineType == SpeechEngineFactory.OpenJTalk)
            {
                var diag = _speechService.GetOpenJTalkDiagnostics();
                if (diag != null)
                    return "OpenJTalk セットアップ状態:\n\n" +
                           diag.FormatChecklist() + "\n\n" +
                           "不足コンポーネントがあります。\n" +
                           "setup_openjtalk.ps1 を TxtToVoice.exe と同じフォルダで実行してください。";
            }
            return $"詳細: {_speechService.InitializationError}\n\n" +
                   "音声機能を使うには、Windows の「設定 → 時刻と言語 → 音声認識」から\n" +
                   "日本語音声パッケージを追加してください。";
        }

        // ----------------------------------------------------------------
        // ウィンドウクローズ
        // ----------------------------------------------------------------

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 設定は Dispose 前に組み立てる（CurrentVoiceName は破棄後に取得できないため）
            var settings = BuildAppSettings(isExit: true);

            _autoPreviewCts?.Cancel();
            _autoPreviewCts?.Dispose();
            _speechService.Stop();
            _speechService.Dispose();

            _settingsService.Save(settings);

            Logger.Info("アプリケーション終了");

            // 監査モード + ログ削除オプションが有効なときは終了時にログファイルを削除する
            if (_clearSensitiveDataOnExit && _deleteLogOnExit)
                Logger.DeleteTodayLog();
        }
    }
}
