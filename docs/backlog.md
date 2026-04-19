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
| 44 | README テスト手順・構成図を v0.3.6 対応に更新 | v0.3.6 で追加した `TxtToVoice.Core` / `TxtToVoice.Core.Tests` をソース構成図とテスト手順に反映する。 |
| 45 | CI 2 レーン化（Core.Tests 必須 / Windows 依存テスト任意） | `TxtToVoice.Core.Tests`（net8.0）を全 PR 必須レーンに、`TxtToVoice.Tests`（net8.0-windows）を Windows runner 任意レーンに分離して回帰検知を強化する。 |
| 33 | OSS 日本語 TTS エンジン同梱（OpenJTalk / VOICEVOX 系）— PoC 計画フェーズ | バックログ未着手が本項目のみになったため調査フェーズから昇格。まず OpenJTalk 最小同梱 PoC（容量・辞書変換・起動時間）、次に VOICEVOX 比較評価を実施する。 |

---

## 優先度：低

| # | 概要 | 詳細 |
|---|---|---|
| — | *(現在なし)* | — |
