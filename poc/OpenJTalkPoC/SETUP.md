# OpenJTalk PoC セットアップ手順

backlog #46 の技術検証 PoC です。  
jtalkDLL + MMDAgent Mei ボイスを使って日本語テキスト → WAV 生成を確認します。

---

## 必要なファイル

```
poc/OpenJTalkPoC/
├── Data/openjtalk/
│   ├── jtalk.dll                    ← jtalkdll (GitHub) からビルド
│   ├── open_jtalk_dic_utf_8/        ← SourceForge からダウンロード
│   │   ├── char.bin
│   │   ├── sys.dic
│   │   └── ... (辞書ファイル一式)
│   └── voice/
│       └── mei_normal.htsvoice      ← MMDAgent からダウンロード
└── (その他プロジェクトファイル)
```

---

## 手順 1: jtalk.dll を用意する

### 方法 A: jtalkdll をソースからビルドする（推奨）

**前提**: Visual Studio 2022 + CMake がインストールされていること。

```powershell
# リポジトリをクローン
git clone https://github.com/rosmarinus/jtalkdll.git
cd jtalkdll

# ビルドスクリプトを使用（詳細は GitHub README を参照）
# 成功すると jtalk.dll が生成される
```

生成された `jtalk.dll` を `Data/openjtalk/` に配置してください。

> **依存 DLL について**: jtalk.dll は VC++ ランタイムに依存する場合があります。  
> 同じディレクトリに必要な依存 DLL もコピーしてください。

### 方法 B: CI アーティファクトや配布パッケージを利用する

jtalkdll の GitHub Actions アーティファクトや、jtalkdll を同梱した  
サードパーティ配布物がある場合はそちらを利用してください。

---

## 手順 2: MeCab UTF-8 辞書をダウンロードする

1. SourceForge の Open JTalk ページを開く  
   `https://sourceforge.net/projects/open-jtalk/files/Dictionary/`

2. `open_jtalk_dic_utf_8-1.11.tar.gz` をダウンロード（約 24 MB）

3. 解凍して中身のディレクトリを丸ごと `Data/openjtalk/` に配置

```
Data/openjtalk/open_jtalk_dic_utf_8/
  ├── char.bin
  ├── matrix.bin
  ├── sys.dic
  └── ...
```

---

## 手順 3: Mei 音声モデルをダウンロードする

### MMDAgent_Example から取得する（推奨）

1. SourceForge の MMDAgent ページを開く  
   `https://sourceforge.net/projects/mmdagent/files/`

2. `MMDAgent_Example-1.8.zip`（約 30 MB）をダウンロード

3. ZIP を解凍し、以下のファイルを `Data/openjtalk/voice/` にコピー

```
MMDAgent_Example/Voice/mei/
  ├── mei_normal.htsvoice    ← メインの音声モデル（通常音声）
  ├── mei_happy.htsvoice     ← 必要であれば
  └── ...
```

### hts_voice_nitech（男性ボイス）を使う場合

1. SourceForge の Open JTalk ページを開く  
   `https://sourceforge.net/projects/open-jtalk/files/HTS%20voice/`

2. `hts_voice_nitech_jp_atr503_m001-1.05.tar.gz` をダウンロード

3. 解凍して `.htsvoice` ファイルを `Data/openjtalk/voice/` に配置

---

## 手順 4: ビルドして実行する

```powershell
cd poc/OpenJTalkPoC
dotnet build -c Release
dotnet run -c Release
```

または Visual Studio / Rider で直接実行してください。

---

## 実行結果の見方

```
============================================================
OpenJTalk PoC — 技術検証
============================================================

[1] 初期化
  初期化時間 : XXX ms  ✅ (基準: 5000 ms 以内)

[2] WAV 生成
  [短文テスト]
    テキスト : 本日は晴天なり。
    結果     : ✅ 成功  XXX KB  生成時間: XXX ms
    出力先   : .../output_短文テスト.wav

[3] データサイズ
  MeCab 辞書      : 約 XX MB
  音声モデル      : X.X MB
  jtalk.dll       : X.X MB
  合計             : 約 XX MB

[判定サマリー]
  ✅ 初期化時間 5 秒以内
  ✅ WAV 生成成功

★ 音声品質は output_*.wav を再生して SAPI / WinRT と比較してください。
```

生成された `output_*.wav` を再生して音声品質を確認し、結果を  
`docs/improvement-proposals.md` の #46 セクションに記録してください。

---

## ライセンス注記

このPoC で使用するコンポーネントのライセンス（詳細は `docs/improvement-proposals.md` #33 参照）:

| コンポーネント | ライセンス |
|---|---|
| jtalkdll オリジナル | MIT License |
| Open JTalk / MeCab | Modified BSD License |
| HTS Voice "Mei" | CC BY 3.0（クレジット表示必須） |

製品への組み込み時は `THIRD_PARTY_LICENSES.txt` を同梱してください（#47 で実装予定）。
