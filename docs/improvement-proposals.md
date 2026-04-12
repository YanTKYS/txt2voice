# 改善提案一覧

全提案の詳細・経緯を管理するドキュメント。
**未着手の項目のみを素早く確認したい場合は [docs/backlog.md](./backlog.md) を参照すること。**

---

## 優先度：高

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

## 優先度：中

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

### 21. テキスト読み込みエンコード判定の README/コード整合

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

### 24. v0.2.x 向けテスト追加

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

### 22. CI パフォーマンステスト閾値の環境依存対策

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

## 技術的負債（解消済み → v0.1.4）

| 項目 | 対応 |
|---|---|
| `MainWindow.xaml.cs` が 700 行超 | 4 つの partial class ファイルに分割 |
| エラーメッセージがユーザー向けと開発者向けで混在 | Logger へは詳細情報、MessageBox へはユーザー向けメッセージを分離（SpeechService, JsonPersistenceService）|
| ロジック層の単体テストがない | `TxtToVoice.Tests` プロジェクトを追加し `DictionaryService` の主要ロジックをカバー |
