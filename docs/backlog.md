# バックログ（未着手・検討中）

未実装の改善項目のみを管理する。詳細・経緯・実装方針は
[improvement-proposals.md](./improvement-proposals.md) を参照。

---

## 優先度：中

| # | 概要 | 詳細 |
|---|---|---|
| 59 | 運用監査向けエクスポート（監査 CSV 最小セット） | timestamp / engineType / format / success / errorCode / fileHash を CSV 追記。詳細 #59。 |

---

## 優先度：低〜中

| # | 概要 | 詳細 |
|---|---|---|
| 67 | TextRule 運用 UI（設定→読みルール 画面） | ルール一覧の ON/OFF 切替 + テスト入力プレビュー。詳細 #67。 |
| 68 | TextRuleLoader Regex タイムアウト | `Regex(..., timeout)` でフェイルセーフ化（ReDoS 対策）。詳細 #68。 |
| 69 | 性能テスト閾値の再設計（AC 導入後） | 現行 30s/45s 閾値を実測ベースに引き下げ。詳細 #69。 |

---

## 優先度：低（中長期）

| # | 概要 | 詳細 |
|---|---|---|
| 70 | 辞書インポート容量ガード | 件数/総文字数が閾値超で確認ダイアログ。AC 構築コストのガード。詳細 #70。 |

---

## クローズ済み

| # | 概要 | 対応バージョン |
|---|---|---|
| 46 | OpenJTalk 同梱 PoC（技術検証） | v0.4.0 |
| 47 | OpenJTalkEngine 実装・UI 統合 | v0.4.1 |
| 48a | OpenJTalk 実機テスト CI 化（実装済み分） | v0.4.2 |
| 49a | OpenJTalk 配布自動化（実装済み分） | v0.4.2 |
| 50-p1 | TextPreprocessor フェーズ1（X月・%） | v0.4.2 |
| 50-p2 | TextPreprocessor フェーズ2（全角正規化・〒/℃/㎡）+ Core 移設 | v0.4.3 |
| 50-p3 | TextPreprocessor フェーズ3（Xか月・第X回・電話番号ハイフン） | v0.4.4 |
| 53 | SystemSpeechEngine MP3/MP4 メモリ最適化 | v0.4.4 |
| 52-a | OpenJTalk セットアップ自己診断（`-VerifyOnly` + UI チェックリスト） | v0.4.5 |
| 54 | エラーコード付与（`TTV-E-OJT-001` 等・ログ + ダイアログ） | v0.4.6 |
| 63 | ログ匿名化ハードニング（メッセージ長キャップ + ポリシー明文化） | v0.4.6 |
| 57 | 辞書インポートバリデーション強化（空読み・優先順位補正レポート） | v0.4.7 |
| 52-b | OpenJTalk 同梱ファイル優先配置（`bundled\mei_normal.htsvoice`） | v0.4.7 |
| 52-c | setup_openjtalk.ps1 URL 複数候補化 + 最終サマリ表示 | v0.4.8 |
| 64 | 辞書一覧リアルタイム絞り込み（TxtDictFilter + ICollectionView.Filter） | v0.4.8 |
| 56-a | MainWindow SettingsOperations 分離 + RecentFiles を FileOperations へ移動 | v0.4.9 |
| 65 | TextPreprocessor フェーズ4（時刻パターン X時 / X分） | v0.4.9 |
| 56-b | PlaybackState sealed record 導入（_isSpeaking / _isPaused 置き換え） | v0.5.0 |
| 62 | 行政文書ゴールデンテスト（TextPreprocessorGoldenTests） | v0.5.0 |
| 66 | TextPreprocessor フェーズ5（コロン時刻 HH:MM → X時X分展開） | v0.5.0 |
| 49b | SourceForge Cloudflare 恒久対応（GitHub Release asset 方針A） | v0.5.1 |
| 61 | TextPreprocessor 外部ルール定義（`Data/text_rules.json` 外部化） | v0.5.2 |
| 51 | OpenJTalk 音声品質評価 CI インフラ整備（VoiceQualityEvalTests + artifact） | v0.5.3 |
| 55 | 音声品質評価の定例化（`docs/release-checklist.md` 新規作成） | v0.5.3 |
| 60 | 辞書照合 Aho-Corasick 導入（O(n×m) → O(n+m)） | v0.5.4 |
| 58 | 設定反映タイミング改善（安全条件つき即時反映） | v0.5.5 |
