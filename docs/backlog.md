# バックログ（未着手・検討中）

未実装の改善項目のみを管理する。詳細・経緯・実装方針は
[improvement-proposals.md](./improvement-proposals.md) を参照。

---

## オープン（未着手）

| # | 概要 | 優先度 | 備考 |
|---|---|---|---|
| 107 | 辞書の一括編集 | 中 | B-5: DataGrid インライン編集または CSV 直接編集 |
| 109 | テンプレート強化（検索・タグ等） | 低 | A-2: v0.7.1 テンプレート機能の拡張 |
| 110 | ファイル名命名テンプレート | 低 | B-7: 音声保存ファイル名のパターン変数化 |

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
| 59 | 運用監査向けエクスポート（監査 CSV 最小セット） | v0.5.6 |
| 68 | TextRuleLoader Regex タイムアウト（ReDoS 対策） | v0.5.7 |
| 69 | 性能テスト閾値の再設計（AC 導入後） | v0.5.7 |
| 67 | TextRule 運用 UI（読みルール画面） | v0.5.8 |
| 70 | 辞書インポート容量ガード | v0.5.9 |
| 71 | 読みルール保存後 ReplaceEngine 後の UI 再同期 | v0.6.0 |
| 72 | TextRuleDialog CancellationTokenSource 後始末 | v0.6.0 |
| 73 | 容量ガード指標を Reading 文字数にも拡張 | v0.6.0 |
| 75 | 読みルール有効ファイルパスをダイアログ上に表示 | v0.6.0 |
| 74 | 監査 CSV ローテーション（月次 audit_YYYYMM.csv） | v0.6.1 |
| 77 | 読みルール保存時・非Idle 通知 | v0.6.2 |
| 78 | 容量ガードをインポート後総量で判定 | v0.6.2 |
| 79 | 読みルール画面に無効ルール診断表示 | v0.6.2 |
| 80 | PathConfig/Audit テスト拡張 | v0.6.2 |
| 76 | 監査ログ保持期間/自動削除 | v0.6.3 |
| 81 | 設定ダイアログ：エンジン変更説明文修正 | v0.6.4 |
| 82 | 入力テキスト自動プレビュー更新トグル | v0.6.4 |
| 84 | SSML ON 時ハイライト自動無効化 | v0.6.4 |
| 86 | 自動プレビュー設定の永続化 | v0.6.5 |
| 87 | プレビューモード設定の永続化 | v0.6.5 |
| 88 | プレビューコピーボタン追加 | v0.6.5 |
| 89 | 音声保存進捗バー確定表示 | v0.6.6 |
| 90 | SSML ポーズ強度カスタマイズ（短め/標準/長め） | v0.6.6 |
| 91 | 辞書エントリ上下移動ボタン | v0.6.7 |
| 95 | BuildAppSettingsTests 新パラメータ対応 | v0.6.7 |
| 96 | 音声保存ファイル名プレフィックス設定 | v0.6.7 |
| 97 | 辞書ソート時の編集/削除対象ずれバグ修正 | v0.6.8 |
| 98 | 辞書移動 Ctrl+Shift+↑↓ ショートカット | v0.6.8 |
| 99 | README エンジン切替説明の実装整合 | v0.6.8 |
| 100 | 読みルール追加・編集 UI（TextRuleDialog の列を編集可能に） | v0.6.9 |
| 101 | 設定画面の基本/詳細タブ分離 | v0.7.0 |
| 102 | 読みルール上下移動ボタン（Ctrl+Shift+↑↓） | v0.7.0 |
| 93 | 原稿テンプレート機能（定型文の登録・挿入） | v0.7.1 |
| 103 | 再生プロファイル（音声・速度・音量・SSML 設定の保存と呼び出し） | v0.7.2 |
| 104 | プレビュー変換件数表示 + ◀▶ジャンプナビゲーション | v0.7.2 |
| 105 | ショートカット拡張（Ctrl+T=テンプレート挿入, Ctrl+L=読みルール編集） | v0.7.2 |
| 106 | 読み上げ範囲指定 UI（段落送り）— ◀▶ボタンで行単位ナビゲーション | v0.7.3 |
| 108 | 未保存変更ガード — ファイル読み込み前の内容破棄確認 | v0.7.3 |
