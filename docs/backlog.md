# バックログ（未着手・検討中）

未実装の改善項目のみを管理する。詳細・経緯・実装方針は
[improvement-proposals.md](./improvement-proposals.md) を参照。

---

## 優先度：高

| # | 概要 | 詳細 |
|---|---|---|
| 36 | BuildAppSettings テストの追加 | `BuildAppSettings(isExit)` は保存ポリシー分岐が多く、将来の設定項目追加で回帰しやすい。ViewModel 層への分離か、WPF テストフレームワーク経由でのテストを検討。`SpeechEngineFactoryTests` は v0.3.3 で追加済み。 |
| 38 | ログ匿名化の強化（空白パス・UNCパス対応） | 現行の `[^\s,"']+` パターンは空白を含むパス（例: `C:\My Documents\...`）および UNC パス（例: `\\server\share\...`）を取りこぼす。複数パターン化・引用符付きパスへの対応を検討。 |
| 39 | v0.3.4 追加機能へのテスト追加（回帰防止） | DictionaryService キャッシュ無効化・CSV 重複マージ・Logger.AnonymizePaths・保存進捗メッセージが未テスト。4 つの新テストクラスを追加して回帰を防ぐ。 |

---

## 優先度：中

| # | 概要 | 詳細 |
|---|---|---|
| 40 | CSV 重複判定の計算量最適化 | `imported` を 2 回走査し `HasDisplay()`（線形検索）を繰り返す構造を、`HashSet<string>` 化で O(1) 判定・1 パス振り分けに改善する。 |
| 41 | 音声選択の安定化（表示名ではなく ID 保存） | WinRT 側は DisplayName で保存しており同名音声がある環境では誤選択リスクがある。`voiceId`（内部識別子）と `voiceDisplayName`（表示用）に分離し、`voiceName` を移行期後方互換キーとして扱う。 |

---

## 優先度：低

| # | 概要 | 詳細 |
|---|---|---|
| 29 | テスト構成の分離 | 純ロジック層（DictionaryService 等）を net8.0 でテスト可能に分離（大規模）。中間キャンセルテストは単独で追加可能。 |
| 33 | OSS 日本語 TTS エンジン同梱（OpenJTalk / VOICEVOX 系） | #32 評価後に検討。ライセンス概要確認済み（詳細は speech-quality-improvement.md）。配布物単位の棚卸しと同梱物ライセンス個別確認が必要。 |
| 37 | WinRT 保存処理の長文メモリ効率改善 | v0.3.3 で `using` 解放と事前確保は対応済み。長文対応として一時 WAV ファイル経由のストリーミングエンコード（`MemoryStream` 廃止）を別途検討。 |
