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
| — | *(現在なし)* | — |
