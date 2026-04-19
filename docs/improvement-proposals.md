# 改善提案一覧

全提案の詳細・経緯を管理するドキュメント。
**未着手の項目のみを素早く確認したい場合は [docs/backlog.md](./backlog.md) を参照すること。**

---

## 優先度：高

### 42. WinRT 一時ファイル（.tmp）残存バグ修正 ✅

**課題**  
v0.3.6 の `WinRtSpeechEngine.SaveToFile()` で使用している

```csharp
string tempWavPath = Path.ChangeExtension(Path.GetTempFileName(), ".wav");
```

は、`Path.GetTempFileName()` が内部で空の `.tmp` ファイルを OS 上に実際に作成したのち、`ChangeExtension` で返す文字列を `.wav` に変えるだけ。元の `.tmp` ファイルは削除されないため、音声保存のたびに `%TEMP%` に空の `.tmp` ファイルが蓄積する。

**実装方針**

`Path.GetRandomFileName()` はファイルを生成しないため、こちらを使う。

```csharp
// 修正後: ファイルを生成しない GetRandomFileName を使用
string tempWavPath = Path.Combine(Path.GetTempPath(),
    Path.ChangeExtension(Path.GetRandomFileName(), ".wav"));
```

**関連ファイル**

- `TxtToVoice/Services/WinRtSpeechEngine.cs` — `SaveToFile()` のテンポラリパス生成部分を修正

---

### 43. WinRT WAV 保存の File.Move 異ドライブ失敗耐性を上げる ✅

**課題**  
v0.3.6 の `WinRtSpeechEngine.SaveToFile()` WAV パスは `File.Move` で一時ファイルを最終パスへ移動する。  
.NET の `File.Move` は **同一ボリューム間はアトミック移動、異なるボリューム間は `IOException`** を投げる。  
ユーザーがネットワーク共有フォルダや D ドライブへ保存するケースで保存が失敗し、一時ファイルが `%TEMP%` に残る。

**実装方針**

`File.Copy` + `File.Delete` に変更する（コスト: 追加のディスク読み書きが発生するが、短い WAV ファイルでは許容範囲）。

```csharp
// 修正後: 異なるボリュームでも確実に動作
File.Copy(tempWavPath, outputPath, overwrite: true);
// finally ブロックで tempWavPath を削除（既存の処理で対応済み）
tempWavPath = string.Empty; // 明示移動完了フラグ
```

ただし `tempWavPath = string.Empty` を設定する位置に注意（finally での削除ロジックと整合させる）。

**関連ファイル**

- `TxtToVoice/Services/WinRtSpeechEngine.cs` — WAV 分岐の `File.Move` → `File.Copy` + `File.Delete`

---

### 17. Shift_JIS コードページ登録の一本化 ✅

**課題**  
`CsvService` の静的コンストラクタで `Encoding.RegisterProvider` を呼んでいるが、
テキストファイル読み込み側（`MainWindow.FileOperations.ReadTextFileWithFallback`）は
`Encoding.GetEncoding("shift_jis")` を直接呼んでいる。

CSV 機能を使う前に Shift_JIS .txt ファイルをドロップ・開く操作をした場合、
プロバイダーが未登録のまま `GetEncoding` が実行され `ArgumentException` になる可能性がある。

**再現手順**（環境依存）

1. アプリ起動
2. CSV インポートを使わず Shift_JIS テキストをドラッグ&ドロップ
3. `ReadTextFileWithFallback` の Shift_JIS フォールバック処理で例外

**実装方針**

`App.OnStartup` で `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` を
一度だけ呼ぶ（全体の共通化）。`CsvService` の静的コンストラクタからは削除して重複を排除。

```csharp
// App.xaml.cs OnStartup の先頭に追加
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
```

**関連ファイル**

- `App.xaml.cs` — `OnStartup` に登録処理を追加
- `Services/CsvService.cs` — 静的コンストラクタの登録処理を削除（App 側に委譲）
- `TxtToVoice.Tests/Services/CsvServiceTests.cs` — コンストラクタの登録処理を削除（不要になる）

---

### 18. ポータブルモード時の未処理例外ダイアログのログパス誤表示 ✅

**課題**  
`App.xaml.cs` の `DispatcherUnhandledException` ハンドラ内でログパスをハードコードしている。
ポータブルモードでは `PathConfig.LogDirectory` が EXE フォルダ配下になるにもかかわらず、
エラーダイアログに `%LOCALAPPDATA%\TxtToVoice\logs` が表示され、案内が誤る。

```csharp
// 現状（App.xaml.cs 34-36行）: ハードコード
string logPath = System.IO.Path.Combine(
    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
    "TxtToVoice", "logs");
```

**実装方針**

```csharp
// 修正後: PathConfig を使用
string logPath = PathConfig.LogDirectory;
```

**関連ファイル**

- `App.xaml.cs` — `DispatcherUnhandledException` ハンドラのログパスを `PathConfig.LogDirectory` に変更

---

### 2. ドラッグ&ドロップでファイルを開く ✅

**課題**  
テキストファイルをウィンドウにドロップして開けない。現場でのファイル操作が多い職員には不便。

**実装方針**

XAML の `Window` 要素に `AllowDrop="True"` を追加し、`Drop` イベントを処理する。

---

### 3. 音声保存の非同期化 ✅（キャンセル UI も実装済み）

**課題**  
`SpeechService.SaveToFile()` は同期処理のため、長い原稿（数千文字）を MP3 保存すると
UI スレッドがブロックされ、ウィンドウが「応答なし」状態になる。

**実装方針（済）**

- `SaveToFile()` に `CancellationToken` を受け取る非同期版 `SaveToFileAsync()` を追加
- `MainWindow.PlaybackOperations.cs` の `SaveAudio()` を `async void SaveAudio()` に変更

**v0.2.0 で実装済み: 保存進捗ダイアログ＋キャンセル**

- `Dialogs/SaveProgressDialog.xaml` / `.xaml.cs` を追加（非モーダル、インジケーター＋キャンセルボタン）
- `SaveAudio()` 内で `CancellationTokenSource` を生成してダイアログに渡す
- キャンセル時は書きかけファイルを自動削除
- `SpeechService` 側で `SpeakAsync` + `ManualResetEventSlim` + `ct.Register(() => SpeakAsyncCancelAll())` でベストエフォート中断

---

### 10. CSV インポートの複数行セル対応 ✅

**課題**  
`CsvService.Import()` が `File.ReadAllLines()` + 単行パーサの構成のため、
RFC 4180 §2.6 の「引用符内に改行を含むフィールド」に非対応。
実装コメントに「RFC 4180 に準じたクォート処理をサポートする」と記載されており、
仕様と実装にギャップがある。

**再現例**

```csv
表記,読み,備考
"市の花
バラ",しのはなばら,複数行備考
```

→ `ReadAllLines()` で分割されるため 2 行目が不正データとして破棄される。

**実装方針**

`StreamReader` でレコード単位のパーサに変更する。
閉域配布方針により外部ライブラリ（CsvHelper 等）は不採用。

```csharp
// 方針: File.ReadAllLines → StreamReader で逐次読み込み
// ParseCsvLine を IEnumerable<List<string>> ParseCsvRecords(StreamReader) に昇格
```

---

### 11. バージョン情報の一元化 ✅

**課題**  
- リリースノートは `v0.1.x` だが `TxtToVoice.csproj` は `1.0.0` 固定
- 「バージョン情報」ダイアログに実バージョンが表示されない
- サポート問い合わせ時に利用者が使用バージョンを特定しにくい

**実装方針**

1. `TxtToVoice.csproj` の `<Version>` をリリース時に更新
2. About ダイアログで `AssemblyInformationalVersionAttribute` からバージョンを取得して表示
3. CI の release ワークフローで `csproj` バージョンを自動書き換える（将来対応）

---

### 36. BuildAppSettings テストの追加 ✅

**課題**  
`BuildAppSettings(isExit)` は `TxtInput`, `SldRate`, `ChkHighlight`, `ChkSsml` 等の UI コントロールを直接参照し、保存ポリシー分岐（監査モード・SSML・ハイライト等）が多い。将来の設定項目追加で回帰しやすく、現状テストがない。

`SpeechEngineFactory` の単体テスト（`SpeechEngineFactoryTests`）は v0.3.3 で追加済みだが、`BuildAppSettings` 本体のテストは UI 依存の問題で未着手。

**実装方針**

**(A) ViewModel 層への分離**  
設定値を `AppSettingsViewModel`（または設定構造体）に集約し、UI コントロールへの直接依存を排除したうえで単体テストを追加する。

**(B) WPF テストフレームワーク経由**  
`Microsoft.Extensions.Testing.Abstractions` 等の WPF テストフレームワークを使い、UI スレッドでのテストを実行する。

**関連ファイル**

- `TxtToVoice/MainWindow.PlaybackOperations.cs` — `BuildAppSettings(isExit)` のロジック分離（A の場合）
- `TxtToVoice.Tests/` — テストクラスの追加

---

### 38. ログ匿名化の強化（空白パス・UNCパス対応） ✅

**課題**  
`Logger.AnonymizePaths()` の正規表現 `[A-Za-z]:\\[^\s,"']+` は以下のケースを取りこぼす。

- **空白を含むパス**（例: `C:\Users\A\My Documents\原稿.txt`）— `[^\s]` で空白前に切れる
- **UNC パス**（例: `\\server\share\原稿.txt`）— ドライブレター先頭パターンに非適合
- **引用符で囲まれたパス**（例: `"C:\path\file.txt"`）— `[,"']` で末尾切れが誤る場合あり

**実装方針**

以下の 3 パターンをカバーするように正規表現を複数化する。

1. **引用符付きパス（Windows & UNC）**: `"(?:[A-Za-z]:\\|\\\\)[^"]+"`
2. **ドライブレターパス（スペース含む）**: `[A-Za-z]:\\(?:\S+\\)*\S+`
3. **UNC パス**: `\\\\[A-Za-z0-9._-]+\\[A-Za-z0-9._$-]+(?:\\\S+)*`

```csharp
private static string AnonymizePaths(string message)
{
    // 引用符付きパス (Windows ドライブ & UNC)
    message = Regex.Replace(message, @"""(?:[A-Za-z]:\\|\\\\)[^""]+""",
        m => $"\"{Path.GetFileName(m.Value.Trim('"'))}\"");
    // ドライブレターパス（空白含む）
    message = Regex.Replace(message, @"[A-Za-z]:\\(?:\S+\\)*\S+",
        m => { try { return Path.GetFileName(m.Value); } catch { return m.Value; } });
    // UNC パス
    message = Regex.Replace(message, @"\\\\[A-Za-z0-9._-]+\\[A-Za-z0-9._$-]+(?:\\\S+)*",
        m => { try { return Path.GetFileName(m.Value); } catch { return m.Value; } });
    return message;
}
```

過度な置換によるデバッグ性低下を避けるため、ファイル名部分（`Path.GetFileName`）は維持する。

**関連ファイル**

- `TxtToVoice/Services/Logger.cs` — `AnonymizePaths()` の正規表現を強化
- `TxtToVoice.Tests/Services/LoggerAnonymizeTests.cs` — 空白パス・UNC・引用符のテストケースを追加（新規）

---

### 39. v0.3.4 追加機能へのテスト追加（回帰防止） ✅

**課題**  
v0.3.4 で辞書キャッシュ化・CSV 重複マージ・保存進捗フェーズ・ログ匿名化を一括実装したが、対応するテストがない。将来の変更による回帰を早期検知するためにテストを整備する。

| 機能 | 対象クラス |
|---|---|
| 辞書ソートキャッシュ無効化 | `DictionaryService._sortedCache` |
| CSV 重複マージ（上書き/スキップ） | `DictionaryService.HasDisplay()` / `UpdateByDisplay()` |
| Logger パス匿名化 | `Logger.AnonymizePaths()`（#38 と連動） |
| 保存進捗フェーズメッセージ | `SystemSpeechEngine` / `WinRtSpeechEngine` |

**実装方針**

```
TxtToVoice.Tests/Services/
├── DictionaryServiceCacheTests.cs   # キャッシュ無効化（Add/Update/Remove/Load/ReplaceAll）
├── DictionaryServiceMergeTests.cs   # HasDisplay・UpdateByDisplay の境界値テスト
├── LoggerAnonymizeTests.cs          # AnonymizePaths（通常パス・空白含む・UNC・引用符）
└── SpeechProgressTests.cs           # WAV / MP3 / MP4 保存時のフェーズメッセージ
```

`DictionaryService` および `Logger` のテストは WPF 依存なしで追加可能。  
`SpeechProgressTests` はエンジン依存のため `[Trait("Category", "RequiresEngine")]` での CI 除外を検討。

**関連ファイル**

- `TxtToVoice.Tests/Services/DictionaryServiceCacheTests.cs` — 新規追加
- `TxtToVoice.Tests/Services/DictionaryServiceMergeTests.cs` — 新規追加
- `TxtToVoice.Tests/Services/LoggerAnonymizeTests.cs` — 新規追加
- `TxtToVoice.Tests/Services/SpeechProgressTests.cs` — 新規追加（CI 除外条件付き）

---

## 優先度：中

### 44. README のテスト手順・ソース構成図を v0.3.6 対応に更新

**課題**  
v0.3.6 で `TxtToVoice.Core` / `TxtToVoice.Core.Tests` を新設したが、README のテスト手順とソース構成図は v0.3.5 以前のまま（`TxtToVoice.Tests` 単体）。  
新規参加者がリポジトリを見たときに構成を誤認する。

**実装方針**

1. **テスト節の更新**: 「Core.Tests（常時・全 PR）」と「TxtToVoice.Tests（Windows 実機 / エンジン依存）」に分離して記述。
   - Core.Tests 実行コマンド例: `dotnet test TxtToVoice.Core.Tests`
   - TxtToVoice.Tests エンジン除外例: `dotnet test TxtToVoice.Tests --filter "Category!=RequiresEngine"`
2. **ソース構成図の更新**: `TxtToVoice.Core`（net8.0）と `TxtToVoice.Core.Tests`（net8.0）を追記し、依存関係の矢印を示す。

**関連ファイル**

- `README.md` — テスト節・ソース構成図を更新

---

### 45. CI 2 レーン化（Core.Tests 必須 / Windows 依存テスト任意）

**課題**  
v0.3.6 でプロジェクト分離が完了しているが、CI ワークフローは変更しておらず、`TxtToVoice.Tests` を単体で実行している（あるいは CI 自体が未整備）。  
`TxtToVoice.Core.Tests` が net8.0 で OS 非依存のため、Linux runner でも実行できるにもかかわらず活用されていない。

**実装方針**

```
レーンA（必須 / 全 PR / Linux or Windows）:
  dotnet test TxtToVoice.Core.Tests

レーンB（任意 / Windows runner / PR または定期実行）:
  dotnet test TxtToVoice.Tests --filter "Category!=RequiresEngine"
```

- レーンA: 純ロジックテストを全 PR で必須実行し、回帰を早期検知する。
- レーンB: エンジン非依存部分を Windows runner で確認。RequiresEngine は週次等の定期実行に分離。

**関連ファイル**

- `.github/workflows/ci.yml`（またはそれに相当する CI 設定ファイル）— ジョブを 2 レーンに分割

---

### 40. CSV 重複判定の計算量最適化 ✅

**課題**  
`MenuImportCsv_Click` の重複検出処理は `imported` リストを 2 回走査し、各要素ごとに `HasDisplay()`（内部 `Any()` による線形検索）を呼び出す。辞書件数 M・インポート件数 N のとき O(N × M) の計算量となり、大規模辞書での CSV インポート時に体感遅延になりうる。

**実装方針**

1. 既存エントリの Display を `HashSet<string>` に変換（O(M)）
2. `imported` を 1 パスで走査し `newEntries` / `duplicates` に振り分け（O(N)）

```csharp
var existingDisplays = new HashSet<string>(
    _dictService.Entries.Select(e => e.Display), StringComparer.Ordinal);

var newEntries = new List<DictionaryEntry>();
var duplicates = new List<DictionaryEntry>();
foreach (var item in imported)
{
    if (existingDisplays.Contains(item.Display))
        duplicates.Add(item);
    else
        newEntries.Add(item);
}
```

全体計算量: O(M + N)（従来の O(N × M) から改善）。  
`DictionaryService.HasDisplay()` / `UpdateByDisplay()` は他の呼び出し元でも使われるため残存させる。

**関連ファイル**

- `TxtToVoice/MainWindow.DictionaryOperations.cs` — `MenuImportCsv_Click` の重複検出ロジックを HashSet 化

---

### 41. 音声選択の安定化（表示名ではなく ID 保存） ✅

**課題**  
現在の設定モデルは音声を `voiceName`（文字列 1 本）で保管しており、WinRT 側は `DisplayName` を保存キーとして利用している。以下のリスクがある。

- 同名音声が複数ある環境では意図しない音声が選択される可能性がある
- OS アップデート後に `DisplayName` が変化した場合、起動時に音声が見つからずデフォルト音声にフォールバックする
- 将来的な複数エンジン共存時に、`voiceName` 1 本の構造では識別子の形式が統一されず管理が複雑になる

**実装方針**

1. `AppSettings` に `VoiceId`（内部識別子）と `VoiceDisplayName`（表示用）を追加
2. 起動時は `VoiceId` で音声を選択し、不一致時は `VoiceDisplayName` にフォールバック
3. 既存 `voiceName` キーは移行期間中の後方互換キーとして読み込み専用で保持
4. WinRT: `VoiceInformation.Id`、SAPI: `VoiceInfo.Id` を `VoiceId` に使用

**関連ファイル**

- `TxtToVoice/Models/AppSettings.cs` — `VoiceId`・`VoiceDisplayName` フィールドを追加
- `TxtToVoice/Services/AppSettingsService.cs` — 移行ロジック追加（`voiceName` 読み込み → `VoiceId` に変換）
- `TxtToVoice/Services/WinRtSpeechEngine.cs` — `VoiceInformation.Id` ベースの音声選択に変更
- `TxtToVoice/Services/SystemSpeechEngine.cs` — `VoiceInfo.Id` ベースの音声選択に変更
- `TxtToVoice/Dialogs/SettingsDialog.xaml.cs` — 音声選択時に `VoiceId` も保存

---

### 26. 辞書置換エンジンの高速化（都度ソート廃止・Aho-Corasick 移行）

**課題**  
`DictionaryService.ApplyDictionary()` は毎回 `BuildSortedEntries()` で辞書エントリを長さ降順にソートし、
各エントリごとに `IndexOf` を走査するため O(エントリ数 × 本文長) の計算量となる。
辞書が数百件・本文が数万文字になると読み上げ前の置換処理が体感遅延のボトルネックになりうる。

性能テストの閾値も 30〜45 秒と緩めに設定されており、アルゴリズム回帰を早期検知しにくい。

**実装方針（段階的）**

**(A) 都度ソート廃止（最小対応）**  
辞書更新時のみ `_sortedEntries` を再構築し、`ApplyDictionary()` のたびにソートしない。
`AddEntry` / `DeleteEntry` / `Import` 時にキャッシュを更新する。

**(B) Aho-Corasick への移行（高度）**  
1 パスで全エントリの出現位置を O(本文長 + 全エントリ長の合計) で検出する。
辞書更新時のみオートマトンを構築し直す。

**(C) 性能テスト閾値の再定義**  
実運用サイズを基準に閾値を 1〜3 秒台に再定義し、回帰検知感度を高める。

**関連ファイル**

- `TxtToVoice/Services/DictionaryService.cs` — `BuildSortedEntries` をキャッシュ化または Aho-Corasick に置換
- `TxtToVoice.Tests/Services/DictionaryServicePerformanceTests.cs` — 閾値の再定義

---

### 27. CSV インポート時の重複語句マージポリシー明確化

**課題**  
CSV 追加インポート時、エントリは `AddEntry` でそのまま追加されるため、
同一「表記」を持つ重複エントリが辞書に積み上がる。
置換ロジックは長さ・出現順ベースで動作するが、重複時の優先順位はユーザーには不透明で、
辞書メンテ担当者による誤投入リスクがある。

**実装方針**

インポート前に重複を検出し、処理方針をユーザーが選択できるようにする。

| オプション | 動作 |
|---|---|
| 上書き | 既存エントリを新しい読みに置き換える |
| スキップ | 重複エントリは追加しない（既存を維持） |
| 両方保持 | 現状と同じ動作（重複を積む） |

インポート実行前に「追加: N 件 / 重複: N 件 / 更新: N 件」のプレビューを表示することも推奨する。

**関連ファイル**

- `TxtToVoice/MainWindow.DictionaryOperations.cs` — インポート処理にポリシー選択を追加
- `TxtToVoice/Services/DictionaryService.cs` — `ImportEntries(IEnumerable<DictionaryEntry>, MergePolicy)` を追加

---

### 31. ISpeechEngine 抽象化（音声エンジン差し替え可能化） ✅

**課題**  
`SpeechService` が `System.Speech.Synthesis.SpeechSynthesizer` に直接結合しており、
エンジンの差し替え・テスト時のモック注入・将来のマルチエンジン対応がいずれも困難な状態にある。

項目 #32（WinRT 移行）・#33（OSS エンジン同梱）を安全に段階導入するためには、
このインターフェース抽象化が前提ステップとなる。

**実装方針**

`ISpeechEngine` インターフェースを新設し、現行の `SpeechSynthesizer` ラッパーを
`SystemSpeechEngine` として切り出す。`SpeechService` はエンジンを DI（コンストラクタ注入）で受け取る。

```csharp
public interface ISpeechEngine : IDisposable
{
    bool     IsAvailable        { get; }
    string?  InitializationError { get; }
    string?  CurrentVoiceName   { get; }
    IReadOnlyList<string> GetVoices();
    void SetVoice(string voiceName);
    void SetRate(int rate);
    void SetVolume(int volume);
    Task SpeakAsync(string text);
    Task SpeakSsmlAsync(string ssml);
    void Pause();
    void Resume();
    void Stop();
    Task SaveWavAsync(string text, string outputPath, CancellationToken ct);
}
```

設定ダイアログで「音声エンジン種別」（`SystemSpeech` / `WinRT` 等）を選択できるよう `AppSettings` に `SpeechEngineType` フィールドを追加する。
既定値は現行互換（`SystemSpeech`）として段階導入を可能にする。

**関連ファイル**

- `TxtToVoice/Services/ISpeechEngine.cs` — 新規追加（インターフェース定義）
- `TxtToVoice/Services/SystemSpeechEngine.cs` — 新規追加（現行実装のラッパー）
- `TxtToVoice/Services/SpeechService.cs` — エンジン注入対応にリファクタリング
- `TxtToVoice/Models/AppSettings.cs` — `SpeechEngineType` フィールドを追加
- `TxtToVoice/Dialogs/SettingsDialog.xaml` / `.xaml.cs` — エンジン種別選択 UI を追加

---

### 25. 監査モード INFO 抑制の起動直後適用 ✅

**課題**  
`Logger.SuppressInfo` は `LoadSettings()` で設定されるため、`App.OnStartup` および `MainWindow`
初期化中のログ（「アプリケーション起動」「SpeechSynthesizer 初期化成功」等）は
監査モードでも書き込まれてしまう。  
厳密な監査運用では「アプリを一度でも起動した事実」もログに残らないことが要件となりうる。

**実装方針**

`App.OnStartup` の先頭（`Encoding.RegisterProvider` の直後）で
設定 JSON から `clearSensitiveDataOnExit` フィールドのみを先読みし、`Logger.SuppressInfo` に適用する。  
完全な設定読み込みは `MainWindow.LoadSettings()` で従来通り行う。

```csharp
// App.xaml.cs — OnStartup 先頭
var auditFlag = AppSettingsService.ReadAuditFlag();  // 失敗時は false
Logger.SuppressInfo = auditFlag;
```

`AppSettingsService.ReadAuditFlag()` は JSON から `clearSensitiveDataOnExit` のみを
取り出す静的メソッドとして追加する（読み込み失敗・ファイル不在は false として扱う）。

**関連ファイル**

- `App.xaml.cs` — `OnStartup` 先頭に監査フラグ先読みを追加
- `Services/AppSettingsService.cs` — `ReadAuditFlag()` 静的メソッドを追加

---

### 19. 機微データ消去のログ扱いを明文化・UI 文言改善 ✅（A: 文言修正 / B: INFO 抑制 / C: ログ削除オプション 実装済み）

**課題**  
「設定」ダイアログの「終了時にテキスト・ファイル履歴を消去する」は
入力テキストと最近使ったファイル履歴のみが対象であり、ログファイルは消去されない。
ログには読み込んだファイルパスや部分的な入力内容が残りうるため、
「全部消える」と利用者が誤認する可能性がある。

**実装方針（段階的）**

**(A) 最優先: 文言の明確化**  
UI の文言を「テキスト・ファイル履歴のみを消去する（ログは含まない）」に変更する。

**(B) 追加オプション: ログの INFO 抑制**  
監査モード時（`ClearSensitiveDataOnExit = true`）に限り、
`Logger.Info` の出力を抑制して機微情報のログへの書き込みを防ぐ。

**(C) 強化オプション: ログの削除**  
終了時にその日のログファイルを削除するオプションを追加する。
`Logger` にメソッドを追加して `Window_Closing` から呼ぶ。

**関連ファイル**

- `Dialogs/SettingsDialog.xaml` — 文言を修正（A）
- `Services/Logger.cs` — 抑制・削除オプションを追加（B/C）
- `MainWindow.xaml.cs` — `Window_Closing` でオプション分岐（B/C）

---

### 20. ポータブルモード起動時の書込可否チェック ✅（A: 警告表示 / B: 通常モードへ自動フォールバック 実装済み）

**課題**  
EXE フォルダへの書き込みが制限されている環境（配布先 PC の読み取り専用共有フォルダ等）で
ポータブルモードを有効にした場合、設定・辞書・ログの保存がすべてサイレントに失敗する。
エラーメッセージも出ないため、利用者は原因を特定できない。

**実装方針**

起動時（`App.OnStartup` or `MainWindow` コンストラクタ）に
`DataDirectory` / `LogDirectory` への書き込みテストを実行する。

```csharp
private static bool TryWriteAccess(string dir)
{
    try
    {
        Directory.CreateDirectory(dir);
        string probe = Path.Combine(dir, ".write_probe");
        File.WriteAllText(probe, string.Empty);
        File.Delete(probe);
        return true;
    }
    catch { return false; }
}
```

失敗した場合の選択肢:
- **(A) 警告表示のみ**: 「保存先フォルダへの書き込みができません」を MessageBox で通知し続行
- **(B) 通常モードへフォールバック**: ポータブルモードを無効化して `%LOCALAPPDATA%` を使用

**関連ファイル**

- `Services/PathConfig.cs` — 書込テストユーティリティを追加
- `App.xaml.cs` または `MainWindow.xaml.cs` — 起動時チェックを呼び出す

---

### 4. 読み上げ位置のハイライト ✅（蛍光色ハイライト + ON/OFF トグル実装済み）

**課題**  
どこを読んでいるか視覚的にわからない。長い原稿を確認しながら聞く用途で不便。

**実装状況**

- `SpeechService.SpeakProgress` イベントによる進捗通知は実装済み
- v0.1.7 時点では `TxtInput.Select()` による選択ハイライトをステータスバー表示
  （`読み上げ中... (45 / 200 文字)`）に置き換えた
  → **理由**: 複数行にまたがる青い選択表示がユーザーの誤操作と混同されるため

**レビュー指摘（v0.1.7 レビュー）**

選択ハイライトを ON/OFF トグルで切り替えられるようにする案が提起された。

**v0.2.1 で実装済み**

再生操作パネルに「読み上げ中の位置を蛍光色でハイライト表示する」チェックボックスを追加。
ON 時は `SelectionBrush = 蛍光イエロー (#FFEB3B)` + `TxtInput.Select(pos, len) + ScrollToLine()` で表示。
OFF 時はステータスバー表示のみ。SSML モード中はマッピング不可のため常に OFF。

**誤操作との混同を防ぐ対策**: 通常の選択色（システム青）とは異なる蛍光イエローを採用。
読み上げ完了・停止時に SelectionBrush をシステム既定に戻して選択を解除する。

---

### 5. 入力テキストのセッション復元 ✅

**課題**  
アプリ終了前の入力内容が消えるため、再起動のたびに原稿を貼り直す必要がある。

**実装方針**

設定ファイル（提案 1 参照）に `lastInputText` フィールドを追加し、
ウィンドウクローズ時に保存・起動時に復元する。
文字数が多い場合（例: 10,000 字超）は保存しない制限を設けるとよい。

---

### 6. 辞書エントリの試し読みボタン ✅

**課題**  
辞書編集ダイアログで「読み」を登録しても、実際の発音を確認するには
いったん閉じてプレビューを見る必要がある。

**実装方針**

`DictionaryEntryDialog.xaml` に「試し読み」ボタンを追加し、
`SpeechService.SpeakAsync(Reading)` を呼ぶ。

---

### 12. ログ出力量の制御 ✅

**課題**  
`SetStatus()` が `Logger.Info()` を毎回呼び出すため、読み上げ進捗更新時（単語単位）に
大量のログエントリが生成される。長文読み上げで数百行のログが出力されてしまう。

**実装方針**

1. `Logger` にログレベル設定（INFO / WARN / ERROR）を追加し、設定で切り替え可能にする
2. `OnSpeakProgress` 専用の低頻度更新ロジックを追加する
   （例: 前回更新から 1 秒以上経過した場合のみ `Logger.Info` を呼ぶ）

```csharp
// OnSpeakProgress での間引きイメージ
private DateTime _lastProgressLog = DateTime.MinValue;

private void OnSpeakProgress(object? sender, SpeakProgressInfo e)
{
    // ...位置計算...
    TxtStatus.Text = $"読み上げ中... ({absEnd} / {totalChars} 文字)";  // UI は毎回更新
    if ((DateTime.Now - _lastProgressLog).TotalSeconds >= 1.0)
    {
        Logger.Info($"...");
        _lastProgressLog = DateTime.Now;
    }
}
```

---

### 13. 設定保存ポリシー（機微データ対応） ✅

**課題**  
`lastInputText`（前回入力テキスト）と `recentFiles`（最近使ったファイルパス）が
`%LOCALAPPDATA%` に平文で保存される。閉域環境でも内部規程によっては問題になりうる。

**実装方針**

設定 UI に以下のオプションを追加する。

| オプション | 説明 |
|---|---|
| 「前回テキストを保存しない」 | `lastInputText` を常に空で保存 |
| 「最近使ったファイルを保存しない」 | `recentFiles` を常に空で保存、メニューも非表示 |
| 「終了時に機微データを消去する」 | ウィンドウクローズ時に両フィールドを消去（監査向け） |

設定値自体は `AppSettings` に `bool SaveLastInputText`・`bool SaveRecentFiles` として追加。

---

### 14. テスト範囲拡張（回帰防止） ✅（CsvService / AppSettings / Performance）

**課題**  
現状テストは `DictionaryService` / `SsmlBuilder` 中心で、以下の回帰検知が弱い。

- `CsvService`: エンコード自動判別・クォート処理・異常行スキップ
- `AppSettingsService`: 破損 JSON での安全なフォールバック・一時ファイル原子的更新
- UI ロジック: `ISpeechService` 抽象化によるテスタビリティ向上

**実装方針**

```
TxtToVoice.Tests/
├── Services/CsvServiceTests.cs           # 追加
├── Services/AppSettingsServiceTests.cs   # 追加
└── Services/ISpeechService.cs            # SpeechService の抽象化（インターフェース抽出）
```

---

## 優先度：低

### 28. 音声保存進捗の可視化改善（フェーズ表示・キャンセル状態の明確化） ✅

**課題**  
`SaveProgressDialog` のプログレスバーは `IsIndeterminate=true` 固定のため、
長尺保存（数分）でユーザーが完了見込みを把握できない。

MP3/MP4 保存ではエンコード中のキャンセルが「エンコード完了後チェック」に近く、
キャンセルを押してもすぐ停止しない体感になる。

**実装方針**

フェーズラベルの追加（最小対応）:  
- 「音声生成中...」（`SpeakAsync` 段階）  
- 「エンコード中...」（NAudio 変換段階、MP3/MP4 のみ）  
- キャンセルボタン押下後: 「停止処理中...」に変更し、ボタンを無効化

進捗率（%）の表示は NAudio / SAPI が進捗イベントを提供しないため実現困難。

**関連ファイル**

- `TxtToVoice/Dialogs/SaveProgressDialog.xaml` / `.xaml.cs` — フェーズラベル追加・キャンセル後状態の明確化
- `TxtToVoice/Services/SpeechService.cs` — フェーズ通知用コールバックの追加

---

### 37. WinRT 保存処理の長文メモリ効率改善 ✅

**課題**  
`WinRtSpeechEngine.SaveToFile()` は `SpeechSynthesisStream` を `MemoryStream` に全量読み込んでから NAudio でエンコードするため、長文合成では一時的に大量のメモリを消費する。

**実装状況**  
v0.3.3 で `using` による明示解放と `new MemoryStream((int)stream.Size)` による事前確保は対応済み。

**残課題**  
一時 WAV ファイルを経由したストリーミングエンコードへの移行（`MemoryStream` 廃止）を別途検討。ただし `SpeechSynthesisStream` が `IRandomAccessStream` 形式のため NAudio へのブリッジが必要。

**関連ファイル**

- `TxtToVoice/Services/WinRtSpeechEngine.cs` — `SaveToFile()` のストリーミング化

---

### 29. テスト構成の分離（Windows 依存テストと純ロジックテストの分離）✅

**課題**  
テストプロジェクトが `net8.0-windows` + `UseWPF` 前提のため、
純ロジック（`DictionaryService` / `CsvService` 等）の検証でも Windows 実行環境を必要とする。

`SpeechServiceCancelTests` は事前キャンセルの検証が中心で、
保存処理の途中キャンセル・ファイル後始末・例外系の統合テストが不足している。

**実装方針**

**(A) ロジック層の分離（大規模）**  
`TxtToVoice.Core` プロジェクトを切り出し `net8.0`（クロス OS）でビルドできるようにする。
`DictionaryService` / `CsvService` / `SsmlBuilder` / `Logger` / `PathConfig` 等が対象。
UI・WPF 依存は `TxtToVoice` プロジェクトに残す。

**(B) 中間キャンセルテストの追加（単独で可能）**  
`SpeechServiceCancelTests` に `CancellationTokenSource.CancelAfter(ms)` を使った
遅延キャンセルテストを追加する。エンジン不在の CI 環境では `[Trait("Category", "RequiresEngine")]` で除外する。

**関連ファイル**

- `TxtToVoice.Tests/TxtToVoice.Tests.csproj` — `net8.0-windows` を `net8.0` に変更（A の場合）
- `TxtToVoice.Tests/Services/SpeechServiceCancelTests.cs` — 中間キャンセルテストを追加（B）

---

### 30. 監査強化モードのログ匿名化オプション ✅

**課題**  
v0.2.5 で INFO 抑制は改善済みだが、WARN / ERROR は常時記録されるため、
例外発生時にファイルフルパスや入力文字列の断片がログへ残りうる。

**実装方針**

監査強化モード（`ClearSensitiveDataOnExit = true`）時に限り、
ファイルパスをファイル名のみ（ディレクトリ部を `***`）に変換してログへ記録する。

```csharp
// Logger.Write() 内で呼び出す（監査モード時のみ適用）
private static string Sanitize(string message)
{
    if (!SuppressInfo) return message;
    return Regex.Replace(message, @"[A-Za-z]:\\[^\s:""]+",
        m => $@"***\{Path.GetFileName(m.Value)}");
}
```

例外本文の全文マスクは過剰でデバッグ性が落ちるため、パス部分のみを対象とする。

**関連ファイル**

- `TxtToVoice/Services/Logger.cs` — `Sanitize()` ユーティリティを追加し、`Write()` 内で呼び出す

---

### 32. WinRT 音声エンジン実装（Windows.Media.SpeechSynthesis への移行検証） ✅

**前提**: 項目 #31（ISpeechEngine 抽象化）が完了していること。

**概要**  
`System.Speech.Synthesis` の代わりに Windows 10/11 の WinRT 音声合成 API
（`Windows.Media.SpeechSynthesis`）を使う `WinRtSpeechEngine` を実装する。

**メリット**

- OS 標準機能で完結（閉域環境向き）
- 端末に導入済みの OneCore 系音声を利用でき、場合によっては自然さが改善
- ネット接続不要で運用可能
- 将来的な SSML 対応 API の利用も容易

**デメリット・懸念点**

- `Windows.Media.SpeechSynthesis` は UWP/WinRT API のため、WPF（.NET 8）から呼び出すには
  `Microsoft.Windows.SDK.Contracts` または `CsWinRT` が必要（NuGet 追加）
- 音声ファイル出力が `SpeechSynthesisStream` ベースで WAV/MP3 変換フローが差し替わる
- 実際の音声品質は端末に入っている音声に依存する（環境差あり）

**適合度**: 高（現行の閉域 Windows 運用に近い）

**実装方針**

1. `WinRtSpeechEngine : ISpeechEngine` を新規追加
2. `AppSettings.SpeechEngineType = "WinRT"` で切替
3. 品質・互換性を確認後、既定値変更を検討

**関連ファイル**

- `TxtToVoice/Services/WinRtSpeechEngine.cs` — 新規追加
- `TxtToVoice.csproj` — `Microsoft.Windows.SDK.Contracts` 等の NuGet 参照を追加

---

### 33. OSS 日本語 TTS エンジン同梱（OpenJTalk / VOICEVOX 系）【PoC 計画フェーズへ昇格】

**前提**: 項目 #31（ISpeechEngine 抽象化）が完了していること。

**概要**  
OSS の日本語 TTS エンジンをアプリに同梱しローカル実行する。
読みルールや辞書のカスタマイズ自由度が高く、読み上げ品質の大幅改善が期待できる。

**候補エンジン**

| エンジン | 特徴 | ライセンス（参考） |
|---|---|---|
| OpenJTalk + HTS Engine | 軽量・辞書カスタム可・実績あり | MIT 系（音声モデルに要確認） |
| VOICEVOX エンジン（ローカル） | 自然さ高・HTTP/IPC 連携 | LGPL 系（要確認） |

**メリット**

- 完全オフライン・端末依存なし
- 読みルール・辞書カスタマイズの自由度が高い

**デメリット・懸念点**

- 配布物が大きくなる（音声モデル含め数十〜数百 MB）
- ライセンス確認・更新追従・サポートのメンテコスト増
- VOICEVOX 型は別プロセス起動が必要（CPU/メモリ消費・監視運用）
- UI 操作への応答性調整が必要（初期化遅延等）

**適合度**: 中〜高（品質重視かつ端末スペックが十分な場合に有力）

**PoC 計画（v0.3.6 レビューで昇格）**

v0.3.6 でバックログ未着手が #33 のみになったため、調査フェーズから PoC 計画フェーズへ移行する。

1. **OpenJTalk 最小同梱 PoC**: 容量・辞書変換互換性・起動時間を実測
   - バイナリ同梱方法（NuGet / 手動配置）の選定
   - 既存 `DictionaryService` の読みデータとの互換性確認
2. **VOICEVOX 比較評価**: 同条件（品質・容量・ライセンス運用コスト）で比較
   - HTTP/IPC 連携方式の実装コスト
   - 別プロセス起動・監視の安定性評価

**推奨導入順序**  
まず #32（WinRT）評価結果と現場フィードバックを踏まえ、品質改善ニーズが高い場合に本 PoC を実施する。

**関連ファイル**

- `TxtToVoice/Services/OpenJTalkEngine.cs` or `VoicevoxEngine.cs` — 新規追加
- `TxtToVoice.csproj` — エンジンバイナリの同梱設定を追加

---

### 21. テキスト読み込みエンコード判定の README/コード整合 ✅

**課題**  
README は「UTF-8（BOM あり・なし）+ Shift_JIS を自動判別」と記載しているが、
実装（`ReadTextFileWithFallback`）の判定順は BOM → UTF-8 → Shift_JIS のみで、
UTF-16 BOM（LE/BE）は非対応。また実装コメントも簡略的で判定ロジックの説明が薄い。

**実装方針**

**(A) ドキュメント整合（最小対応）**  
README に「UTF-16 は非対応」と明記し、実装コメントに判定順を詳述する。

**(B) UTF-16 BOM 対応（追加実装）**  
`StreamReader` の `detectEncodingFromByteOrderMarks: true` を使い、
UTF-16 LE/BE BOM を自動検出する。

```csharp
// BOM 付き UTF-16 LE/BE を含む自動検出
using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
// → BOM があれば正しいエンコードで読み込まれる
```

**関連ファイル**

- `MainWindow.FileOperations.cs` — `ReadTextFileWithFallback` の実装とコメントを更新
- `README.md` — エンコード対応表を更新

---

### 23. README の v0.2.x 機能説明更新 ✅

**課題**  
v0.2.1 で追加した以下の機能が README に未掲載。  
- 読み上げ位置ハイライト ON/OFF（蛍光イエロー表示）  
- ポータブルモード起動時の書込可否チェック（警告ダイアログ）  
- 機微データ消去の UI 文言改善（「ログは含まない」の明記）

**実装方針**

README の「機能一覧」および「操作手順」セクションを v0.2.1 時点に合わせて更新する。  
スクリーンショットがあれば補足として追記する。

**関連ファイル**

- `README.md` — 各機能の説明を追記

---

### 24. v0.2.x 向けテスト追加 ✅

**課題**  
v0.2.0/v0.2.1 で実装した以下の機能にテストがない。

- `PathConfig.CheckPortableWriteAccess()` — ポータブルモード書込可否チェック
- `SpeechService.SaveWavDirect` / `SaveEncoded` — キャンセル時の `OperationCanceledException` 伝播
- 読み上げ位置ハイライト ON/OFF 切り替え（UI ロジック）

**実装方針**

```
TxtToVoice.Tests/
├── Services/PathConfigTests.cs        # CheckPortableWriteAccess のユニットテスト
└── Services/SpeechServiceSaveTests.cs # キャンセル伝播テスト（mock エンジン環境）
```

**関連ファイル**

- `TxtToVoice.Tests/Services/PathConfigTests.cs` — 新規追加
- `TxtToVoice.Tests/Services/SpeechServiceSaveTests.cs` — 新規追加（環境依存のため [Trait] で CI 除外可）

---

### 22. CI パフォーマンステスト閾値の環境依存対策 ✅

**課題**  
`DictionaryServicePerformanceTests` は絶対時間（10 秒・15 秒）で合否判定しており、
CI 環境の性能差（低スペック runner、コンテナ等）でフレーキーになりうる。

**実装方針（選択式）**

**(A) 余裕を持たせた閾値（最小対応）**  
閾値を 30 秒・45 秒程度に拡大し、アルゴリズム回帰の検出は維持しつつ
環境差によるフレークを抑制する。

**(B) `[Trait]` で CI 除外**  
```csharp
[Trait("Category", "Performance")]
```
通常 CI からは除外し、パフォーマンス専用の週次ジョブ等で実行する。

**(C) 相対比較（高度）**  
ベースライン実行 N 回の中央値を基準に「3 倍以内」等の相対閾値で判定する。

**関連ファイル**

- `TxtToVoice.Tests/Services/DictionaryServicePerformanceTests.cs` — 閾値・Trait を調整

---

### 7. 最近使ったファイル（Recent Files） ✅

**課題**  
毎回ファイルダイアログを開く必要がある。同じファイルを繰り返し使う職員には非効率。

**実装方針**

- 設定ファイル（提案 1 参照）に `recentFiles: string[]`（最大 5 件）を追加
- `ファイル` メニューに動的サブメニューとして表示

---

### 8. SSML ポーズ自動挿入 ✅

`docs/speech-quality-improvement.md` を参照。
句読点（。！？、）や改行に `<break>` タグを自動挿入して自然な読み上げに近づける。
再生操作パネルの「句読点・改行に自動ポーズを挿入する（SSML モード）」チェックボックスで On/Off 切替可能。

---

### 9. ポータブルモード ✅

**課題**  
USB メモリや共有フォルダから実行したい場合、
辞書・ログが `%LOCALAPPDATA%` に書かれると持ち運べない。

**実装方針**

起動時に EXE と同じフォルダに `portable.flag` ファイルが存在する場合、
辞書・設定・ログをすべて EXE フォルダ配下に保存する。
`IPathProvider` インターフェースを導入して保存先を一括切り替えする構成が望ましい。

---

### 15. 大規模辞書向けパフォーマンス改善 ✅（ベースラインテスト追加）

**課題**  
現状の置換処理は全エントリを走査 + `IndexOf` 反復のため、
辞書件数が増加すると O(n²) 的な効率低下が発生しやすい。

**実装方針**

1. まずベンチマーク計測を追加し、実際のボトルネックを確認する
2. 問題がある場合は Aho-Corasick アルゴリズムへの段階移行を検討する
   （現状の件数規模では問題が顕在化しないことも多い）

---

### 16. README の機能説明更新 ✅

**課題**  
README の操作説明が「WAV 保存」中心で書かれており、v0.1.3 以降に追加された
MP3/MP4 保存・D&D ファイル読み込み・最近使ったファイル・SSML モード等が未掲載。
問い合わせ増加の原因になりうる。

**実装方針**

ユーザー向け操作手順を最新の UI に合わせて更新する。
スクリーンショットがあると操作説明の補足になる。

---

## 実装済み一覧

| バージョン | 機能 |
|---|---|
| v0.1.4 | MainWindow 分割・単体テスト・メニュー修正 |
| v0.1.5 | 設定の永続化・読み上げ位置ハイライト・音声保存の非同期化 |
| v0.1.6 | ドラッグ&ドロップ・セッション復元・辞書試し読みボタン・SSML ポーズ挿入 On/Off |
| v0.1.7 | 最近使ったファイル（Recent Files） |
| v0.1.8 | バージョン表示・ログ間引き・テスト拡充（CSV/AppSettings/Performance）・README 更新 |
| v0.1.9 | CSV 複数行セル対応・機微データ保存ポリシー UI・ポータブルモード |
| v0.2.0 | Shift_JIS 登録一本化（App.OnStartup）・ポータブルモード例外ダイアログのログパス修正・音声保存キャンセル UI |
| v0.2.1 | 機微データ消去の UI 文言改善（ログ非対象を明記）・ポータブルモード書込可否チェック（警告ダイアログ）・読み上げ位置ハイライト ON/OFF（蛍光イエロー） |
| v0.2.2 | キャンセル安全性修正（done.Wait(ct)・ObjectDisposedException ガード・エンコード後キャンセル確認）・未使用フィールド削除 |
| v0.2.3 | 監査モード INFO ログ抑制・終了時ログ削除オプション・ポータブルモード自動フォールバック（PathConfig 統合）・README v0.2.x 機能説明更新 |
| v0.2.4 | エンコード判定ドキュメント整合（UTF-16 BOM 対応を明記）・パフォーマンステスト `[Trait]`+閾値拡大・PathConfig / SpeechService キャンセルテスト追加 |
| v0.2.5 | 監査モード INFO 抑制の起動直後適用（`AppSettingsService.ReadAuditFlag()` + `App.OnStartup` 先読み）・テスト tautology 修正・README テスト一覧更新 |
| v0.3.0 | ISpeechEngine 抽象化（SystemSpeechEngine 切り出し）・WinRtSpeechEngine 実装・設定ダイアログにエンジン種別選択 UI 追加・TargetFramework を `net8.0-windows10.0.19041.0` に更新 |
| v0.3.1 | v0.3.0 ビルドエラー修正（`Timelines` プロパティ削除）・テストプロジェクト TargetFramework 修正・起動時 NullReferenceException 修正（ChkHighlight / ChkSsml / PreviewMode_Changed） |
| v0.3.2 | SpeechEngineFactory 新設（定数・Create・GetLabel）・BuildAppSettings 共通化リファクタ |
| v0.3.3 | WinRT 読み上げ位置ハイライト対応（TimedMetadataTracks / SpeechCue）・エンジン設定値の正規化・自己修復（IsKnown）・WinRT 保存処理 using 解放・SpeechEngineFactoryTests 追加（#34/#35/#36 部分/#37 部分） |
| v0.3.4 | 辞書ソートキャッシュ化（#26）・CSV 重複マージポリシー選択（#27）・保存進捗フェーズ表示（#28）・ログ匿名化（#30） |
| v0.3.5 | BuildAppSettings テスト可能化・AppSettingsBuilder 新設（#36）・ログ匿名化強化（#38）・v0.3.4 テスト拡充（#39）・CSV 重複判定 HashSet 最適化（#40）・音声選択 VoiceId 保存（#41） |
| v0.3.6 | テスト構成の分離・TxtToVoice.Core 新設（#29）・WinRT 保存の MemoryStream 廃止（#37） |
| v0.3.7 | WinRT 一時ファイル残存バグ修正（#42）・WAV 保存の異ドライブ対応（#43） |

## v0.1.9 レビュー査読結果

| 指摘 | 妥当性 | 対応状況 |
|---|---|---|
| Shift_JIS 登録を App.OnStartup に一本化 | **妥当（実バグ）** | → 項目 17 として追加 |
| ポータブルモード時の例外ダイアログのログパス修正 | **妥当（実バグ）** | → 項目 18 として追加 |
| 音声保存キャンセル UI の実装 | 妥当（既知残タスク） | 項目 3 に記載済み |
| 機微データ消去のログ扱いを明文化 | 妥当 | → 項目 19 として追加 |
| ポータブルモード起動時の書込可否チェック | 妥当 | → 項目 20 として追加 |
| 読み上げ位置をハイライト ON/OFF で切替可能に | 妥当（継続課題） | 項目 4 に記載済み |
| エンコード判定の README/コード整合 | 妥当（低優先度） | → 項目 21 として追加 |
| CI パフォーマンステスト閾値の見直し | 妥当（低優先度） | → 項目 22 として追加 |

---

## v0.2.1 レビュー査読結果

| 指摘 | 優先度 | 妥当性 | 対応状況 |
|---|---|---|---|
| `done.Wait()` → `done.Wait(ct)` に変更（エンジン無応答時に無限待機） | 高 | **妥当（実バグ）** | v0.2.2 で修正済み |
| `SpeakCompleted` コールバック内 `done.Set()` の `ObjectDisposedException` ガード | 高 | **妥当（実バグ）** | v0.2.2 で修正済み |
| NAudio エンコード後に `ct.ThrowIfCancellationRequested()` を追加 | 中 | 妥当（改善） | v0.2.2 で修正済み |
| `_showReadingHighlight` フィールドが未使用（`ChkHighlight.IsChecked` を直接参照している） | 中 | 妥当（コード品質） | v0.2.2 で削除済み |
| v0.2.1 新機能（ハイライト・書込チェック）の README 反映 | 低 | 妥当（低優先度） | → 項目 23 として追加 |
| v0.2.x 実装済み機能（ポータブル書込チェック・ハイライト ON/OFF）のテスト追加 | 低 | 妥当（低優先度） | → 項目 24 として追加 |

---

## v0.2.3 レビュー査読結果

| 指摘 | 優先度 | 妥当性 | 対応状況 |
|---|---|---|---|
| 監査モード INFO 抑制を起動直後から適用（App.OnStartup で先読み） | 中 | **妥当（動作上の制限）** | → 項目 25 として追加 |
| 設定ダイアログの依存関係 UI 明示化（ChkDeleteLogOnExit の IsEnabled 連動） | 中 | 妥当（UX 改善） | v0.2.3 修正済み（`IsEnabled` XAML バインディング追加） |
| About ダイアログに自動フォールバック状態を表示 | 低 | 妥当（運用改善） | v0.2.3 修正済み（`portableNote` に `PortableFallbackApplied` 分岐追加） |
| backlog #24 テスト追加（PathConfig / SpeechService キャンセル） | 低 | 妥当（既知課題） | backlog #24 に記載済み |
| backlog #21/#22 の計画化（エンコード範囲明記・CI 閾値対策） | 低 | 妥当（低優先度） | backlog #21/#22 に記載済み |

---

## v0.2.4 レビュー査読結果

| 指摘 | 優先度 | 妥当性 | 対応状況 |
|---|---|---|---|
| backlog #25 を実装（監査モード INFO 抑制を起動直後に適用） | 中 | **妥当（動作上の制限）** | v0.2.5 で実装済み |
| `SpeechService_初期化時に例外をスローしない` のトートロジー修正（`Record.Exception` + `Assert.Null` へ置換） | 中 | **妥当（テスト品質）** | v0.2.4 修正済み |
| README のテスト一覧に PathConfigTests / SpeechServiceCancelTests を追記 | 低 | 妥当（ドキュメント不整合） | v0.2.4 修正済み |

---

## v0.2.5 レビュー査読結果

| 指摘 | 優先度 | 妥当性 | 対応状況 |
|---|---|---|---|
| 辞書置換エンジンの高速化（都度ソート廃止・Aho-Corasick 移行・閾値再定義） | 中 | **妥当（性能改善）** | → 項目 26 として追加 |
| CSV インポート時の重複語句マージポリシー明確化（上書き/スキップ/確認） | 中 | **妥当（運用リスク）** | → 項目 27 として追加 |
| 音声保存進捗の可視化改善（フェーズ表示・キャンセル状態の明確化） | 低 | 妥当（UX 改善） | → 項目 28 として追加 |
| テスト構成の分離（Windows 依存 vs 純ロジック・中間キャンセルテスト追加） | 低 | 妥当（技術負債） | → 項目 29 として追加 |
| 監査強化モードのログ匿名化オプション（WARN/ERROR パスのマスキング） | 低 | 妥当（監査要件依存） | → 項目 30 として追加 |

---

## v0.3.5 レビュー査読結果（実装時の判断記録）

| 項目 | 対応内容 |
|---|---|
| #36 BuildAppSettings テスト | `AppSettingsBuilder` を `TxtToVoice.Services` に公開クラスとして追加。`MainWindow` は薄いラッパーに変更。`InternalsVisibleTo` で `AnonymizePaths` の internal テストも可能に |
| #38 ログ匿名化強化 | シングルクォート付き（.NET 例外型）・ダブルクォート付き・クォートなしドライブレター・クォートなし UNC の 4 パターン対応 |
| #39 v0.3.4 テスト追加 | DictionaryService キャッシュ/マージ・LoggerAnonymize・SpeechProgress の 4 テストクラスを追加。SpeechProgress は `[Trait("Category", "RequiresEngine")]` で CI 除外可 |
| #40 CSV 重複判定最適化 | `HashSet<string>` + 1 パスに変更。`HasDisplay()` / `UpdateByDisplay()` は他の呼び出し元（UI ボタン等）向けに残存 |
| #41 音声選択 ID 保存 | `voiceId` を `AppSettings` / `ISpeechEngine` / 両エンジンに追加。ロード時は ID 検索 → DisplayName フォールバックの 2 段構え |

---

## v0.3.6 レビュー査読結果

| 指摘 | 優先度 | 妥当性 | 対応状況 |
|---|---|---|---|
| WinRT 一時ファイル残存バグ（`Path.GetTempFileName()` → `.tmp` が残る） | 高 | **妥当（実バグ）** | → 項目 #42 として追加 |
| WAV 保存の `File.Move` 異ドライブ失敗耐性 | 高 | **妥当（実バグ）** | → 項目 #43 として追加 |
| README のテスト手順・構成図を v0.3.6 対応に更新 | 中 | 妥当（ドキュメント不整合） | → 項目 #44 として追加 |
| CI 2 レーン化（Core.Tests 必須 / Windows 依存テスト任意） | 中 | 妥当（品質改善） | → 項目 #45 として追加 |
| backlog #33 を調査フェーズから PoC 計画フェーズへ昇格 | 中 | 妥当（方向性明確化） | #33 を中優先度へ移動・PoC 計画を追記 |

---

## v0.3.7 レビュー査読結果（実装時の判断記録）

| 項目 | 対応内容 |
|---|---|
| #42 WinRT 一時ファイル残存バグ | `Path.GetTempFileName()` → `Path.GetRandomFileName()` に変更。`GetTempFileName()` はファイルを実際に生成するため音声保存ごとに `%TEMP%` に空の `.tmp` ファイルが蓄積する問題を修正 |
| #43 WAV 保存の異ドライブ対応 | `File.Move` → `File.Copy` に変更。`File.Move` は異なるボリューム間で `IOException` を投げるため、ネットワーク共有や別ドライブへの保存で失敗していた問題を修正。一時ファイルの削除は `finally` ブロックに委ねる設計のため `tempWavPath = string.Empty` フラグは不要になり削除 |

---

## v0.3.6 レビュー査読結果（実装時の判断記録）

| 項目 | 対応内容 |
|---|---|
| #29(A) テスト構成の分離 | `TxtToVoice.Core`（net8.0）を新設。`DictionaryService` / `CsvService` / `Logger` / `PathConfig` 等の純ロジック層を移動。`TxtToVoice` は Core を参照し、`SpeechEngineFactory` は `SpeechEngineTypes`（Core）に定数・検証を委譲 |
| #29(B) 中間キャンセルテスト | `TxtToVoice.Core.Tests`（net8.0）を新設して純ロジックテストを移動。エンジン依存テストは引き続き `TxtToVoice.Tests`（net8.0-windows）に残置。`SpeechServiceCancelTests` に `CancelAfter` 遅延キャンセルテストを 2 件追加（`[Trait("Category", "RequiresEngine")]` で CI 除外） |
| #37 WinRT MemoryStream 廃止 | `WinRtSpeechEngine.SaveToFile()` を一時 WAV ファイル経由に変更。`SpeechSynthesisStream → tempWavPath → 出力` の流れで長文合成時のピークメモリ使用量を削減。WAV の場合は `File.Move`、MP3/MP4 の場合は `WaveFileReader(tempWavPath)` でエンコード後に一時ファイルを削除 |

---

## v0.3.4 レビュー査読結果

| 指摘 | 優先度 | 妥当性 | 対応状況 |
|---|---|---|---|
| BuildAppSettings テストの追加（backlog #36 を 中→高 に昇格） | 高 | **妥当（回帰リスク）** | backlog #36 を高に昇格 |
| ログ匿名化の強化（空白パス・UNCパス対応） | 高 | **妥当（実装上の抜け）** | → 項目 #38 として追加 |
| v0.3.4 追加機能へのテスト追加（キャッシュ・マージ・匿名化・進捗） | 高 | **妥当（回帰防止）** | → 項目 #39 として追加 |
| CSV 重複判定の計算量最適化（HashSet 化・1 パス振り分け） | 中 | 妥当（性能改善） | → 項目 #40 として追加 |
| 音声選択の安定化（表示名ではなく ID 保存） | 中 | 妥当（運用安定性） | → 項目 #41 として追加 |
| backlog #29/#33/#37 は引き続き低優先度として継続 | 低 | 妥当（中長期テーマ） | backlog 低優先度に据え置き |

---

## 技術的負債（解消済み → v0.1.4）

| 項目 | 対応 |
|---|---|
| `MainWindow.xaml.cs` が 700 行超 | 4 つの partial class ファイルに分割 |
| エラーメッセージがユーザー向けと開発者向けで混在 | Logger へは詳細情報、MessageBox へはユーザー向けメッセージを分離（SpeechService, JsonPersistenceService）|
| ロジック層の単体テストがない | `TxtToVoice.Tests` プロジェクトを追加し `DictionaryService` の主要ロジックをカバー |
