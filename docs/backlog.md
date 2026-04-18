# バックログ（未着手・検討中）

未実装の改善項目のみを管理する。詳細・経緯・実装方針は
[improvement-proposals.md](./improvement-proposals.md) を参照。

---

## 優先度：高

| # | 概要 | 詳細 |
|---|---|---|
| 34 | WinRT エンジンの読み上げ箇所ハイライト対応 | `MediaPlaybackItem.TimedMetadataTracks` + `SpeechCue` を使い単語境界イベントを取得して `SpeakProgress` を発火。`_synth.Options.IncludeWordBoundaryMetadata = true` 設定が必要。SAPI との機能パリティ回復。 |

---

## 優先度：中

| # | 概要 | 詳細 |
|---|---|---|
| 26 | 辞書置換エンジンの高速化 | 都度ソートをキャッシュ化（最小対応）→ Aho-Corasick 移行（高度）。性能テスト閾値を実運用サイズ（1〜3 秒台）に再定義。 |
| 27 | CSV インポート時の重複語句マージポリシー明確化 | インポート前に重複検出し「上書き/スキップ/両方保持」をユーザーが選択できるように。追加/重複/更新件数のプレビュー表示も推奨。 |
| 35 | エンジン設定値の正規化（未知値の自己修復） | `ReadEngineType()` は値検証なしでそのまま返す。`SpeechEngineFactory.Create()` は未知値を `SystemSpeech` にフォールバックするため「使用エンジン」と「設定値文字列」が乖離しうる。起動時に `NormalizeEngineType()` を通して未知値なら `Default` に置換して即保存する。 |
| 36 | v0.3.2 リファクタリングのテスト新設 | `SpeechEngineFactoryTests`（Create / GetLabel / 未知値フォールバック）と `BuildAppSettingsTests`（`isExit` true/false × 機微データポリシー分岐）を追加。#33 対応前に回帰テストの網を張る。 |
| 37 | WinRT 保存処理のメモリ効率・リソース管理改善 | 現在は合成ストリームを `MemoryStream` に丸ごとコピーしてからエンコードしており、長文時にメモリピークが上がりやすい。`SpeechSynthesisStream` の `using` 明示化と、一時 WAV ファイル経由のストリーミングエンコード（長文安定性優先）の検討。 |

---

## 優先度：低

| # | 概要 | 詳細 |
|---|---|---|
| 28 | 音声保存進捗の可視化改善 | フェーズラベル（音声生成中/エンコード中）を追加。キャンセル押下後を「停止処理中...」に変更しボタン無効化。 |
| 29 | テスト構成の分離 | 純ロジック層（DictionaryService 等）を net8.0 でテスト可能に分離（大規模）。中間キャンセルテストは単独で追加可能。 |
| 30 | 監査強化モードのログ匿名化 | 監査モード時に WARN/ERROR のファイルフルパスをファイル名のみ（ディレクトリ伏せ）に変換して記録。 |
| 33 | OSS 日本語 TTS エンジン同梱（OpenJTalk / VOICEVOX 系） | #32 評価後に検討。ライセンス概要確認済み（詳細は speech-quality-improvement.md）。配布物単位の棚卸しと同梱物ライセンス個別確認が必要。 |
| 38 | README の音声エンジン選択説明を v0.3 系仕様に更新 | 「音声エンジン選択」節を追加（SAPI / WinRT の用途別推奨、再起動適用の明記）。トラブルシュートに WinRT 専用項目（音声名の差異、ハイライト非対応）を追加。 |
