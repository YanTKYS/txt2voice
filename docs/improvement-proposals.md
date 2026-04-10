# 改善提案一覧

今後の開発で対応予定の改善事項をまとめたドキュメント。
優先度順に記載。実装時はこのリストから削除またはステータスを更新すること。

---

## 優先度：高

### 1. 設定の永続化

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

### 2. ドラッグ&ドロップでファイルを開く

**課題**  
テキストファイルをウィンドウにドロップして開けない。現場でのファイル操作が多い職員には不便。

**実装方針**

XAML の `Window` 要素に `AllowDrop="True"` を追加し、`Drop` イベントを処理する。

```xml
<Window ... AllowDrop="True" Drop="Window_Drop" DragOver="Window_DragOver">
```

```csharp
// MainWindow.FileOperations.cs に追加
private void Window_DragOver(object sender, DragEventArgs e)
{
    e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
        ? DragDropEffects.Copy
        : DragDropEffects.None;
    e.Handled = true;
}

private void Window_Drop(object sender, DragEventArgs e)
{
    if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        LoadTextFile(files[0]);
}
```

---

### 3. 音声保存の非同期化

**課題**  
`SpeechService.SaveToFile()` は同期処理のため、長い原稿（数千文字）を MP3 保存すると
UI スレッドがブロックされ、ウィンドウが「応答なし」状態になる。

**実装方針**

- `SaveToFile()` に `CancellationToken` を受け取る非同期版 `SaveToFileAsync()` を追加
- `MainWindow.PlaybackOperations.cs` の `SaveAudio()` を `async void SaveAudio()` に変更
- 保存中はプログレスダイアログ（モーダル）を表示してキャンセルボタンを設ける

```csharp
// SpeechService に追加
public async Task SaveToFileAsync(string text, string outputPath, AudioFormat format,
    CancellationToken ct = default)
{
    await Task.Run(() => SaveToFile(text, outputPath, format), ct);
}
```

---

## 優先度：中

### 4. 読み上げ位置のハイライト

**課題**  
どこを読んでいるか視覚的にわからない。長い原稿を確認しながら聞く用途で不便。

**実装方針**

- `SpeechSynthesizer.SpeakProgress` イベントを `SpeechService` で受け取り
  `SpeakProgressEventArgs.CharacterPosition` と `CharacterCount` を UI に通知
- `TxtInput.Select(pos, len)` でハイライト

**制約**  
辞書変換後テキストで読み上げているため、元テキストとの位置がずれる。
変換なしの場合のみハイライトを有効にするか、将来的に変換マッピングを保持する設計が必要。

---

### 5. 入力テキストのセッション復元

**課題**  
アプリ終了前の入力内容が消えるため、再起動のたびに原稿を貼り直す必要がある。

**実装方針**

設定ファイル（提案 1 参照）に `lastInputText` フィールドを追加し、
ウィンドウクローズ時に保存・起動時に復元する。
文字数が多い場合（例: 10,000 字超）は保存しない制限を設けるとよい。

---

### 6. 辞書エントリの試し読みボタン

**課題**  
辞書編集ダイアログで「読み」を登録しても、実際の発音を確認するには
いったん閉じてプレビューを見る必要がある。

**実装方針**

`DictionaryEntryDialog.xaml` に「試し読み」ボタンを追加し、
`SpeechService.SpeakAsync(Reading)` を呼ぶ。

---

## 優先度：低

### 7. 最近使ったファイル（Recent Files）

**課題**  
毎回ファイルダイアログを開く必要がある。同じファイルを繰り返し使う職員には非効率。

**実装方針**

- 設定ファイル（提案 1 参照）に `recentFiles: string[]`（最大 5 件）を追加
- `ファイル` メニューに動的サブメニューとして表示

---

### 8. SSML ポーズ自動挿入

`docs/speech-quality-improvement.md` を参照。
句読点（。！？、）や改行に `<break>` タグを自動挿入して自然な読み上げに近づける。

---

### 9. ポータブルモード

**課題**  
USB メモリや共有フォルダから実行したい場合、
辞書・ログが `%LOCALAPPDATA%` に書かれると持ち運べない。

**実装方針**

起動時に EXE と同じフォルダに `portable.flag` ファイルが存在する場合、
辞書・設定・ログをすべて EXE フォルダ配下に保存する。

---

## 技術的負債（解消済み → v0.1.4）

| 項目 | 対応 |
|---|---|
| `MainWindow.xaml.cs` が 700 行超 | 4 つの partial class ファイルに分割 |
| エラーメッセージがユーザー向けと開発者向けで混在 | Logger へは詳細情報、MessageBox へはユーザー向けメッセージを分離（SpeechService, JsonPersistenceService）|
| ロジック層の単体テストがない | `TxtToVoice.Tests` プロジェクトを追加し `DictionaryService` の主要ロジックをカバー |
