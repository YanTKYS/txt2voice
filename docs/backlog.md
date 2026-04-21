# バックログ（未着手・検討中）

未実装の改善項目のみを管理する。詳細・経緯・実装方針は
[improvement-proposals.md](./improvement-proposals.md) を参照。

---

## 優先度：高

| # | 概要 | 詳細 |
|---|---|---|
| 48 | OpenJTalk 回帰テスト充実 | `RequiresEngine` テストを jtalk.dll 有環境で実行・CI パイプライン整備。IsAvailable=false 時のメッセージ検証は jtalk.dll 不在環境で常時パスすることを確認済み。 |
| 49 | OpenJTalk 配布自動化 | setup.ps1 で生成した Data\openjtalk\ を TxtToVoice の配布 ZIP に含める手順またはインストーラ統合（SourceForge Cloudflare 問題の恒久対応含む）。 |

---

## 優先度：中

| # | 概要 | 詳細 |
|---|---|---|
| 50 | OpenJTalk 辞書連携 | アプリ内ユーザー辞書（DictionaryService）をテキスト前処理として OpenJTalk 読み上げにも適用する（現状は SAPI / WinRT のみ SSML 経由で適用）。 |
| 51 | OpenJTalk 音声品質評価・レポート | SAPI / WinRT との音質比較（自然さ・明瞭度）を `docs/improvement-proposals.md` にまとめ、推奨エンジンの指針を策定する。 |

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
