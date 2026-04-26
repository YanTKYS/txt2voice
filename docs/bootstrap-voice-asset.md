# mei_normal.htsvoice の初回ブートストラップ手順

## 背景

CI リリースワークフローは `mei_normal.htsvoice`（Mei 音声モデル）を
**前バージョンの GitHub Release asset から転用**して各リリースに添付します。

ただし、どのリリースにもこのファイルが存在しない場合（初回のみ）は
以下の手順で手動追加が必要です。一度追加すれば以降は自動で引き継がれます。

## 手順（初回のみ）

### 1. mei_normal.htsvoice を取得する

以下のいずれかの方法でファイルを入手してください（約 25 MB）。

**方法 A: setup_openjtalk.ps1 を使用**

```powershell
# Windows 端末で実行
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\tools\setup_openjtalk.ps1
```

セットアップ完了後、以下のパスにファイルが配置されます:
```
Data\openjtalk\voice\mei_normal.htsvoice
```

**方法 B: MMDAgent_Example-1.8.zip から手動取得**

1. https://sourceforge.net/projects/mmdagent/files/MMDAgent_Example/MMDAgent_Example-1.8/ を開く
2. `MMDAgent_Example-1.8.zip` をダウンロードして展開
3. `MMDAgent_Example-1.8\Voice\mei_normal\mei_normal.htsvoice` を取り出す

### 2. 既存の任意のリリースに asset として手動アップロード

```bash
# gh CLI を使用
gh release upload v0.5.0 mei_normal.htsvoice --clobber
```

バージョンは既存のリリースタグであればどれでも構いません。

### 3. 次回リリース以降は自動

次回 `release.yml` が実行された際に、ステップ「前バージョンの asset 転用」が
このファイルを自動的に引き継ぎます。

## ライセンス

HTS Voice "Mei" は **Creative Commons Attribution 3.0** で配布されています。  
再配布時は `THIRD_PARTY_LICENSES.txt` を同梱してクレジット義務を満たしてください。  
© 2009-2015 名古屋工業大学
