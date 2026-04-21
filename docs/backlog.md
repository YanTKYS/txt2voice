# バックログ（未着手・検討中）

未実装の改善項目のみを管理する。詳細・経緯・実装方針は
[improvement-proposals.md](./improvement-proposals.md) を参照。

---

## 優先度：高

| # | 概要 | 詳細 |
|---|---|---|
| 48 | OpenJTalk 実機テスト CI 化 | `openjtalk-engine-test.yml` を追加済み（手動 + 週次、`tools/setup_openjtalk.ps1` ベース、jtalkdll/音声モデルキャッシュ付き）。スキップ時は `Assert.Skip(reason)` により理由をワークフローサマリーに表示。SourceForge Cloudflare 制限の恒久対応は #49 に依存。 |
| 49 | OpenJTalk 配布自動化 | `tools/setup_openjtalk.ps1`（配布向けスタンドアロン版）を追加済み。リリース ZIP に同梱。CI・README・SettingsDialog の導線もこのスクリプトに統一済み。SourceForge Cloudflare の恒久対応（代替ホスト・事前同梱）は未解決。 |

---

## 優先度：中

| # | 概要 | 詳細 |
|---|---|---|
| 50 | OpenJTalk 向け数値・記号読み最適化 | ユーザー辞書による基本置換は StartSpeech() で共通適用済み。本項目は `TextPreprocessor` クラスを導入し以下を段階実装する。**フェーズ1**: 数字（3月→さんがつ、10%→じゅっぱーせんと）、**フェーズ2**: 記号（〒→ゆうびんばんごう、℃→ど）、**フェーズ3**: 慣用表現。各ルールにはゴールデンサンプルテスト（入力→期待読み仮名）を追加し回帰を防ぐ。 |
| 51 | OpenJTalk 音声品質評価・レポート | SAPI / WinRT との音質比較（自然さ・明瞭度）を `docs/improvement-proposals.md` にまとめる。評価セット（原稿・評価軸・採点表）を先に固定してから実施する。 |

---

## 優先度：低

| # | 概要 | 詳細 |
|---|---|---|
| — | *(現在なし)* | — |

---

## クローズ済み

| # | 概要 | 対応バージョン |
|---|---|---|
| 46 | OpenJTalk 同梱 PoC（技術検証） | v0.4.0 |
| 47 | OpenJTalkEngine 実装・UI 統合 | v0.4.1 |
