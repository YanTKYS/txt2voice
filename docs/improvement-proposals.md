# 改善提案一覧

今後の開発で対応予定の改善事項をまとめたドキュメント。
優先度順に記載。実装時はこのリストから削除またはステータスを更新すること。

---

## 優先度：高（v0.1.5 で実装済み）

### 1. 設定の永続化 ✅

**課題**  
速度・音量スライダーの値と選択中の音声が、アプリ再起動のたびにリセットされる。

**実装方針**

- 保存先: `%LOCALAPPDATA%\TxtToVoice\settings.json`
- 保存タイミング: スライダー値変更時・音声選択変更時・ウィンドウクローズ時
- 読み込みタイミング: `MainWindow` コンストラクタの末尾（`InitializeVoiceCombo()` の後）

**保存する設定項目**

| キー | 型 | 説明 |
|---|---|---|
| `rate` | int | 読み上げ速度（-10〜10） |
| `volume` | int | 音量（0〜100） |
| `voiceName` | string | 選択音声名 |

**関連ファイル**

```
TxtToVoice/
├── Models/AppSettings.cs              # 新規: 設定モデル
├── Services/AppSettingsService.cs     # 新規: 設定の読み書き（JsonPersistenceService と同パターン）
└── MainWindow.PlaybackOperations.cs   # 変更: スライダー変更時に SaveSettings() を呼ぶ
```

---

### 2. ドラッグ&ドロップでファイルを開く ✅

**課題**  
テキストファイルをウィンドウにドロップして開けない。現場でのファイル操作が多い職員には不便。

**実装方針**

XAML の `Window` 要素に `AllowDrop="True"` を追加し、`Drop` イベントを処理する。

---

### 3. 音声保存の非同期化 ✅（キャンセル UI は未実装）

**課題**  
`SpeechService.SaveToFile()` は同期処理のため、長い原稿（数千文字）を MP3 保存すると
UI スレッドがブロックされ、ウィンドウが「応答なし」状態になる。

**実装方針（済）**

- `SaveToFile()` に `CancellationToken` を受け取る非同期版 `SaveToFileAsync()` を追加
- `MainWindow.PlaybackOperations.cs` の `SaveAudio()` を `async void SaveAudio()` に変更

**残タスク: 保存進捗ダイアログ＋キャンセル**

非同期化はできているが、キャンセル UI がないため長文保存時の運用性がまだ弱い。

- モーダルダイアログに「キャンセル」ボタンを設ける
- `SaveAudio()` 内で `CancellationTokenSource` を生成してダイアログに渡す
- キャンセル時は保存途中ファイルを削除する

```csharp
// SaveAudio() に追加するイメージ
using var cts = new CancellationTokenSource();
var progressDialog = new SaveProgressDialog(cts) { Owner = this };
progressDialog.Show();
try
{
    await _speechService.SaveToFileAsync(content, dlg.FileName, format, isSsml: useSsml, ct: cts.Token);
}
catch (OperationCanceledException)
{
    try { File.Delete(dlg.FileName); } catch { }
    SetStatus("音声保存をキャンセルしました。");
}
finally { progressDialog.Close(); }
```

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

### 4. 読み上げ位置のハイライト ✅（ステータスバー表示に変更済み）

**課題**  
どこを読んでいるか視覚的にわからない。長い原稿を確認しながら聞く用途で不便。

**実装状況**

- `SpeechService.SpeakProgress` イベントによる進捗通知は実装済み
- v0.1.7 時点では `TxtInput.Select()` による選択ハイライトをステータスバー表示
  （`読み上げ中... (45 / 200 文字)`）に置き換えた
  → **理由**: 複数行にまたがる青い選択表示がユーザーの誤操作と混同されるため

**レビュー指摘（v0.1.7 レビュー）**

選択ハイライトを ON/OFF トグルで切り替えられるようにする案が提起された。

**今後の方針（案）**

再生操作パネルにチェックボックス「読み上げ位置をハイライト表示する」を追加し、
ON 時は `TxtInput.Select(pos, len) + ScrollToLine()`、
OFF 時は現在のステータスバー表示のまま、とする。
SSML モード中はマッピング不可のため常に OFF。

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

## 技術的負債（解消済み → v0.1.4）

| 項目 | 対応 |
|---|---|
| `MainWindow.xaml.cs` が 700 行超 | 4 つの partial class ファイルに分割 |
| エラーメッセージがユーザー向けと開発者向けで混在 | Logger へは詳細情報、MessageBox へはユーザー向けメッセージを分離（SpeechService, JsonPersistenceService）|
| ロジック層の単体テストがない | `TxtToVoice.Tests` プロジェクトを追加し `DictionaryService` の主要ロジックをカバー |
