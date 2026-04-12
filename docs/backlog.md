# バックログ（未着手・検討中）

未実装の改善項目のみを管理する。詳細・経緯・実装方針は
[improvement-proposals.md](./improvement-proposals.md) を参照。

---

## 優先度：高

| # | 概要 | 詳細 |
|---|---|---|
| — | *(現在なし)* | — |

---

## 優先度：中

| # | 概要 | 詳細 |
|---|---|---|
| 25 | 監査モード INFO 抑制の起動直後適用 | App.OnStartup の先頭で clearSensitiveDataOnExit を先読みして Logger.SuppressInfo を即時設定する。AppSettingsService に ReadAuditFlag() を追加。 |

---

## 優先度：低

| # | 概要 | 詳細 |
|---|---|---|
| 21 | テキスト読み込みエンコード判定の README/コード整合 | UTF-16 非対応を README に明記、または `StreamReader` の BOM 自動検出で対応範囲を拡張。 |
| 22 | CI パフォーマンステスト閾値の環境依存対策 | 絶対時間閾値を緩和、または `[Trait]` で CI から除外して週次ジョブに移行。 |
| 24 | v0.2.x 向けテスト追加 | `PathConfig.PortableFallbackApplied` ロジックのユニットテスト・`SpeechService` キャンセル伝播テストを追加。 |
