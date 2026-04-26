# バックログ（未着手・検討中）

未実装の改善項目のみを管理する。詳細・経緯・実装方針は
[improvement-proposals.md](./improvement-proposals.md) を参照。

---

## 優先度：低〜中（v0.5.x）

| # | 概要 | 詳細 |
|---|---|---|
| 60 | 辞書照合 Aho-Corasick 導入 | 辞書エントリが 1,000 件超を想定する前に設計着手。詳細 #60。 |

---

## 優先度：低（中長期）

| # | 概要 | 詳細 |
|---|---|---|
| 58 | 設定反映タイミング改善 | エンジン再初期化による即時反映の PoC（リソース解放漏れ等のリスク確認が先）。詳細 #58。 |
| 59 | 運用監査向けエクスポート | 保存操作の最小監査ログを CSV 出力（個人情報なし・ハッシュ化）。詳細 #59。 |

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
