# バックログ（未着手・検討中）

未実装の改善項目のみを管理する。詳細・経緯・実装方針は
[improvement-proposals.md](./improvement-proposals.md) を参照。

---

## 優先度：高

| # | 概要 | 詳細 |
|---|---|---|
| 48 | OpenJTalk 実機テスト CI 化 | RequiresEngine テストを jtalk.dll/辞書/voice 配置済み専用ランナーで定期実行するジョブを整備。現状は実行環境依存で実質スキップが多い。 |
| 49 | OpenJTalk 配布自動化 | setup.ps1 生成物を配布 ZIP に含める手順またはインストーラ統合。SourceForge Cloudflare 問題の恒久対応含む。Release ビルド時の MSBuild 警告は実装済み（v0.4.1 後）。 |

---

## 優先度：中

| # | 概要 | 詳細 |
|---|---|---|
| 50 | OpenJTalk 辞書連携 | ユーザー辞書（DictionaryService）の読み替えを OpenJTalk 読み上げにも適用。数値・記号の読み最適化ルール追加で体感品質向上。 |
| 51 | OpenJTalk 音声品質評価・レポート | SAPI / WinRT との音質比較（自然さ・明瞭度）を `docs/improvement-proposals.md` にまとめ、推奨エンジンの指針を策定。 |

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
