# リリース前チェックリスト

各バージョンのリリース前に確認する項目。すべて ✅ になってからタグを打つこと。

---

## 必須チェック

### ビルド・テスト

- [ ] `dotnet build TxtToVoice.sln -c Release` が警告なしで成功する
- [ ] `dotnet test TxtToVoice.Core.Tests/TxtToVoice.Core.Tests.csproj -c Release` が全件 Pass する
- [ ] `dotnet test TxtToVoice.Tests/TxtToVoice.Tests.csproj -c Release --filter "Category!=RequiresEngine"` が全件 Pass する
- [ ] `openjtalk-engine-test.yml` ワークフローの RequiresEngine テストが全件 Pass する

### バージョン・ドキュメント

- [ ] `TxtToVoice/TxtToVoice.csproj` の `<Version>` がリリースタグと一致している（例: タグ `v0.9.3` → `<Version>0.9.3</Version>`）
- [ ] `release-notes/v{バージョン}.md` が存在すること
- [ ] `CHANGELOG.md` に当該バージョンのエントリが追加されていること
- [ ] `docs/backlog.md` のクローズ済みテーブルに当該バージョンの項目が移動されていること

### THIRD_PARTY_LICENSES.txt

- [ ] 新たに追加した NuGet パッケージ（船積みされるもの）のライセンスが記載されていること
- [ ] 各エントリに **著作権表示（Copyright 行）** と **ライセンス全文または全文参照** が含まれていること
- [ ] CC BY 3.0 コンポーネント（HTS Voice "Mei"）のクレジット表示義務が満たされていること

### 配布パッケージ内容確認

- [ ] `release.yml` ワークフローが成功し、Release asset として以下が存在すること:
  - `TxtToVoice-v{バージョン}-win-x64.zip`
  - `mei_normal.htsvoice`
- [ ] ZIP を展開して以下のファイルが **すべて** 含まれていること:
  - `TxtToVoice.exe`
  - `Data/sample_dictionary.json`
  - `Data/text_rules.json`
  - `THIRD_PARTY_LICENSES.txt`
  - `setup_openjtalk.ps1`
- [ ] ZIP を展開して `TxtToVoice.exe` が起動することをローカルで確認すること
- [ ] ヘルプ → バージョン情報 でバージョン番号が正しく表示されること

### setup_openjtalk.ps1 動作確認

- [ ] スクリプトを EXE と同じフォルダに置き、`-VerifyOnly` で実行:
  ```powershell
  .\setup_openjtalk.ps1 -VerifyOnly
  ```
  - セットアップ済み環境で `exit 0` かつ全コンポーネント `[OK]` になること
  - 未セットアップ環境で `exit 1` かつ不足コンポーネントが `[NG]` になること
- [ ] フルセットアップ実行後、OpenJTalk で読み上げが成功すること
  ```powershell
  Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
  .\setup_openjtalk.ps1
  ```

### クリーン環境検証（新規インストール相当）

- [ ] 配布 ZIP を **別のフォルダ** に展開し、初回起動する
- [ ] 既存設定ファイルがない状態で起動エラーが出ないこと（デフォルト値で起動できること）
- [ ] ヘルプ → ショートカットキー一覧 が正しく表示されること
- [ ] ヘルプ → バージョン情報 で設定/辞書/テンプレート/ログのパスが表示されること

---

## 音声品質評価（#51 / #55）

### WAV 生成

1. `openjtalk-engine-test.yml` ワークフローを手動実行する
2. ワークフロー完了後、アーティファクト `voice-quality-eval-wavs` をダウンロードする
3. S1〜S4 の WAV ファイルを聴取する

### 評価原稿（S1〜S4）

| ID | 原稿 | 評価ポイント |
|----|------|-------------|
| S1 | 今月3月の広報紙をお届けします。消費税は10%です。 | 月・%読み（TextPreprocessor対象） |
| S2 | 〒100-0001東京都千代田区、気温は25℃、面積50㎡。 | 記号読み |
| S3 | 令和7年度の予算案について市民の皆様にご説明いたします。 | 長文・接続詞の流暢さ |
| S4 | ご不明な点は、0120-XXX-YYYにお電話ください。 | 電話番号読み |

### 採点表（1〜5、高=良）

| 評価軸 | S1 | S2 | S3 | S4 |
|--------|----|----|----|----|
| 自然さ（イントネーション） | — | — | — | — |
| 明瞭度（聴き取りやすさ） | — | — | — | — |
| 数値・記号の読み正確性 | — | — | — | — |
| 読み上げ速度（適切さ） | — | — | — | — |

### 合否基準

- 前バージョン比較で明らかな劣化（スコア -1 以上の低下）がないこと
- S1〜S4 すべてで数値・記号の読み正確性が **3 以上**であること

評価完了後、採点表に結果を記入し `docs/improvement-proposals.md` の #51 セクションに追記すること。
