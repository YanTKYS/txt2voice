# 声の広報 テキスト読み上げツール

自治体職員が「声の広報」原稿を読み上げ用に整え、住民向けに聞きやすい音声を再生・保存できるツールです。

---

## 画面構成

```
┌──────────────────────────────────────────────────────────────────────┐
│ [ファイル（最近使ったファイル含む）] [辞書] [ヘルプ]                     │
├──────────────────────────┬───────────────────────────────────────────┤
│                          │ 辞書補正後プレビュー                        │
│  原稿入力                │ （変換箇所を【元表記→読み】で表示）          │
│                          ├───────────────────────────────────────────┤
│  ここに読み上げる原稿を   │ 辞書一覧                                   │
│  入力・貼り付け・         │ [追加] [編集] [削除] [CSV読込] [CSV出力]   │
│  ファイル読込・D&Dドロップ ├───────────────────────────────────────────┤
│                          │ 再生操作                                   │
│  [ファイルを開く][クリア] │ 音声選択 / 速度・音量スライダー             │
│                          │ ☐ 句読点・改行に自動ポーズを挿入（SSML）   │
│  X,XXX 文字  / 読み上げ 約 X 分  │ ☑ 読み上げ中の位置を蛍光色でハイライト表示する │
│                          │ [読み上げ開始][一時停止][再開][停止][音声保存]│
└──────────────────────────┴───────────────────────────────────────────┘
│ ステータスバー（読み上げ中は「読み上げ中... (45/200 文字)」を表示）        │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 動作要件

| 項目 | 内容 |
|------|------|
| OS | Windows 10 / 11 |
| ランタイム | 不要（自己完結型 EXE — .NET ランタイムを同梱） |
| 音声合成 | Windows 標準音声エンジン（SAPI / WinRT OneCore、追加インストール不要）または OpenJTalk（要セットアップ） |
| インターネット | 不要（完全オフライン動作） |

---

## インストール方法

1. [Releases](https://github.com/YanTKYS/txt2voice/releases) から最新の `TxtToVoice-vX.X.X-win-x64.zip` をダウンロード
2. ZIP を任意のフォルダに展開（例: `C:\Tools\TxtToVoice\`）
3. `TxtToVoice.exe` をダブルクリックして起動

> .NET ランタイムのインストールは不要です（EXE にランタイムを同梱済み）。

### OpenJTalk エンジンを使う場合（初回のみ）

```powershell
# TxtToVoice.exe と同じフォルダで実行
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\setup_openjtalk.ps1
```

セットアップ後、設定ダイアログで「OpenJTalk」を選択して再起動してください。

---

## ビルド手順

### 前提条件

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) がインストールされていること
- Windows 環境（WPF は Windows のみ）

### ビルド

```powershell
cd TxtToVoice
dotnet restore
dotnet build -c Release
```

ビルド成果物は `TxtToVoice\bin\Release\net8.0-windows10.0.19041.0\` に出力されます。

### テスト

テストプロジェクトは 2 つあります。

```powershell
# 純ロジックテスト（OS 非依存・全環境で実行可）
dotnet test TxtToVoice.Core.Tests/TxtToVoice.Core.Tests.csproj

# Windows 依存テスト（音声エンジン系・エンジン不在の CI はフィルタ除外）
dotnet test TxtToVoice.Tests/TxtToVoice.Tests.csproj --filter "Category!=RequiresEngine"
```

| プロジェクト | 対象 | 実行環境 |
|---|---|---|
| `TxtToVoice.Core.Tests` | 辞書・CSV・設定・Logger 等の純ロジック | Windows / Linux / macOS |
| `TxtToVoice.Tests` | 音声エンジン系（SAPI / WinRT / OpenJTalk） | Windows のみ |

### 実行

```powershell
dotnet run --project TxtToVoice
```

---

## 発行手順（スタンドアロン配布）

```powershell
cd TxtToVoice
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o ..\publish
```

`publish\` フォルダの内容を配布先 PC にコピーするだけで動作します。
（.NET ランタイムのインストールも不要）

---

## 使い方

### 基本的な流れ

1. **原稿を入力する**
   - テキストを直接入力するか、「ファイルを開く」で .txt ファイルを読み込みます
   - テキストファイルをウィンドウに**ドラッグ&ドロップ**して開くこともできます
   - 「ファイル」→「最近使ったファイル」から以前開いたファイルをすぐに再読込できます
   - UTF-8（BOM あり・なし）/ UTF-16（BOM あり: LE / BE）/ Shift_JIS を自動判別します
   - 前回終了時のテキストは次回起動時に自動復元されます（10,000 文字以内）

2. **辞書補正後プレビューを確認する**
   - 「辞書を適用してプレビュー更新」ボタン（または Ctrl+P）を押します
   - プレビューエリアに `【市長→しちょう】` のように変換箇所が表示されます
   - 内容を確認し、必要であれば辞書を追加・修正します

3. **読み上げる**
   - 「読み上げ開始」ボタン（または F5）を押します
   - テキストを選択してから F5 を押すと**選択範囲のみ**読み上げます
   - 速度・音量スライダーで調整できます
   - 「句読点・改行に自動ポーズを挿入する」をオンにすると、句読点（。！？、）や
     改行の後に自然な間が入ります（SSMLモード）
   - **「読み上げ中の位置を蛍光色でハイライト表示する」** をオンにすると、現在読み上げている
     単語を蛍光イエローで強調表示します（通常の文字選択とは異なる色で識別できます）
   - 読み上げ中はステータスバーに進捗が表示されます（例: 読み上げ中... 45/200 文字）

4. **音声エンジンを切り替える（オプション）**
   - 「ファイル」→「設定」ダイアログの「音声エンジン」で切り替えられます
   - **再生停止中は即時反映**されます。再生中の場合は次回起動時に反映されます
   - 切り替え後は音声選択コンボボックスから改めて音声を選択してください

   | エンジン | 特徴 | ハイライト | 推奨用途 |
   |---------|------|-----------|---------|
   | SAPI（既定） | Windows 標準。安定動作 | ✅ 対応 | 通常の読み上げ・位置確認 |
   | WinRT（OneCore） | OneCore 系音声。発音が自然になる場合あり | ❌ 非対応 | 音声品質を優先したい場合 |
   | OpenJTalk | 日本語専用 TTS。要セットアップ（後述） | ❌ 非対応 | 日本語特化の読み上げ品質を試したい場合 |

   > **WinRT の制限**: 読み上げ箇所ハイライトは非対応です。エンジン切り替え後に音声を再選択してください。

   #### OpenJTalk 利用前セットアップ（初回のみ）

   OpenJTalk エンジンは外部バイナリが必要です。リリース ZIP に同梱の `setup_openjtalk.ps1` を実行してください。

   ```powershell
   # TxtToVoice.exe と同じフォルダで実行
   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
   .\setup_openjtalk.ps1
   ```

   スクリプトが `jtalk.dll`・MeCab 辞書・HTS 音声モデルを自動取得・配置します。

   ```
   # セットアップ後の設定
   設定ダイアログ → 「OpenJTalk」を選択 → 再起動
   ```

   > ファイルが揃っていない場合は起動時にエラーメッセージで案内します。
   > HTS Voice "Mei" は CC BY 3.0 ライセンスです（クレジット: 名古屋工業大学）。
   > `THIRD_PARTY_LICENSES.txt` を参照してください。

5. **音声ファイルとして保存する**
   - 「音声保存」ボタン（または Ctrl+S）を押します
   - **MP3 / WAV / MP4（AAC）** から保存形式を選択できます
   - 辞書補正済みテキストが保存されます。SSML モードがオンの場合はポーズも反映されます
   - 保存中はプログレスダイアログが表示され、**「キャンセル」ボタン**で中断できます
   - **保存プリセット**: よく使う保存設定（ファイル名テンプレート・形式・SSML 強度）をプリセット登録して即実行できます

6. **テンプレートを使う**
   - Ctrl+T またはメニュー「ファイル → テンプレート」でテンプレート管理ダイアログを開きます
   - タイトルと内容を登録しておくと、原稿に即座に挿入できます
   - `{今日}` `{now}` などの予約変数を使うと挿入時に現在日時が自動入力されます
   - `{変数名}` のプレースホルダを使うと挿入ダイアログで任意の値に置換できます
   - よく使うテンプレートは ★ アイコンでピン留めして一覧の先頭に固定できます

7. **段落・セクションで読む範囲を指定する**
   - 原稿入力エリアの **◀ 段落 ▶** ボタンで行単位に移動し、F5 でその段落だけ読み上げます
   - 「■第1章」「◆はじめに」などの見出し行は**セクションナビ**のドロップダウンで一覧表示され、クリックでジャンプ・再生できます

8. **読み上げキューを使う**
   - 「＋ キュー」ボタンで現在のテキストをキューに追加します（同じテキストの重複は自動スキップ）
   - 「▶ 順次再生」でキュー内のアイテムを順番に読み上げます
   - キューの並び替えは ↑↓ ボタン、削除は「削除」ボタンまたは Delete キー（複数選択可）
   - 設定で「読み上げキューを再起動後も復元する」をオンにすると、次回起動時にキューが復元されます

9. **読みルールを管理する**
   - Ctrl+L または「辞書」メニューから読みルール編集ダイアログを開きます
   - 正規表現ベースの変換ルール（例: `(\d+)月` → `$1がつ`）を登録・順序変更できます
   - ルールは `Data\text_rules.json` に保存され、テキスト変換時に辞書適用の前に実行されます

### ショートカットキー

#### 全体

| キー | 操作 |
|------|------|
| Ctrl+O | テキストファイルを開く |
| Ctrl+P | 辞書を適用してプレビュー更新 |
| Ctrl+S | 音声ファイルとして保存（MP3 / WAV / MP4） |
| Ctrl+T | テンプレート挿入ダイアログを開く |
| Ctrl+L | 読みルール編集ダイアログを開く |
| F5 | 読み上げ開始（選択範囲があれば選択範囲のみ） |
| F6 | 一時停止 |
| F7 | 再開 |
| F8 | 停止 |

#### 段落・セクション・プレビューナビ

| キー | 操作 |
|------|------|
| Alt+↑ / Alt+↓ | 前の段落 / 次の段落へジャンプ |
| Alt+Shift+↑ / Alt+Shift+↓ | 前のセクション / 次のセクションへジャンプ |
| Alt+← / Alt+→ | プレビューの前の変換箇所 / 次の変換箇所へジャンプ |

#### 辞書一覧

| キー | 操作 |
|------|------|
| Ins | エントリ追加 |
| F2 | 選択エントリ編集 |
| Del | 選択エントリ削除（複数選択可） |
| Ctrl+Shift+↑ | 選択エントリを上に移動 |
| Ctrl+Shift+↓ | 選択エントリを下に移動 |

#### 読み上げキュー

| キー | 操作 |
|------|------|
| Del（キュー選択中） | 選択アイテムを削除（複数選択可） |

---

## 辞書機能

### 辞書エントリの項目

| 項目 | 説明 |
|------|------|
| 表記 | 原稿内に現れる文字列（例: 市長） |
| 読み | 読み上げ時に置き換える文字列（例: しちょう） |
| 備考 | 担当者向けのメモ（例: 誤読防止） |
| 優先順位 | 1〜100。大きいほど優先（通常は 50 で十分） |

辞書編集ダイアログの「試し読み」ボタンで、登録した読みをその場で確認できます。

### 置換ルール

1. **長い語句を優先**して先にマッチングします
2. 同じ長さの場合は「優先順位」の大きい方を適用します
3. 一度置換された範囲は二重置換しません

### サンプル辞書

初回起動時に `Data\sample_dictionary.json` から自治体業務向けの語句が自動的に読み込まれます。
「辞書」メニュー → 「サンプル辞書を読み込む」でいつでも再読み込みできます。

### CSV インポート / エクスポート

列順：`表記,読み,備考,優先順位`

```csv
表記,読み,備考,優先順位
市長,しちょう,役職名,80
○○市,まるまるし,市名,90
```

UTF-8（BOM あり・なし）/ Shift_JIS を自動判別して読み込みます。
RFC 4180 に準じた複数行セル（引用符内の改行）にも対応しています。

---

## ファイルの保存場所

### 通常モード

| ファイル | パス | 説明 |
|---------|------|------|
| 辞書 | `%LOCALAPPDATA%\TxtToVoice\dictionary.json` | |
| 設定 | `%LOCALAPPDATA%\TxtToVoice\settings.json` | |
| テンプレート | `%LOCALAPPDATA%\TxtToVoice\templates.json` | ピン留め・利用回数も保存 |
| 再生プロファイル | `%LOCALAPPDATA%\TxtToVoice\profiles.json` | |
| 保存プリセット | `%LOCALAPPDATA%\TxtToVoice\save_presets.json` | |
| 読み上げキュー | `%LOCALAPPDATA%\TxtToVoice\queue.json` | 永続化 ON 時のみ |
| 監査ログ | `%LOCALAPPDATA%\TxtToVoice\audit_YYYYMM.csv` | 監査機能 ON 時のみ |
| アプリログ | `%LOCALAPPDATA%\TxtToVoice\logs\app_YYYYMMDD.log` | |
| テキスト変換ルール | `<EXEフォルダ>\Data\text_rules.json` | アプリ同梱・ユーザー編集可 |

`%LOCALAPPDATA%` は通常 `C:\Users\<ユーザー名>\AppData\Local` です。

### ポータブルモード

EXE と同じフォルダに `portable.flag` ファイルを置くと**ポータブルモード**で起動します。
上記のすべてのファイルが `%LOCALAPPDATA%` ではなく EXE フォルダ配下に保存されます。

> EXE フォルダへの書き込みができない場合は自動的に通常モードへ切り替わり、起動時に通知ダイアログが表示されます。

---

## 機微データ保存ポリシー（監査向け機能）

「ファイル」→「設定」ダイアログで以下のポリシーを設定できます。

| オプション | 説明 |
|-----------|------|
| 前回テキストを保存する | オフにすると起動ごとにテキストエリアが空欄になります |
| 最近使ったファイルを保存する | オフにすると「最近使ったファイル」メニューが非表示になります |
| 終了時に入力テキスト・ファイル履歴を消去する（監査向け） | 終了時にテキスト・履歴を消去し、INFO ログの書き込みも抑制します |
| 終了時にログファイルも削除する（監査向け・強化オプション） | 上の「消去する」がオンのときに有効。終了時にその日のログファイルを削除します |

---

## トラブルシューティング

### 「利用可能な音声エンジンが見つかりません」と表示される

Windows の「コントロールパネル」→「音声認識」→「テキスト読み上げ」から
インストール済みの音声エンジンを確認してください。
日本語音声が入っていない場合は、Windows の言語設定から「日本語」の音声パッケージを追加してください。

### WinRT エンジン選択後に音声が一覧に表示されない

WinRT（OneCore）エンジンの音声は SAPI の音声とは別の名前体系です
（例: SAPI `"Microsoft Haruka Desktop"` → WinRT `"Microsoft Haruka"` 等）。
エンジンを切り替えた後は起動時に「音声選択」から改めて音声を選択してください。
OneCore 音声がインストールされていない場合は「設定」→「時刻と言語」→「音声認識」で
日本語の音声パッケージを追加してください。

### WinRT エンジンで読み上げ箇所のハイライトが表示されない（SSML モード）

WinRT エンジンでの読み上げ箇所ハイライトは v0.3.3 以降で対応済みです。
ただし「句読点・改行に自動ポーズを挿入する（SSML）」がオンの場合は SAPI と同様にハイライトは無効です。
SSML をオフにした状態でハイライトをご確認ください。

### 「辞書ファイルが破損しているか読み込めません」と表示される

辞書ファイル（`%LOCALAPPDATA%\TxtToVoice\dictionary.json`）が壊れています。
ファイルを削除して再起動すると、サンプル辞書が自動的に読み込まれます。

### 音声ファイルが保存できない

保存先フォルダの書き込み権限を確認してください。
デスクトップやドキュメントフォルダへの保存を試してください。

### MP3 / MP4 保存でエラーが出る

MP3 / MP4（AAC）の保存には Windows Media Foundation が必要です。
Windows 10 / 11 では標準で利用可能ですが、Server OS や最小構成では
「メディア機能」を有効化する必要があります。

### 「ポータブルモード — 通常モードへ自動切替」と表示される

EXE フォルダへの書き込みができないため、保存先が `%LOCALAPPDATA%\TxtToVoice` に
自動切替されています。ポータブルモードを使う場合は、EXE フォルダのアクセス権限を確認してください。

---

## ソースコード構成

```
TxtToVoice.Core/                        OS 非依存の純ロジック層（net8.0）
├── Models/
│   ├── AppSettings.cs                  アプリ設定モデル
│   ├── DictionaryEntry.cs              辞書エントリ
│   ├── PlaybackProfile.cs              再生プロファイル
│   ├── QueueEntry.cs                   読み上げキュー・履歴エントリ
│   ├── SavePreset.cs                   保存プリセット
│   ├── Template.cs                     原稿テンプレート（ピン留め対応）
│   └── TextRule.cs                     テキスト変換ルール
└── Services/
    ├── AhoCorasick.cs                  辞書照合アルゴリズム（O(n+m)）
    ├── AppSettingsBuilder.cs           設定値の構築
    ├── AppSettingsService.cs           設定 JSON 読み書き
    ├── AuditLogger.cs                  監査 CSV ログ出力（月次ローテーション）
    ├── CsvImportReport.cs              CSV インポート結果レポート
    ├── CsvService.cs                   CSV インポート / エクスポート
    ├── DictionaryService.cs            辞書管理・テキスト置換（Aho-Corasick）
    ├── FileNameBuilder.cs              ファイル名命名テンプレート展開
    ├── JsonPersistenceService.cs       JSON 汎用読み書き
    ├── Logger.cs                       ファイルログ（INFO 抑制・終了時削除対応）
    ├── OperationalPackService.cs       運用パック ZIP エクスポート/インポート
    ├── PathConfig.cs                   保存先パス管理（通常/ポータブルモード）
    ├── ProfileService.cs               再生プロファイル管理
    ├── QueuePersistenceService.cs      読み上げキュー永続化
    ├── SavePresetService.cs            保存プリセット管理
    ├── SpeechEngineTypes.cs            音声エンジン種別定数
    ├── SpeechPositionMap.cs            読み上げ位置マッピング
    ├── SsmlBuilder.cs                  テキスト → SSML 変換
    ├── TemplateService.cs              テンプレート管理
    ├── TextPreprocessor.cs             テキスト前処理（数字・記号の読み変換）
    ├── TextRuleLoader.cs               text_rules.json 読み込み・Regex タイムアウト
    └── TtvErrorCode.cs                 エラーコード定数

TxtToVoice/                             WPF アプリ本体（net8.0-windows10.0.19041.0）
├── App.xaml / App.xaml.cs
├── PlaybackState.cs                    再生状態 sealed record（Idle/Active/Paused）
├── MainWindow.xaml                     メイン画面レイアウト
├── MainWindow.xaml.cs                  フィールド・初期化・共通ユーティリティ
├── MainWindow.DictionaryOperations.cs  辞書 CRUD・プレビュー・CSV 入出力
├── MainWindow.EditingOperations.cs     段落/セクションナビ・テキスト編集
├── MainWindow.FileOperations.cs        ファイル開く・D&D
├── MainWindow.HistoryOperations.cs     読み上げ履歴（直近 20 件）
├── MainWindow.PlaybackOperations.cs    読み上げ・音声保存・プロファイル
├── MainWindow.QueueOperations.cs       読み上げキュー・順次再生・永続化
├── MainWindow.SettingsOperations.cs    設定読み書き・エンジン切替
├── Dialogs/
│   ├── BatchSaveResultDialog           一括保存結果ダイアログ
│   ├── DictionaryEntryDialog           辞書編集ダイアログ
│   ├── InputDialog                     汎用テキスト入力ダイアログ
│   ├── PlaceholderDialog               テンプレートプレースホルダ置換
│   ├── SavePresetDialog                保存プリセット登録
│   ├── SaveProgressDialog              音声保存進捗（キャンセル対応）
│   ├── SettingsDialog                  設定ダイアログ（基本/詳細タブ）
│   ├── TemplateEntryDialog             テンプレート編集
│   ├── TemplateManagerDialog           テンプレート管理（ピン留め・ソート）
│   ├── TextRuleDialog                  読みルール一覧・上下移動・無効診断
│   └── TextRuleEntryDialog             読みルール編集
├── Services/
│   ├── ISpeechEngine.cs
│   ├── NativeJTalk.cs                  OpenJTalk P/Invoke ラッパー
│   ├── OpenJTalkEngine.cs
│   ├── SpeechEngineFactory.cs
│   ├── SpeechService.cs                音声合成ラッパー（WAV/MP3/MP4・キャンセル）
│   ├── SystemSpeechEngine.cs           SAPI エンジン
│   └── WinRtSpeechEngine.cs            WinRT OneCore エンジン
└── Data/
    ├── sample_dictionary.json
    └── text_rules.json                 テキスト変換ルール（ユーザー編集可）
```
