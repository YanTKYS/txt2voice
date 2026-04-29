# 改善提案一覧

全提案の詳細・経緯を管理するドキュメント。
**未着手の項目のみを素早く確認したい場合は [docs/backlog.md](./backlog.md) を参照すること。**

---

## v0.7.9 以降の機能追加候補（2026-04-29 整理）

v0.7.8 までで UI/UX の主要改善（再生プロファイル / 段落・セクションナビ / セクション再生 / 一括保存 / 辞書一括操作・Undo / 運用パック）はかなり充実した。
監査・運用ログ系は本番運用後の課題として保留し、**機能追加・体感改善を優先する方針**で次の打ち手を整理する。

### 優先度 高 — 次の 1〜2 版で実装する価値が高い

#### #119 読み上げ進捗インジケータ＋残り時間表示

**課題**: 再生中の視覚フィードバックが「再生/一時停止/停止」ボタンの状態のみ。長文の校正中に「あとどれくらいで終わるか」が分からずストレスが大きい。

**提案**:
- `SpeechService` に `IProgress<SpeechProgress>` を追加（現在位置・推定総時間・経過時間）
  - 各エンジン（SAPI/WinRT/OpenJTalk）の SpeakProgress イベント or タイマーで進捗を発行
- 再生コントロールエリアに `ProgressBar` を追加（速度・音量変更時もリアルタイム更新）
- 「残り時間」ラベルを表示（既存の `UpdateEstimatedTime()` を再生中専用に拡張）

**注意**:
- SAPI: `SpeechSynthesizer.SpeakProgress` イベントが利用可能
- WinRT: `MediaElement.Position` をポーリング
- OpenJTalk: ファイル再生のため `MediaElement.Position` で取得可能

#### #120 テンプレート利用履歴（最近使ったテンプレート） ✅ v0.7.9

**課題**: テンプレート数が増えると目的のものを探す時間が増える。検索（#109）はあるが、毎週使うテンプレが上位に並ばない。

**提案**:
- `Template` モデルに `LastUsedAt: DateTimeOffset?`、`UsageCount: int` を追加
- 挿入時にこれらを更新して `templates.json` に保存
- TemplateManagerDialog の DataGrid に「最近使った順」/「タイトル順」/「使用回数順」のソート切替
- デフォルトは「最近使った順」

#### #121 テンプレート予約変数（{today}/{now}/{month}/{year}/{weekday}） ✅ v0.7.9

**課題**: テンプレート挿入時に「2026年4月」「本日」「来週月曜日」等を毎回手入力するのは煩雑。

**提案**:
- `PlaceholderDialog` 側で予約変数を検知し、初期値を自動入力
  - `{today}` = 「2026年4月29日」
  - `{now}` = 「14:30」
  - `{month}` = 「4月」
  - `{year}` = 「2026年」
  - `{weekday}` = 「水曜日」
- ユーザー定義変数は従来通り空欄から入力
- ダイアログ内で「予約変数 ▼」ボタンを押すと和暦/西暦切替や定型句選択が可能（拡張余地）

#### #122 保存プリセット

**課題**: 配布シナリオごとに「形式・命名テンプレート・SSML 設定・速度」が異なるが、毎回設定し直す必要がある。

**提案**:
- 既存の「再生プロファイル」とは別に「保存プリセット」を追加
  - 名前（例: 「週次広報MP3」「庁内放送WAV」）
  - 保存形式（単体 or 一括）
  - 命名テンプレート
  - SSML 強度
- 音声保存ボタンの右隣にプリセット選択 ComboBox を配置
- プリセット適用 → 即保存ダイアログのワンクリック動線

---

### 優先度 中 — 高優先度の後に着手

#### #123 プレビュー比較ペイン

**課題**: 現在の注釈付きプレビューは便利だが、辞書適用前後を並べて比較する場面では行ごと対応が分かりづらい。

**提案**:
- プレビュー領域にトグル「比較表示」を追加
- ON 時: 左ペイン=元テキスト、右ペイン=辞書適用後、行同期スクロール
- OFF 時: 既存の単一プレビュー（注釈/プレーンモード）

#### #124 段落/行番号表示トグル

**課題**: 校正担当者間で「○行目を直して」とコミュニケーションする際、TxtInput に行番号がないため数えにくい。

**提案**:
- 設定ダイアログに「行番号を表示」チェックボックス追加
- ON 時: TxtInput の左に行番号カラムを追加（カスタム描画 or サードパーティコントロールなしで `RichTextBox + LineNumberMargin` で実装）

#### #125 ショートカット拡張（Alt 系）

**課題**: 段落・セクション・プレビュー間移動はボタンクリックが必要。キーボード派には負担。

**提案**:
- `Alt+↑/↓` = 段落ナビ前後
- `Alt+Shift+↑/↓` = セクションナビ前後
- `Alt+→/←` = プレビュー次/前マッチ
- TextBox 既定動作と衝突しない Alt 系で実装、`OnKeyDown` で吸収

---

### 検討中 — 設計が必要

#### #126 読み上げキュー（簡易版）

**前提**: #119 の進捗 API（特に「読み上げ完了」イベント）が前提。#119 完了後に再評価。

**提案案**:
- セクション一覧から複数選択 → 「キュー追加」
- キューパネル（ListBox）+「順次再生」ボタン
- 各エンジンの `SpeakAsync` 完了をシリアル待機して次を再生

#### #127 辞書エントリのカテゴリ/タグ

**課題**: 大規模辞書（地名・人名・専門用語等が混在）でカテゴリごとに管理したい。

**提案**:
- `DictionaryEntry` に `Category: string` を追加（任意・空欄可）
- 辞書一覧にカテゴリ列追加・カテゴリ別フィルター
- スキーマ変更を伴うため後方互換性に注意

#### #128 音声出力デバイス選択

**課題**: スピーカーとイヤホンを切替えながら作業する場面でデバイス選択 UI がない。

**懸念**:
- WinRT の `SpeechSynthesizer` はデバイス指定 API が限定的
- OpenJTalk はファイル経由のため `MediaElement.SetOutputDevice` 相当が必要
- 実装難度に対する効果が不明（OS の音量ミキサーで代替可能）

---

## v0.7.8 実装済み提案（#116・#117・#118 + 一括保存サマリ強化）

### #116 セクション再生 ✅ v0.7.8

**課題**: セクションジャンプ後に手動で F5 を押す必要があり、1アクション余計。

**v0.7.8 実装**:
- `BtnSectionPlay` ボタンを `PnlSectionNav` 内に追加（ドロップダウンの右隣）
- `CmbSection_SelectionChanged` でセクション選択時に有効化
- `BtnSectionPlay_Click`: 選択セクションの CharOffset〜次セクション（またはテキスト末尾）を `TxtInput.Select()` で選択し `StartSpeech()` を呼び出す

### #117 辞書一括操作 Undo（1段） ✅ v0.7.8

**課題**: 一括削除・優先度一括変更の誤操作を即座に戻せない。

**v0.7.8 実装**:
- `_dictUndoSnapshot: List<DictionaryEntry>?` フィールドを追加
- `SaveDictUndoSnapshot()`: 操作確定直前にエントリを全 `.Clone()` して保存、`BtnDictUndo.IsEnabled = true`
- `ClearDictUndoSnapshot()`: 個別追加/編集/インライン編集/CSV インポート後に呼び出し
- `BtnDictUndo_Click`: `_dictService.ReplaceAll(snapshot)` → 再保存・再描画
- スナップショット取得タイミング: 確認ダイアログ通過後、実際の削除/変更前

### #118 運用パック入出力 ✅ v0.7.8

**課題**: 閉域運用で複数端末に設定を展開する際、ファイルを個別にコピーする必要があった。

**v0.7.8 実装**:
- `OperationalPackService` を新規作成（`System.IO.Compression.ZipFile` 使用、NuGet 追加不要）
  - `Export()`: dictionary.json / templates.json / settings.json（機微データ除去）/ text_rules.json（存在時）を ZIP化
  - `Import()`: ZIP を展開して各ファイルを上書きコピー
  - `ListContents()`: インポート前のプレビュー用（既知ファイルのみ列挙）
- ファイルメニューに「運用パック エクスポート」「インポート」を追加
- インポート後: `LoadDictionary()` + `LoadSettings()` を自動実行

### 一括保存サマリ強化 ✅ v0.7.8

**課題**: 一括保存完了の MessageBox からパスをコピーできない。

**v0.7.8 実装**:
- `BatchSaveResultDialog`（新規）: 読み取り専用 TextBox にパス一覧、「パスをコピー」ボタンで `Clipboard.SetText()`
- エラー情報も同一ダイアログに表示

---

## v0.7.8 見送り判断記録（2026-04-29）

| 提案 | 判断 | 理由 |
|---|---|---|
| 辞書影響プレビューの詳細化（件数→該当語句/行） | **見送り** | 確認ダイアログが肥大化するリスク。現在の件数表示（#115）で判断材料として十分。詳細表示が必要なら専用「影響確認」ダイアログを別途設計すべき。 |

---

## v0.7.7 実装済み提案（#113・#114・#115）

### #113 セクションナビ（ジャンプ） ✅ v0.7.7

**課題**: 段落ナビは行単位で細かすぎ、長文広報で「章」レベルの移動ができない。

**v0.7.7 実装**:
- `SectionHeadPattern` 正規表現（`■|◆|●|▶|第\d+[章節部]|【[^】]+】` で始まる行）でセクション行を検出
- `UpdateSections()` を `TxtInput_TextChanged` から毎回呼び出し
- `CmbSection` ComboBox に見出し一覧を表示、選択時に該当位置へジャンプ
- 見出しが0件なら `PnlSectionNav` は `Visibility.Collapsed`（画面を占有しない）
- 「このセクションのみ再生」は v0.7.8 以降

### #114 音声一括保存 ✅ v0.7.7

**課題**: 配布先ごとに異なる形式が必要な場合、毎回手動で保存し直す必要がある。

**v0.7.7 実装**:
- `AppSettings.BatchSaveFormats: List<string>` を追加（デフォルト `["mp3"]`）
- SettingsDialog「一括保存形式」に MP3/WAV/MP4 チェックボックスを追加
- `BtnBatchSave` ボタン（「音声保存」の右隣）を追加
- `BatchSaveAudio()`: ファイル名ベース（拡張子なし）を1回指定、形式ごとに SaveToFileAsync を順次実行
  - 各形式の進捗は SaveProgressDialog で表示
  - 完了後に保存ファイル一覧をメッセージボックスで通知

### #115 辞書削除・優先度変更の影響プレビュー ✅ v0.7.7

**課題**: 誤って重要なエントリを削除・変更した場合に原稿への影響が分からない。

**v0.7.7 実装**:
- `DictionaryService.CountOccurrences(text, entries)` を追加（単純文字列検索でヒット件数を合算）
- `BtnDeleteEntry_Click`: 削除確認メッセージに「現在の原稿で X 件マッチ」を追記
- `BtnBatchPriority_Click`: 優先度入力後の変更確認ダイアログに同様のヒット件数を表示

---

## v0.7.7 見送り判断記録（2026-04-29）

| 提案 | 判断 | 理由 |
|---|---|---|
| 1) 読み上げキュー（複数範囲を連続再生） | **見送り** | 各エンジン（SAPI/WinRT/OpenJTalk）の「再生完了」通知の統一が必要。SpeechService の現設計は `SaveToFileAsync` は awaitable だが `SpeakAsync` は fire-and-forget。キュー制御を安全に実装するには SpeechService の設計変更が必要。 |
| 4) テンプレート変数入力補助 | **見送り** | 「前回値候補」は履歴ストレージ設計が必要、「予約変数ボタン」は PlaceholderDialog の小改良で済むが `{date}` 等は FileNameBuilder 側の概念と混在する。設計を整理してから v0.7.8 でまとめる。 |
| ショートカット拡張 | **見送り** | TextBox フォーカス中の `Ctrl+↑↓` 干渉リスクが前回評価から変化なし。 |

---

## v0.7.6 実装済み提案（#111・#112）

### #111 辞書一括操作 ✅ v0.7.6

**課題**: `SelectionMode="Single"` のため大量整備時は1件ずつ操作が必要で手数が多い。

**v0.7.6 実装**:
- `DgDictionary` を `SelectionMode="Extended"` に変更（Shift/Ctrl クリックで複数選択）
- `DgDictionary_SelectionChanged` を追加し、選択数に応じてボタン有効/無効を制御
  - 「編集」「↑」「↓」: 単一選択のみ有効
  - 「削除」「優先度変更」: 1件以上で有効
- `BtnDeleteEntry_Click` を複数選択一括削除に対応（後ろインデックスから順に削除してインデックスずれ防止）
- `BtnBatchPriority_Click` を新規追加: `InputDialog` で新優先度を入力し、全選択エントリに適用
  - `_dictService.Invalidate()` を呼んでから保存・再描画

### #112 テンプレートプレースホルダ ✅ v0.7.6

**課題**: テンプレート挿入後に担当者名・日付等を手動で書き直す必要があり、定型文再利用の手間が残っていた。

**v0.7.6 実装**:
- `PlaceholderDialog`（新規）: `{変数名}` ごとに TextBox を並べ、`Apply(template)` で一括置換して返す
- `BtnInsertTemplate_Click` に `ExtractPlaceholders()` を追加し `{name}` 形式を出現順・重複なしで検出
  - プレースホルダがあれば `PlaceholderDialog` を表示して値を取得してから挿入
  - プレースホルダがなければ従来通り即時挿入（後方互換）

---

## v0.7.6 見送り判断記録（2026-04-29）

ユーザーが提案した5案のうち、以下は実装せずに見送った。

| 提案 | 判断 | 理由 |
|---|---|---|
| 1) 原稿構造化ナビ（見出し/章単位ナビ） | **見送り** | 見出し定義が曖昧（`##` / `【】` / 行頭記号のどれを使う？）。左ペイン追加は大規模レイアウト変更で WPF 設計上リスクが高い。v0.7.3 の段落ナビで基本ニーズはカバー済み。章ナビの価値は長文広報に限られ、一般的な短文原稿では不要。 |
| 4) 読み上げキュー（複数範囲を連続再生） | **見送り** | `SpeechService` へのキュー管理機構の追加が大きなアーキテクチャ変更を要する。現行の「選択範囲 + F5」ワークフローで大半のユースケースをカバーできる。既存の一時停止/再開/停止制御との整合も複雑。 |
| 5) 保存の"まとめ出力"（MP3+WAV 同時・セクション別連番） | **見送り** | セクション検出が未実装のためセクション別連番は単独では価値が低い。MP3+WAV 同時出力は保存 UI の設計が複雑になる割に閉域運用以外での需要が限定的。命名テンプレート (#110) との組み合わせも将来課題として残す。 |
| ショートカット追加拡張（段落ナビ移動・プレビュー次/前にキー割当） | **見送り** | `TextBox` にフォーカスがある状態での `Ctrl+↓/↑` は TextBox デフォルト動作と干渉するリスクがある。◀▶ ボタンと既存 `Ctrl+Shift+↑↓`（辞書移動）で代替可能。 |

---

## v0.6.2 実装済み提案（#77・#78・#79・#80）

### 76. 監査ログの保持期間/自動削除ポリシー ✅ v0.6.3

**課題**: v0.6.1 で月次ローテーションは入ったが、古いファイルが蓄積し続ける。年単位運用では手動削除が必要。

**v0.6.3 実装**:
- `AppSettings.AuditRetentionMonths`（デフォルト: 13、0 = 無制限）を追加
- `AuditLogger.PurgeOldLogs(int retentionMonths)` — 保持期間を超えた `audit_YYYYMM.csv` を自動削除
  - 内部実装 `PurgeOldLogsFrom(retentionMonths, dataDirectory)` でテスト可能に分離
- 設定画面「監査ログ保持期間」GroupBox を追加（ComboBox: 無制限/3/6/13/24か月）
- 起動時（`LoadSettings()` 直後）と設定変更時の両タイミングでパージを実行
- テスト 4 件追加（削除対象/無制限/期間内保持/ディレクトリ不在）

### 77. 読みルール保存時・非Idle 通知 ✅ v0.6.2

**課題**: 再生中に読みルールを保存しても何も通知されず、ユーザーが「保存されたか」「いつ反映されるか」を判断できない。

**v0.6.2 実装**:
- Idle 時: `SetStatus("読みルールを保存しました。")` を追加
- 非Idle 時: `SetStatus("読みルールを保存しました。次回エンジン起動時に反映されます。")` を追加

### 78. 容量ガードを「インポート後総量」で判定 ✅ v0.6.2

**課題**: #73 のガードは `imported` 単体のみ評価。既存辞書が巨大な状態で少量追加するケースを見逃す。

**v0.6.2 実装**:
- 既存辞書の Display 文字数を加算した `afterDisplayChars` / `afterCount` を計算
- 追加判定: `afterCount > 2,000` または `afterDisplayChars > 20,000`
- メッセージをトリガー種別（インポート単体 vs 総量超過）に応じて出し分け

### 79. 読みルール画面に無効ルール診断表示 ✅ v0.6.2

**課題**: プレビューで無効 Regex / タイムアウトを silently skip しており、利用者が問題に気づきづらい。

**v0.6.2 実装**:
- `TxtDiagnostic` TextBlock（赤字）を変換結果欄の下に追加（通常は非表示）
- プレビュー評価中に例外が発生したパターンを収集し、件数と先頭パターンを表示
- 入力欄が空になると診断ラベルを隠す

### 80. PathConfig/Audit テスト拡張 ✅ v0.6.2

**課題**: `EffectiveTextRulesPath` / `UserTextRulesPath` の優先ロジックと AuditLogger の CSV 解析に脆弱性があった。

**v0.6.2 実装**:
- `PathConfigTests` に 5 件追加: `TextRulesPath`・`UserTextRulesPath`・`EffectiveTextRulesPath` の検証
- `AuditLoggerTests` の `Split(',')` を RFC 4180 準拠の `ParseCsvLine()` に置き換え（カンマ含むフィールド耐性）
- engineType にカンマを含む場合の CSV エスケープ確認テストを追加

---

## v0.6.0 実装済み提案（#71・#72・#73・#75）

### 71. 読みルール保存後の ReplaceEngine 後 UI 再同期 ✅ v0.6.0

**課題**: v0.5.9 で読みルール保存後に `ReplaceEngine` を呼び出すようにしたが、設定変更時と異なり `InitializeVoiceCombo` / `SetRate` / `SetVolume` の再適用がなかった。エンジン再起動後に UI 表示と内部状態（音声リスト・速度・音量）がズレる可能性があった。

**v0.6.0 実装**:
- `MenuTextRules_Click`: `ReplaceEngine` 後に `InitializeVoiceCombo()` / `SetRate` / `SetVolume` を追加（設定変更導線と同等の再同期）

### 72. TextRuleDialog CancellationTokenSource 後始末 ✅ v0.6.0

**課題**: `_previewCts` はキャンセルしていたが Dispose していなかった。ダイアログの開閉を繰り返す運用でリソースリークの可能性があった。また `Task.Run` の fire-and-forget で `OperationCanceledException` が暗黙的に握り潰されていた。

**v0.6.0 実装**:
- `Closed` イベントで `Cancel()` + `Dispose()` を確実に実行
- `TxtTestInput_TextChanged`: 前回の CTS を `Cancel` + `Dispose` してから新しい CTS を生成
- `Task.Run` の本体を `try/catch (OperationCanceledException)` で明示的にキャッチしてキャンセルを正常系として処理

### 73. 容量ガード指標を Reading 文字数にも拡張 ✅ v0.6.0

**課題**: #70 のガードは `Display` 合計文字数のみで、`Reading`（読み仮名）の総文字数はチェックしていなかった。大量の読み文字列はメモリ使用量・置換処理コストに直結する。

**v0.6.0 実装**:
- `totalReadingChars = imported.Sum(en => en.Reading.Length)` を追加
- 判定: `imported.Count > 1000 || totalDisplayChars > 10_000 || totalReadingChars > 10_000`
- 警告メッセージに表記文字数・読み文字数を両方表示

### 74. 監査 CSV ローテーション（月次アーカイブ）✅ v0.6.1

**課題**: `AuditLogger` は追記のみで、サイズ管理・世代管理がなかった。長期運用で `audit.csv` が肥大化する可能性があった。

**v0.6.1 実装**:
- `PathConfig.AuditLogPath` を月次形式に変更: `audit_YYYYMM.csv`（例: `audit_202601.csv`）
- `PathConfig.AuditLogPathForMonth(DateTimeOffset)` を追加（指定月のパスを返す。テスト・アーカイブ参照用）
- `AuditLogger` 側の変更なし（`PathConfig.AuditLogPath` 経由で自動的に月次ファイルへ書き込む）
- 既存の `audit.csv`（v0.5.9 以前）はそのまま残存。新規レコードは月次ファイルに書き込まれる
- テスト 3 件追加: 現在月のパスパターン確認、指定月ファイル名確認、月違いで別ファイルになることの確認

### 75. 読みルール有効ファイルパスをダイアログ上に表示 ✅ v0.6.0

**課題**: フォールバック保存先切替はログ警告のみで UI に表示がなかった。現場での問い合わせ（「どのファイルが使われているのか」）が発生しやすい。

**v0.6.0 実装**:
- `TextRuleDialog.xaml`: DataGrid 下に `x:Name="TxtRulesPathLabel"` TextBlock を追加
- コンストラクタで `TxtRulesPathLabel.Text = $"設定ファイル: {rulesPath}"` をセット（有効パスが一目でわかる）
- 注記文言も「保存後、再生停止中は即時反映。再生中の場合は次回エンジン切替またはアプリ再起動時に反映。」に更新（v0.5.9 の即時反映実装を反映）

---

## v0.5.4 実装済み提案（#60）

### 60. 辞書照合の Aho-Corasick アルゴリズム導入 ✅ v0.5.4

**v0.5.4 実装内容**:
- `TxtToVoice.Core/Services/AhoCorasick.cs` を新規追加（自前実装・外部依存なし）
  - `AhoCorasick.Build(string[] patterns)` — トライ木構築 + 失敗リンク BFS で O(Σ|p|) 構築
  - `AhoCorasick.Search(string text)` — テキストを O(n + 出力数) の 1 パスで走査、全マッチを `(Start, PatternIndex)` で返す
- `DictionaryService` を更新
  - `_acAutomaton` フィールドを追加し `_sortedCache` と一括管理
  - `BuildSortedEntries()` を `EnsureCache()` に統合（初回 `FindReplacements` 時に AC オートマトンも構築）
  - `_sortedCache = null` を全て `InvalidateCache()` に置き換え（`_acAutomaton` も同時クリア）
  - `FindReplacements` を `static` → インスタンスメソッドに変更し AC を使用
  - AC の全候補を「長さ降順 → 優先度降順 → 位置昇順」でソート後、貪欲非重複選択 — 既存の置換結果と完全に等価

**計算量**:
- ビルド: O(Σ|p|)（エントリ変更時のみ再構築）
- 照合: O(n + 出力数) → 従来の O(n × m) から改善

---

## v0.5.3 実装済み提案（#51・#55）

### 51. OpenJTalk 音声品質評価・レポート ✅ v0.5.3（インフラ整備）

**v0.5.3 実装内容（CI インフラ整備）**:
- `TxtToVoice.Tests/Services/VoiceQualityEvalTests.cs` — S1〜S4 固定原稿を WAV 合成する `[VoiceQualityEval]` テストを追加
  - 環境変数 `VOICE_EVAL_OUTPUT_DIR` で出力先を指定可能（未設定時は `%TEMP%\txtvoice-eval`）
  - `[SkippableFact]` + `[Trait("Category", "RequiresEngine")]` + `[Trait("Category", "VoiceQualityEval")]` の 2 カテゴリで管理
- `openjtalk-engine-test.yml` — VoiceQualityEval テスト実行ステップとアーティファクトアップロードを追加
  - RequiresEngine 完了後に `dotnet test --filter "Category=VoiceQualityEval"` を実行（`continue-on-error: true`）
  - S1〜S4 の WAV ファイルをアーティファクト `voice-quality-eval-wavs`（保持 30 日）としてアップロード
- 聴取評価は人間が実施し、結果を `docs/release-checklist.md` の採点表に記入する

**ステータス**: CI インフラ整備済み・聴取評価は各リリース前に実施

### 55. 音声品質評価の定例化 ✅ v0.5.3

**v0.5.3 実装内容**:
- `docs/release-checklist.md` を新規作成
  - ビルド・テスト・バージョン・配布パッケージの必須チェック項目
  - 音声品質評価セクション（WAV 生成手順・評価原稿 S1〜S4・採点表・合否基準）
  - リリース前に全チェックボックスを確認してからタグを打つ運用を明文化

---

## v0.5.1 実装済み提案（#49b）

### 49b. SourceForge Cloudflare 恒久対応（GitHub Release 方針A）✅ v0.5.1

**課題**: MMDAgent 音声モデルのダウンロードが SourceForge の Cloudflare JS チャレンジで失敗する場合があり、`setup_openjtalk.ps1` と CI（openjtalk-engine-test.yml）の信頼性を損なっていた。  
**解決**: CC BY 3.0 は再配布可（クレジット義務あり）のため、`mei_normal.htsvoice` を本プロジェクトの GitHub Release asset として配布し、スクリプト・CI がこれを最優先ダウンロード先とする。

**v0.5.1 実装内容**:
- **`tools/setup_openjtalk.ps1`**:
  - `$githubVoiceUrl = "https://github.com/YanTKYS/txt2voice/releases/latest/download/mei_normal.htsvoice"` 変数を追加
  - voice ダウンロード優先順位を更新:
    1. 同梱 `bundled\mei_normal.htsvoice`（既存）
    2. **GitHub Release 直接ダウンロード**（~25MB・新規）
    3. SourceForge ZIP ミラー 4 候補（~200MB・フォールバック）
  - 失敗時メッセージに GitHub Release の手動ダウンロード手順（手順A）を追加
- **`.github/workflows/release.yml`**:
  - 「音声モデルを取得」ステップを追加（`continue-on-error: true`）
    - 方針1: 前バージョンの release asset を転用（最も確実）
    - 方針2: SourceForge からダウンロード（初回リリース時）
  - release 作成ステップで `mei_normal.htsvoice` を ZIP と並べて添付
- **`.github/workflows/openjtalk-engine-test.yml`**:
  - `mei_normal.htsvoice` 直接キャッシュ（~25MB）を追加（ZIP キャッシュとの 2 段構え）
  - コメントを更新してダウンロード優先順位を明記

---

## v0.5.0 実装済み提案（#56-b, #62, #66）

### 56-b. PlaybackState sealed record 導入 ✅ v0.5.0

**課題**: `MainWindow.xaml.cs` の `_isSpeaking` / `_isPaused` は 2 つのブール値が独立しており、無効な状態（`_isSpeaking=false && _isPaused=true` 等）を型で防げない。  
**提案**: `sealed record PlaybackState` を導入して有効状態を 3 つの静的インスタンス（`Idle` / `Active` / `Paused`）に限定し、参照箇所を 1 フィールドに集約する。  

**v0.5.0 実装内容**:
- `TxtToVoice/PlaybackState.cs` を新規作成
  ```csharp
  internal sealed record PlaybackState(bool IsSpeaking, bool IsPaused)
  {
      internal static readonly PlaybackState Idle   = new(false, false);
      internal static readonly PlaybackState Active = new(true,  false);
      internal static readonly PlaybackState Paused = new(true,  true);
  }
  ```
- `MainWindow.xaml.cs`: `_isSpeaking` / `_isPaused` フィールドを `private PlaybackState _playback = PlaybackState.Idle;` に一本化
- `MainWindow.PlaybackOperations.cs`: 全 16 参照を `_playback.IsSpeaking` / `_playback.IsPaused` / `PlaybackState.Idle` / `.Active` / `.Paused` に置き換え
- 動作変更なし・純リファクタリング

### 62. 行政文書ゴールデンテスト ✅ v0.5.0

**v0.5.0 実装内容**:
- `TxtToVoice.Core.Tests/Golden/TextPreprocessorGoldenTests.cs` を新規作成
- 6 クラス 11 件の xUnit Theory / Fact テスト
  - S1: 月×時刻複合（フェーズ1+4）
  - S2: 記号×パーセント（フェーズ1+2）
  - S3: 電話番号×第X回×Xか月（フェーズ3）
  - S4: コロン時刻（フェーズ5→4 連鎖）
  - S5: 除外ケース（時間・日付）
  - S6: 全フェーズ混合（行政文書総合）

### 66. TextPreprocessor フェーズ5（コロン時刻）✅ v0.5.0

**課題**: `10:30` 形式のコロン区切り時刻はフェーズ4（X時/X分）で変換されず、OpenJTalk が誤読する。  
**提案**: フェーズ5 として `H:MM / HH:MM` → `X時X分` への展開を追加し、フェーズ4 の読み仮名変換に連鎖させる。  

**v0.5.0 実装内容**:
- `ColonTimePattern = new(@"([01]?\d|2[0-3]):([0-5]\d)(?![:\d])", ...)` を追加
  - 時は 0〜23 のみ一致（`[01]?\d|2[0-3]`）
  - 分は 00〜59 のみ一致（`[0-5]\d`）
  - `HH:MM:SS` 形式を `(?![:\d])` で除外
- Phase 5 を Phase 4 の直前で実行（展開後は Phase 4 が読み仮名化）
- xUnit テスト 7 件追加（基本変換・除外ケース）

### 67. TextRule 運用 UI（設定→読みルール 画面）✅

**課題**: `text_rules.json` 外部化は実装済みだが、同梱ルールが全て `enabled: false` で現場での ON/OFF 導線がない。テキストエディタ操作が前提となっており、保守性・誤操作リスクが高い。  

**v0.5.8 実装**:
- ファイルメニューに「読みルール(_L)...」を追加し、`TextRuleDialog` を開く
- `TextRuleDialog`: DataGrid でルール一覧（`Enabled` / `Pattern` / `Replacement` / `Description`）を表示。`Enabled` チェックボックスのみ編集可
- 「保存して閉じる」で `TextRuleLoader.SaveRaw()` により `text_rules.json` に書き出し
- テスト入力欄: 任意テキストを入力して有効なルールを順番に適用した変換結果をリアルタイム表示（500ms タイムアウト付き Regex を使用）
- `TextRuleLoader.LoadRaw()` / `SaveRaw()` を Core に追加し、UI と IO を疎結合に保つ

### 68. TextRuleLoader Regex タイムアウト ✅

**課題**: `new Regex(rule.Pattern, RegexOptions.Compiled)` はタイムアウト未指定のため、ReDoS（指数的バックトラッキング）パターンが設定されるとスレッドが CPU 張り付きになる。  
**提案**: `Regex(pattern, options, TimeSpan)` オーバーロードでマッチタイムアウト（例: 1 秒）を設定し、`RegexMatchTimeoutException` をキャッチして当該ルールをスキップ・ログ出力する。  

**v0.5.7 実装**:
- `TextRuleLoader.Load()`: `new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(1))` に変更
- `CompiledTextRule.Apply()`: `RegexMatchTimeoutException` を try/catch し、元テキストをそのまま返してログ警告
- `TextRuleLoaderTests.cs` に 2 件追加: タイムアウト時の動作確認 + 正常ルールのロード確認

### 69. 性能テスト閾値の再設計（Aho-Corasick 導入後）✅

**課題**: 現行の性能テスト閾値（500件辞書×10,000文字で 30 秒、100件×50,000文字で 45 秒）は Aho-Corasick 導入前の O(n×m) を前提とした値で、回帰検知として機能していない。  

**v0.5.7 実装**:
- 500件 × 10,000文字: 30,000ms → **1,000ms**（AC 実測 < 10ms の ~100 倍余裕）
- 100件 × 50,000文字: 45,000ms → **2,000ms**（AC 実測 < 5ms の ~400 倍余裕）
- CI ランナーのばらつき（Azure VM コンテナ起動等）を考慮した余裕係数をコメントに明記

### 70. 辞書インポート容量ガード ✅ v0.5.9

**課題**: CSV インポートは件数上限・総文字数上限が未実装。極端な大規模辞書投入時に Aho-Corasick 構築メモリ（状態数 ∝ パターン総文字数）と応答性が悪化する可能性がある。  

**v0.5.9 実装**:
- `MainWindow.DictionaryOperations.cs` の `MenuImportCsv_Click` にガードを追加
- 閾値: エントリ数 1,000 件超 または 総表記文字数 10,000 文字超で YesNo 確認ダイアログ
- ユーザーが「いいえ」を選択した場合はインポート中止（「はい」で続行可能）

---

## v0.5.9 実装済み提案（#70 + リファクタリング）

### 読みルール保存先フォールバック ✅ v0.5.9

**課題**: Program Files 等に EXE を配置すると `Data/text_rules.json` が読み取り専用になり、読みルール画面で保存が失敗する。  

**v0.5.9 実装**:
- `PathConfig.UserTextRulesPath` — `DataDirectory` 下の `text_rules.json` フルパス（新規追加）
- `PathConfig.EffectiveTextRulesPath` — ユーザー保存済みファイルを優先し、未保存時は EXE 配下を返す（新規追加）
- `OpenJTalkEngine` / `TextRuleDialog` / `MainWindow.SettingsOperations` でいずれも `EffectiveTextRulesPath` を使用
- `TextRuleDialog.BtnOk_Click`: 最初に `_rulesPath` へ書き込み試行、`UnauthorizedAccessException` または `IOException` が発生した場合は `UserTextRulesPath` へ自動フォールバック

### プレビューデバウンス ✅ v0.5.9

**課題**: `TxtTestInput_TextChanged` が毎キー入力ごとに全ルールに対して Regex.Replace を実行するため、多数のルールがある場合に UI スレッドが詰まる。  

**v0.5.9 実装**:
- `CancellationTokenSource` パターンで 300ms デバウンス
- `Task.Run` でスナップショットをバックグラウンド評価し、完了後 `Dispatcher.InvokeAsync` で UI に反映
- UI スレッドへの負荷ゼロ; キャンセルで古い評価が捨てられる

### 読みルール保存後のエンジン即時再起動 ✅ v0.5.9

**課題**: 読みルール画面で保存しても、エンジン切替またはアプリ再起動まで変更が反映されない（v0.5.8 時点の制約）。  

**v0.5.9 実装**:
- `MenuTextRules_Click`: `dlg.ShowDialog() == true && _playback == PlaybackState.Idle` の場合に `_speechService.ReplaceEngine()` を呼び出し、保存後即時反映
- 再生中の場合はスキップ（再起動時に反映）

### 監査 CSV timestamp タイムゾーンオフセット付与 ✅ v0.5.9

**課題**: `AuditLogger` の timestamp が `DateTime.Now` でローカル時刻のみを記録しており、UTC オフセットが不明なため監査ログの突合が困難。  

**v0.5.9 実装**:
- `DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")` に変更（例: `2026-04-26T14:30:00+09:00`）
- RFC 3339 / ISO 8601 準拠の形式で UTCオフセットを明示

---

## v0.4.9 実装済み提案（#56-a, #65）

### 56-a. MainWindow コードビハインド縮小（段階 1）✅ v0.4.9

**v0.4.9 実装内容**:
- `MainWindow.SettingsOperations.cs` を新規作成（partial class）
  - `LoadSettings()` / `SaveCurrentSettings()` / `BuildAppSettings()` を `PlaybackOperations.cs` から移動
  - `MenuSettings_Click()` を `MainWindow.xaml.cs` から移動
- `UpdateRecentFilesMenu()` / `OpenRecentFile()` を `MainWindow.xaml.cs` から `FileOperations.cs` へ移動
- `MainWindow.xaml.cs` クラスコメント更新（ファイル一覧に SettingsOperations を追加）
- 副作用ゼロの純リファクタリング（動作変更なし）

partial class 構成（v0.4.9 以降）:
```
MainWindow.xaml.cs               — フィールド・コンストラクタ・初期化・共通ユーティリティ・ヘルプ
MainWindow.FileOperations.cs     — ファイル開く・最近使ったファイル・クリア・テキスト入力
MainWindow.SettingsOperations.cs — 設定読み書き・SettingsDialog 呼び出し（新規）
MainWindow.PlaybackOperations.cs — 読み上げ・音声保存・パラメータ操作
MainWindow.DictionaryOperations.cs — 辞書CRUD・プレビュー・CSV入出力
```

### 65. TextPreprocessor フェーズ4（時刻パターン）✅ v0.4.9

**課題**: OpenJTalk の MeCab が X時・X分を誤読するケースがある。行政文書では「10時30分から受付」等の時刻表現が頻出。  
**提案**: TextPreprocessor に時刻パターンを追加し、読み仮名に事前変換する。  

**v0.4.9 実装内容**:
- `HourPattern` / `MinutePattern` 正規表現を追加（`\d{1,2}時(?!間)` / `\d{1,2}分`）
- `HourReadings[24]` — 0〜23時の読み仮名テーブル（4時=よじ・7時=しちじ・9時=くじ等の慣用読み対応）
- `MinuteReadings[60]` — 0〜59分の読み仮名テーブル（促音便 ぷん/ふん を正確に反映）
- 「X時間」（持続時間）との衝突を負の先読み `(?!間)` で除外
- xUnit テスト 12 件追加（時・分・除外ケース・複合パターン）

---

## v0.4.8 実装済み提案（#64, #52 残）

### 64. 辞書一覧リアルタイム絞り込み ✅ v0.4.8

**課題**: 辞書エントリが増えると目的のエントリを探すのに時間がかかる。DataGrid のソートだけでは不十分。  
**提案**: 辞書一覧ヘッダーの下にフィルター入力欄を追加し、表記・読み・備考で部分一致絞り込みを行う。  
**優先度**: 中（エントリ数が 50 件超の環境で体感改善）

**v0.4.8 実装内容**:
- `MainWindow.xaml` の辞書一覧 Grid に行を追加し、`TxtDictFilter` TextBox と「×」クリアボタンを配置
- `CollectionViewSource.GetDefaultView(_entries).Filter` に `FilterDictEntry()` を設定
- フィルター変更時に `view.Refresh()` を呼び出してリアルタイム絞り込みを実現
- カウント表示を「辞書: N 件」→「辞書: X / N 件（フィルター中）」に切り替え
- フィルター条件: 表記・読み・備考の部分一致（大文字小文字区別なし）

### 52. OpenJTalk セットアップのオフライン完結強化（URL 複数候補化）✅ v0.4.8

**v0.4.8 実装内容（URL 複数候補化）**:
- `$mmdUrlCandidates` 配列に SourceForge ミラー 4 候補を定義（jaist / downloads / umnmirror / excellmirror）
- ダウンロード失敗時は次候補へ自動フォールバックし、各試行の失敗理由を表示
- 全候補失敗時は最後のエラー内容を明示して手動手順を案内
- セットアップ完了時に「コンポーネント状態」サマリ（OK/NG）を出力
- `$summaryWarnings` リストで警告を集約し最終サマリに表示

---

## v0.4.4 レビュー提案（#60–#63）

### 60. 辞書照合の Aho-Corasick アルゴリズム導入

**課題**: `DictionaryService.ApplyDictionaryForSpeech()` は現在 O(n×m) の単純スキャン。辞書エントリが増えるにつれて長文処理の遅延が顕在化する可能性がある。  
**提案**: 辞書ビルド時に Aho-Corasick オートマトンを構築し、テキスト照合を O(n+m) に改善。依存ライブラリを導入するか、`TxtToVoice.Core` に自前実装（小規模のため実装コスト低）。  
**優先度**: 低〜中（辞書エントリ数が数百件未満の現状では体感差なし。1,000 件超を想定する場合に検討）

### 61. TextPreprocessor 外部ルール定義（JSON / TSV 化）✅ v0.5.2

**課題**: 変換ルールが C# コードにハードコードされており、現場ルール追加のたびにリリースが必要。  
**提案**: ルールを `Data/text_rules.json`（または `.tsv`）として外部定義し、起動時にロード。辞書と同様に UI からルール一覧を確認・追加できる将来パスも想定。  
**優先度**: 低〜中（現状のルール数が少ない間は不要。ルールが 20 件超える前に設計着手を推奨）

**v0.5.2 実装内容**:
- `TxtToVoice.Core/Models/TextRule.cs` — JSON DTO（`pattern` / `replacement` / `description` / `enabled`）
- `TxtToVoice.Core/Services/TextRuleLoader.cs` — `TextRuleLoader.Load(filePath)` で JSON 読み込みとコンパイル
  - `CompiledTextRule` クラスで `Regex` + 置換文字列を保持し `Apply(text)` を提供
  - ファイル不在・JSON エラー・無効な正規表現はすべてスキップしてログ記録、処理を継続
- `TextPreprocessor.Apply(text, rules?)` — `rules` パラメータを追加（省略可・null で既存動作を維持）
  - フェーズ6として既存フェーズ1〜5 の後に外部ルールを適用
- `PathConfig.TextRulesPath` — EXE 配置ディレクトリの `Data/text_rules.json` を返すプロパティを追加
- `TxtToVoice/Data/text_rules.json` — サンプルルール（CPU / AI / SNS / PDF / URL / QR の略語展開を `enabled: false` で同梱）
- `OpenJTalkEngine` — コンストラクタで `TextRuleLoader.Load(PathConfig.TextRulesPath)` を呼び `_textRules` に保持、`SpeakAsync` / `SaveToFile` の両呼び出し元に渡す
- `TxtToVoice.Core.Tests/Services/TextRuleLoaderTests.cs` — 11 件のユニットテスト（不在/空配列/有効ルール/disabled フィルタ/不正 JSON/無効正規表現/空パターン/Apply パラメータ）

### 62. 運用品質テストセット（ゴールデンファイル）の整備

**課題**: 現在のテストは単語単位のゴールデンサンプル。実運用で使われる「1〜2 段落の行政文書」レベルの読み品質を自動検証できていない。  
**提案**: `tests/golden/` に代表的な行政文書スニペット（個人情報を除いた架空テキスト）を用意し、TextPreprocessor + 辞書適用後の出力を期待値ファイルと比較するテストを追加。  
**優先度**: 中（v0.5.x で #51 音声品質評価定例化と合わせて整備）

### 63. ログの匿名化ハードニング ✅ v0.4.6

**課題**: `Logger.Info()` / `Logger.Error()` が本文テキストをそのまま記録するケースがあり、ユーザー入力（原稿テキスト）がログに残る可能性がある。  
**提案**: `Logger.SuppressInfo` フラグ（既存）に加え、テキスト内容を記録しないよう API を見直す。保存パス・エンジン種別・文字数などの「メタ情報のみ」をログ対象とする方針を明文化。  
**優先度**: 中（個人情報保護規程が厳しい自治体向け配布前に対応を推奨）

**v0.4.6 実装内容**:
- `Logger` クラスに `MaxMessageLength = 500` 定数を追加し、超過分を切り捨て＋`…[省略]` 付加
- Logger の XML コメントにログ匿名化ポリシーを明文化（「原稿テキスト本文は記録しない」「メタ情報のみ対象」）
- 既存の呼び出しサイトを確認し、全 Logger 呼び出しがメタ情報のみを対象としていることを検証済み

---

## v0.4.3 レビュー提案（docs/v0.4.3-review-proposals.md より転記）

### 52. OpenJTalk セットアップのオフライン完結強化（A-1）

**課題**: `setup_openjtalk.ps1` は SourceForge Cloudflare 失敗を想定済みだが、閉域配布で「手順の分岐」が障害点になる。  
**提案**:
- `mei_normal.htsvoice` を配布アセットに同梱し、スクリプトは同梱ファイルを優先 ✅ v0.4.7
- 取得元 URL を複数候補化し、失敗理由を最終サマリに明示（未着手）
- `--verify-only` モードで事前確認できる機能を実装 ✅ v0.4.5

**v0.4.5 実装内容**:
- `setup_openjtalk.ps1 -VerifyOnly` スイッチ: jtalk.dll / MeCab 辞書 / 音声モデルの存在を [OK]/[NG] で確認し終了（exit 0 = 全 OK、exit 1 = 不足あり）
- `OpenJTalkDiagnostics` レコード（`DllPresent` / `DictionaryPresent` / `VoicePresent` + `FormatChecklist()`）を `OpenJTalkEngine` に追加
- `SpeechService.GetOpenJTalkDiagnostics()` 経由で UI 層から診断結果を取得可能に
- `MainWindow` の初期化エラーダイアログで OpenJTalk 選択時はチェックリスト形式を表示（`BuildEngineErrorDetail()`）

**v0.4.7 実装内容（同梱ファイル優先）**:
- `setup_openjtalk.ps1` に同梱ファイル優先チェックを追加: スクリプトと同じフォルダの `bundled\mei_normal.htsvoice` が存在する場合はダウンロードをスキップしてそのままコピー配置

### 53. SystemSpeechEngine 長文 MP3/MP4 のメモリ最適化（A-3）✅ v0.4.4

**課題**: `SaveEncoded()` が `MemoryStream` に WAV 全体を保持してからエンコードするため、長文でメモリピークが上がる。  
**対応**: 一時 WAV ファイル経由に変更し `WaveFileReader` でストリーミングエンコード。後始末（中間ファイル削除）を finally で保証。

### 54. 例外ログへのエラーコード付与（A-4）✅ v0.4.6

**提案**: 主要例外に短いエラーコード（例: `TTV-E-SETUP-001`）を付与し、ダイアログ表示にも同コードを出す。  
問い合わせテンプレートにコード記載欄を追加し、一次切り分けを高速化。

**v0.4.6 実装内容**:
- `TxtToVoice.Core/Services/TtvErrorCode.cs` を新規作成（`TTV-E-OJT-001`〜`005` / `TTV-E-SAPI-001` / `TTV-E-WRT-001` / `TTV-E-SAVE-001`）
- `OpenJTalkEngine` の Fail() / SpeakCore() / SaveToFile() の各エラー文にコードを付与
- `SystemSpeechEngine` / `WinRtSpeechEngine` の初期化失敗 Logger.Error にコードを付与
- `MainWindow.PlaybackOperations` の保存失敗ダイアログ・Logger・ステータスに `TTV-E-SAVE-001` を付与

### 55. 音声品質評価の定例化（B-1）

評価セット S1〜S4・採点表は `#51` テンプレートとして固定済み。v0.5.x でリリース前チェックリストに組み込む。

### 56. MainWindow コードビハインド縮小（B-2）

設定保存・復元と再生制御状態を ViewModel/サービスへ段階分離。回帰テストを追加しながら移行。

### 57. 辞書インポートバリデーション強化（B-3）✅ v0.4.7

CSV インポート時に「重複語・極端な優先順位・空読み」を検知しレポート表示。「成功/警告/失敗」分類で提示。

**v0.4.7 実装内容**:
- `CsvImportReport` sealed class を新規作成（`ValidEntries` / `SkippedEmptyDisplay` / `SkippedEmptyReading` / `PriorityClampedCount` / `HasIssues` / `FormatIssues()`）
- `CsvService.ImportWithReport()` を追加: 空表記・空読みのスキップ件数と優先順位補正（1〜100 クランプ）件数を集計し `CsvImportReport` として返す
- `MainWindow.DictionaryOperations.MenuImportCsv_Click` を改修: `ImportWithReport()` を使用し、問題がある場合は確認ダイアログに検証サマリを表示
- xUnit テスト 6 件を追加（正常系 / 空表記スキップ / 空読みスキップ / 優先順位補正 / 混在 / `FormatIssues` 非空文字列確認）

### 58. 設定反映タイミングの改善（C-1）✅

エンジン切替は再起動反映のまま維持しつつ、「エンジン再初期化」による即時反映を将来 PoC で確認。  
リスク（リソース解放漏れ・ハンドル競合）の事前確認が前提。

**v0.5.5 実装（安全条件つき即時反映）**:
- `SpeechService._engine` から `readonly` を除去し、`ReplaceEngine(ISpeechEngine)` メソッドを追加
- コンストラクタの匿名ラムダを名前付きハンドラ（`OnEngineStarted` 等）に変換し、`AttachEngine` / `DetachEngine` ヘルパーでハンドラの着脱を管理
- `ReplaceEngine`: 旧エンジンをデタッチ → Stop → 新エンジンをアタッチ → 旧エンジンを Dispose の順で実行
- `MainWindow.SettingsOperations.cs` の `MenuSettings_Click` に「`_speechEngineType` 変化かつ `_playback == PlaybackState.Idle`」ガードを追加し、条件を満たす場合のみ即時切替 + `InitializeVoiceCombo()` + `SetRate` / `SetVolume` 再適用

### 59. 運用監査向けエクスポート（C-2）✅

「いつ・どのファイルを・どのエンジンで・どの形式に保存したか」の最小監査ログを CSV 出力可能に。  
個人情報は保持せず（ファイル名ハッシュ化・件数ベース統計採用）。

**v0.5.6 実装（監査 CSV 最小セット）**:
- `TxtToVoice.Core/Services/AuditLogger.cs` を新規追加（`Record(engineType, format, success, errorCode?, outputPath?)`）
- `PathConfig.AuditLogPath` を追加し `%LOCALAPPDATA%\TxtToVoice\audit.csv` に追記（ポータブルモード時は EXE フォルダ配下）
- `MainWindow.PlaybackOperations.cs` の `SaveAudio()` で成功時・失敗時に `AuditLogger.Record()` を呼び出す（キャンセルは記録しない）
- 個人情報ゼロ方針: ファイル名のみ SHA-256 ハッシュ（先頭 8 文字小文字）として記録、実パスは保持しない
- `AuditLoggerTests.cs` で 5 件のテストを追加（ヘッダー、errorCode、複数行、outputPath なし、timestamp 形式）

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

### 44. README のテスト手順・ソース構成図を v0.3.6 対応に更新 ✅

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

### 45. CI 2 レーン化（Core.Tests 必須 / Windows 依存テスト任意） ✅

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

### 33. OSS 日本語 TTS エンジン同梱（OpenJTalk 同梱）【v0.4.0 系 計画フェーズ】

**前提**: 項目 #31（ISpeechEngine 抽象化）が完了していること。✅

**概要**  
OSS の日本語 TTS エンジン OpenJTalk をアプリに同梱しローカル実行する。
既存の SAPI / WinRT と並ぶ第 3 の音声エンジン選択肢として追加する。

---

**統合方式の検討結果**

| 方式 | 概要 | 採否 |
|---|---|---|
| **jtalkDLL（C++/CLI）** | rosmarinus 製 .NET 向け管理 DLL。Windows x64 ビルド済み | **採用** |
| open_jtalk.exe プロセス起動 | `Process.Start` で WAV 生成 | 不採用（プロセスごとに辞書再読み込みで低速） |
| P/Invoke（ネイティブ DLL 直接） | 最速だが C API ラッパーを自前で実装する必要あり | 将来検討 |

jtalkDLL は .NET 向けに設計された C++/CLI 管理 DLL であり、P/Invoke を自前で書かずに利用できる。最初の PoC に適している。

---

**同梱ファイル構成（概算）**

```
Data/openjtalk/
├── open_jtalk_dic_utf_8/   MeCab UTF-8 辞書      約 20 MB
├── mei/                    MMDAgent Mei 音声モデル 約  7 MB
└── jtalk.dll               jtalkDLL               約  1 MB
                                              合計 約 28 MB
```

現行スタンドアロン EXE の配布サイズへの影響は許容範囲と判断済み。

---

**ライセンス確認結果** ✅ 確認済み（内部配布・再配布とも可）

| コンポーネント | ライセンス | 再配布 | 主な条件 |
|---|---|---|---|
| jtalkDLL オリジナル部分 | MIT License | ○ | 著作権表示の保持 |
| Open JTalk 本体 | Modified BSD License | ○ | 著作権表示の保持 |
| MeCab + ipadic 辞書 | Modified BSD License | ○ | 著作権表示の保持 |
| hts_engine API | Modified BSD License | ○ | 著作権表示の保持 |
| **HTS Voice "Mei"（MMDAgent）** | **CC BY 3.0** | **○** | **クレジット表示必須**（著作者名・ライセンス名・URL） |
| PortAudio | MIT/PortAudio License | ○ | 著作権表示の保持 |

> 参照: https://www.mmdagent.jp/ / https://open-jtalk.sourceforge.net/

**CC BY 3.0 クレジット表示要件**（Mei ボイス）  
アプリの About ダイアログまたは同梱ライセンスファイルに以下を含める必要がある。

```
HTS Voice "Mei" — Copyright (C) Nagoya Institute of Technology
Licensed under Creative Commons Attribution 3.0 (CC BY 3.0)
https://www.mmdagent.jp/
```

jtalkDLL は「オリジナル部分 MIT ＋ 各同梱物はそれぞれのライセンス」という構成のため、
上記 6 コンポーネントすべての著作権表示を THIRD_PARTY_LICENSES.txt 等にまとめて同梱する。

---

**既存 DictionaryService との連携**

既存の読み替え辞書（`DictionaryService`）と OpenJTalk の MeCab 辞書は役割が異なり、両立できる。

```
入力テキスト
   ↓ DictionaryService.ApplyDictionary()  ← 既存の読み替え（例: 市長→しちょう）
   ↓ OpenJTalkEngine.SpeakAsync()         ← MeCab 形態素解析 → HTS 音声合成
```

既存辞書はテキスト段階の前処理として引き続き有効。OpenJTalk の MeCab 辞書とは独立。

---

**メリット**

- 完全オフライン・端末依存なし（SAPI / WinRT の音声パック不要）
- 日本語に特化した形態素解析による自然な読み上げ
- 読みルール・辞書カスタマイズの自由度が高い

**デメリット・懸念点**

- 同梱サイズ +28 MB（許容済み）
- 辞書 + 音声モデルの初期化に 1〜5 秒かかる可能性 → 起動時バックグラウンド初期化が必要
- jtalkDLL のメンテ状況・将来互換性は継続監視が必要
- SSML ポーズ（既存機能）との統合方法は実装時に検討

**実装フェーズ分割**

| フェーズ | バックログ # | 内容 |
|---|---|---|
| v0.4.0 | [#46] | jtalkDLL で WAV 生成・初期化時間実測・品質比較（ライセンス確認済み） |
| v0.4.1 | [#47] | `OpenJTalkEngine : ISpeechEngine` 実装・UI 統合・クレジット表示追加 |

---

### 46. OpenJTalk 同梱 PoC — 技術検証フェーズ（v0.4.0） ✅

**目的**  
`OpenJTalkEngine` を実装する前に、jtalkDLL + Mei モデルでテキスト → WAV が生成できることを最小コードで確認し、性能・品質を実測して #47 実装の前提を満たす。

> ライセンス確認は完了済み（#33 参照）。技術検証に集中する。

**検証項目**

1. **技術検証（最小コード）**
   - jtalkDLL を参照したコンソールプロジェクトでテキスト → WAV 生成
   - 辞書 + モデルの初期化時間を計測（目標: 5 秒以内）
   - 読み上げ品質を SAPI / WinRT と聴き比べ
2. **サイズ確認**
   - 辞書 + モデル + DLL の実際のバイト数を記録し #33 概算と照合
3. **PoC 結論の記録**
   - 合否を improvement-proposals.md に追記し、#47 着手可否を判断

**合否基準**

| 項目 | 基準 |
|---|---|
| WAV 生成 | テキスト入力 → WAV ファイル出力が動作すること |
| 初期化時間 | 5 秒以内（起動時バックグラウンド初期化で許容可） |
| 品質 | SAPI より自然と判断できること（定性評価） |

**関連ファイル**

- `poc/OpenJTalkPoC/` — PoC 用コンソールプロジェクト（メインプロジェクトには含めない）

---

### 47. OpenJTalkEngine 実装・UI 統合（v0.4.1）

**前提**: #46 の PoC 検証が合格していること。

**実装内容**

1. `TxtToVoice/Services/OpenJTalkEngine.cs` — `ISpeechEngine` 実装
   - jtalkDLL をラップ
   - 起動時バックグラウンド初期化（辞書 + モデル読み込み）
   - `SpeakAsync` / `SpeakSsmlAsync` / `SaveToFile` を実装
   - `SpeakProgress` イベント（単語境界）の対応可否を検討
2. `TxtToVoice.Core/Services/SpeechEngineTypes.cs` — `OpenJTalk` 定数を追加
3. `TxtToVoice/Services/SpeechEngineFactory.cs` — `OpenJTalk` ケースを追加
4. `TxtToVoice/Dialogs/SettingsDialog.xaml` — エンジン選択肢に「OpenJTalk」を追加
5. `TxtToVoice/TxtToVoice.csproj` — `Data/openjtalk/` をコンテンツとして同梱
6. `TxtToVoice.Tests/Services/` — `OpenJTalkEngine` の基本テスト追加
7. **サードパーティライセンス表示**（CC BY 3.0 義務対応）
   - `THIRD_PARTY_LICENSES.txt` を同梱ファイルに追加
   - jtalkDLL / Open JTalk / MeCab / hts_engine / **HTS Voice "Mei"（CC BY 3.0）** / PortAudio の著作権表示を記載
   - About ダイアログに「サードパーティライセンス」リンクまたは表示欄を追加

**必須クレジット表示文（HTS Voice "Mei"）**

```
HTS Voice "Mei" — Copyright (C) Nagoya Institute of Technology
Licensed under Creative Commons Attribution 3.0 (CC BY 3.0)
https://www.mmdagent.jp/
```

**関連ファイル**

- `TxtToVoice/Services/OpenJTalkEngine.cs` — 新規追加
- `TxtToVoice.Core/Services/SpeechEngineTypes.cs` — 定数追加
- `TxtToVoice/Services/SpeechEngineFactory.cs` — ケース追加
- `TxtToVoice/Dialogs/SettingsDialog.xaml / .xaml.cs` — UI 追加
- `TxtToVoice/TxtToVoice.csproj` — 同梱設定追加
- `THIRD_PARTY_LICENSES.txt` — 新規追加（CC BY 3.0 等の著作権表示）

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
| v0.3.7 | WinRT 一時ファイル残存バグ修正（#42）・WAV 保存の異ドライブ対応（#43）・README テスト手順/構成図更新（#44）・CI 2 レーン化（#45） |
| v0.4.0 | OpenJTalk 同梱 PoC プロジェクト追加（#46） |

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

## v0.4.0 レビュー査読結果（実装時の判断記録）

| 項目 | 対応内容 |
|---|---|
| #46 OpenJTalk PoC プロジェクト追加 | `poc/OpenJTalkPoC/` を新設。`NativeJTalk.cs`（P/Invoke ラッパー）・`Program.cs`（PoC ドライバ）・`SETUP.md`（セットアップ手順）を作成。jtalkDLL の U16 変種関数（`openjtalk_initializeU16` / `openjtalk_speakToFileU16`）を `CharSet.Unicode + CallingConvention.StdCall` でバインドし、初期化時間・WAV 生成・データサイズを計測する検証プログラムを実装。バイナリアセット（辞書・音声モデル・DLL）は `.gitignore` で除外 |

---

## v0.3.7 レビュー査読結果（実装時の判断記録）

| 項目 | 対応内容 |
|---|---|
| #42 WinRT 一時ファイル残存バグ | `Path.GetTempFileName()` → `Path.GetRandomFileName()` に変更。`GetTempFileName()` はファイルを実際に生成するため音声保存ごとに `%TEMP%` に空の `.tmp` ファイルが蓄積する問題を修正 |
| #43 WAV 保存の異ドライブ対応 | `File.Move` → `File.Copy` に変更。`File.Move` は異なるボリューム間で `IOException` を投げるため、ネットワーク共有や別ドライブへの保存で失敗していた問題を修正。一時ファイルの削除は `finally` ブロックに委ねる設計のため `tempWavPath = string.Empty` フラグは不要になり削除 |
| #44 README テスト手順・構成図更新 | テスト節を「Core.Tests（OS 非依存）」と「TxtToVoice.Tests（Windows 依存）」の 2 段構えで記述。ソース構成図に `TxtToVoice.Core`（net8.0）・`TxtToVoice.Core.Tests`・更新後の `TxtToVoice.Tests` を追記 |
| #45 CI 2 レーン化 | `build.yml` に `test-core` ジョブ（ubuntu-latest / net8.0）を追加。既存 `build` ジョブは `--filter "Category!=RequiresEngine"` を追加して音声エンジン不在の CI でも安定実行できるよう修正 |

---

## #51 OpenJTalk 音声品質評価テンプレート

**目的**  
OpenJTalk / SAPI / WinRT の読み上げ品質を客観的に比較し、推奨エンジン・推奨シナリオの意思決定材料とする。

### 評価セット（固定原稿）

| ID | 原稿 | 評価ポイント |
|----|------|-------------|
| S1 | 今月3月の広報紙をお届けします。消費税は10%です。 | 月・%読み（TextPreprocessor対象） |
| S2 | 〒100-0001東京都千代田区、気温は25℃、面積50㎡。 | 記号読み |
| S3 | 令和7年度の予算案について市民の皆様にご説明いたします。 | 長文・接続詞の流暢さ |
| S4 | ご不明な点は、0120-XXX-YYYにお電話ください。 | 電話番号読み |

### 評価軸・採点表

| 評価軸 | 尺度 | SAPI | WinRT | OpenJTalk |
|--------|------|------|-------|-----------|
| 自然さ（イントネーション） | 1〜5 | — | — | — |
| 明瞭度（聴き取りやすさ） | 1〜5 | — | — | — |
| 数値・記号の読み正確性 | 1〜5 | — | — | — |
| 読み上げ速度（適切さ） | 1〜5 | — | — | — |
| セットアップ難易度 | 1〜5（高=簡単） | — | — | — |

### 実施手順

1. 固定原稿 S1〜S4 を各エンジンで WAV 保存（SetRate=0, SetVolume=100 統一）
2. 聴取評価（複数名推奨）を上記採点表に記入
3. 結果を `docs/improvement-proposals.md` 末尾に追記し、推奨シナリオを決定

**ステータス**: 未実施（テンプレート固定済み → v0.5.x で実施予定）

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
