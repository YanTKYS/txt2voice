# 読み上げ品質改善 検討メモ

読み上げ品質の改善策を段階別に整理するドキュメント。

- **短期（現行エンジン内）**: System.Speech.Synthesis の範囲で対応できる改善（SSML・前処理等）
- **中長期（エンジン移行）**: より高品質な音声エンジンへの移行戦略（WinRT / OSS TTS）

外部 API・インターネット接続は使用しない（閉域 Windows 環境を前提）。

---

## 現状の限界

Windows SAPI の日本語音声（Haruka 等）はニューラル TTS ではないため、
抑揚・感情表現は機械的になる。外部 API なしでの音質向上には天井がある。

---

## 改善候補

### 1. 句読点ポーズ自動挿入（効果: 高 / 難易度: 低）

現状は句読点をそのまま渡しているため、息継ぎなしで読み続ける。
SSML の `<break>` タグを自動付与するだけで聞き取りやすさが大きく向上する。

**実装方針**

- テキストを SSML に変換するプリプロセッサを `Services/SsmlBuilder.cs` として追加
- `SpeechService.SpeakAsync()` / `SaveToWav()` で `SpeakSsml()` / `SpeakSsmlAsync()` に切り替える
- ポーズ長は設定値として UI から調整可能にする（将来対応）

**ポーズ設定案**

| 記号 | ポーズ |
|------|--------|
| `。` `！` `？` | 600 ms |
| `、` `・` | 200 ms |
| 改行 | 400 ms |
| 段落（空行） | 800 ms |

**SSML 出力例**

```xml
<speak version="1.0"
       xmlns="http://www.w3.org/2001/10/synthesis"
       xml:lang="ja-JP">
  市民の皆様へお知らせします。<break time="600ms"/>
  来月より、<break time="200ms"/>市役所の開庁時間が変わります。<break time="600ms"/>
</speak>
```

**注意点**

- SSML の `<break>` は日本語 SAPI 音声でも動作確認済み
- `<prosody>` タグのサポートは音声エンジンによって異なる（Haruka は部分サポート）
- SSML モード切替は既存の辞書置換後テキストに適用する

---

### 2. 数字・日付の読み前処理（効果: 高 / 難易度: 中）

自治体文書に頻出する数値表現を正しく読ませるための前処理。
辞書とは別に、パターンマッチングで動的に変換する。

**実装方針**

- `Services/TextPreprocessor.cs` として追加
- 辞書適用 → テキスト前処理 → SSML 変換 の順で処理
- 変換ルールは正規表現で管理

**変換パターン案**

| 入力例 | 変換後 | 備考 |
|--------|--------|------|
| `令和7年4月10日` | `れいわ7ねん4がつ10にち` | 年号は辞書で対応済み |
| `午前9時30分` | `ごぜん9じ30ふん` | |
| `1,000円` | `せんえん` | カンマ区切り数字 |
| `10,000円` | `いちまんえん` | |
| `〒123-4567` | `ゆうびんばんごう 123の4567` | |
| `TEL 042-xxx-xxxx` | `でんわ 042の...` | |
| `第3回` | `だい3かい` | |
| `p.3` `P.3` | `3ページ` | |

---

### 3. 読み速度の文脈調整（効果: 中 / 難易度: 中）

SSML の `<prosody rate>` で重要語の前後を意図的に遅くする。
ただし日本語 SAPI 音声での `<prosody>` サポートは限定的なため要検証。

**実装方針**

- 辞書エントリに `読み速度調整` フィールドを追加（将来対応）
- 該当語句を `<prosody rate="slow">語句</prosody>` で囲む

---

## 中長期: 音声エンジン移行戦略

上記 1〜3 の改善は現行エンジン（Windows SAPI / Haruka）の制約内での対応であり、
抑揚・自然さには原理的な上限がある。より高品質な読み上げを目指す場合は
以下の段階的移行戦略を検討する。

### Step 0: ISpeechEngine 抽象化（backlog #31 — 前提ステップ）

現行の `SpeechService` は `System.Speech.Synthesis.SpeechSynthesizer` に直接結合しており、
エンジンの差し替えが困難な状態にある。

移行に先立ち `ISpeechEngine` インターフェースを切り出し、現行実装を `SystemSpeechEngine` として
ラップする。これによりエンジンを設定で切り替え可能な構造になる。

```
SpeechService
└── ISpeechEngine（注入）
    ├── SystemSpeechEngine  ← 現行互換（既定値）
    ├── WinRtSpeechEngine   ← Step 1 で追加
    └── OpenJTalkEngine 等  ← Step 2 で追加
```

### Step 1: WinRT 音声エンジンへの移行（backlog #32 — 第一候補）

`Windows.Media.SpeechSynthesis`（WinRT API）を使う `WinRtSpeechEngine` を実装し、
端末に導入済みの OneCore 系音声を利用する。

| 観点 | 評価 |
|---|---|
| 閉域・オフライン | 〇（OS 標準機能） |
| 音声品質 | △〜〇（端末依存だが Haruka より自然なことが多い） |
| 実装コスト | 中（NuGet 追加・保存フロー差し替え） |
| 適合度 | **高**（現行運用に最も近い） |

**推奨**: まずこのステップで品質改善効果を確認する。

### Step 2: OSS 日本語 TTS エンジン同梱（backlog #33 — 品質重視）

Step 1 で十分な品質が得られない場合、OSS エンジン（OpenJTalk 系 / VOICEVOX 系）を評価する。

| エンジン候補 | 音声品質 | 配布物増加 | 運用コスト | 適合度 |
|---|---|---|---|---|
| OpenJTalk + HTS 音声モデル | 中 | 中（数十 MB） | 低 | 中〜高 |
| VOICEVOX エンジン（ローカル） | 高 | 大（数百 MB） | 高（別プロセス管理） | 中 |

ライセンス確認（音声モデル含む）と配布物サイズ評価が必要。

---

## 実装優先順位

```
【短期: 現行エンジン内改善】
1. 句読点ポーズ自動挿入   ← 効果が最も高く実装も容易（✅ v0.1.6 実装済み）
2. 数字・日付前処理        ← 自治体文書では頻出
3. prosody による速度調整  ← 音声エンジン依存のため優先度低

【中長期: エンジン移行】
4. ISpeechEngine 抽象化   ← 前提ステップ（backlog #31）
5. WinRT エンジン実装      ← 第一候補、まず検証（backlog #32）
6. OSS TTS エンジン同梱   ← Step 5 評価後に判断（backlog #33）
```

## 関連ファイル（実装時に追加・変更するファイル）

```
【短期: 現行エンジン内改善】
TxtToVoice/
├── Services/
│   ├── SsmlBuilder.cs        # 新規: テキスト → SSML 変換（✅ 実装済み）
│   └── TextPreprocessor.cs   # 新規: 数字・日付前処理
├── MainWindow.xaml.cs        # 変更: プレビューに前処理結果を反映
└── Services/
    └── SpeechService.cs      # 変更: SpeakAsync を SpeakSsmlAsync に切替（✅ 実装済み）

【中長期: エンジン移行（backlog #31〜33）】
TxtToVoice/
└── Services/
    ├── ISpeechEngine.cs       # 新規: エンジン抽象インターフェース（#31）
    ├── SystemSpeechEngine.cs  # 新規: 現行 System.Speech ラッパー（#31）
    ├── WinRtSpeechEngine.cs   # 新規: WinRT 実装（#32）
    └── OpenJTalkEngine.cs 等  # 新規: OSS TTS 実装（#33）
TxtToVoice/Models/
    └── AppSettings.cs         # 変更: SpeechEngineType フィールド追加（#31）
TxtToVoice/Dialogs/
    └── SettingsDialog.xaml    # 変更: エンジン種別選択 UI（#31）
```
