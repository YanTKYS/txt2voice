#Requires -Version 5.1
<#
.SYNOPSIS
    OpenJTalk PoC セットアップ・ビルド・実行スクリプト

.DESCRIPTION
    以下を自動化します。
      [1] jtalkDLL を GitHub からビルド（CMake + MSVC）
      [2] MeCab UTF-8 辞書を SourceForge からダウンロード・配置
      [3] Mei 音声モデルを SourceForge からダウンロード・配置
      [4] poc/OpenJTalkPoC を dotnet build で Release ビルド
      [5] dotnet run で実行し output_*.wav を生成

    各ステップはスキップ判定付き（再実行時は既存ファイルを使用）。

.NOTES
    前提ツール:
      - Git
      - .NET 8 SDK
      - CMake 3.15 以上
      - MSVC C++ コンパイラ（以下のいずれか）
          * Visual Studio Build Tools 2019/2022（推奨・IDE 不要・軽量）
            インストーラ: https://visualstudio.microsoft.com/visual-cpp-build-tools/
            ワークロード: 「C++ によるデスクトップ開発」にチェック
          * Visual Studio 2019/2022（C++ によるデスクトップ開発 ワークロード）
        ※ VS Code 単体は C++ コンパイラを含まないため不可

    実行場所: リポジトリ内どこからでも可（スクリプトが自身の場所を検出）

.EXAMPLE
    # PowerShell から直接実行する場合
    Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
    .\poc\OpenJTalkPoC\setup.ps1
#>

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# ---- パス定義 ----------------------------------------------------------------

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { $PWD.Path }
$pocDir    = $scriptDir
$dataDir   = Join-Path $pocDir "Data\openjtalk"
$dicDir    = Join-Path $dataDir "open_jtalk_dic_utf_8"
$voiceDir  = Join-Path $dataDir "voice"
$dllDest   = Join-Path $dataDir "jtalk.dll"
# %TEMP% は CMake 4.x の pkgRedirects 書き込みでアクセス拒否になる場合があるため
# ユーザーホームディレクトリ配下（C:\Users\<name>\openjtalk-poc-setup）を使用する
$tmpDir    = Join-Path $env:USERPROFILE "openjtalk-poc-setup"

$csproj    = Join-Path $pocDir "OpenJTalkPoC.csproj"

# ---- ダウンロード URL --------------------------------------------------------

$dicUrl    = "https://downloads.sourceforge.net/project/open-jtalk/Dictionary/open_jtalk_dic_utf_8-1.11.tar.gz"
$mmdUrl    = "https://downloads.sourceforge.net/project/mmdagent/MMDAgent_Example/MMDAgent_Example-1.8/MMDAgent_Example-1.8.zip"
$jtalkRepo = "https://github.com/rosmarinus/jtalkdll.git"

# ---- ユーティリティ関数 ------------------------------------------------------

function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host "==[ $msg ]" + ("=" * [Math]::Max(0, 58 - $msg.Length)) -ForegroundColor Cyan
}
function Write-OK([string]$msg)   { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Info([string]$msg) { Write-Host "  [..] $msg" -ForegroundColor Gray }
function Write-Warn([string]$msg) { Write-Host "  [!!] $msg" -ForegroundColor Yellow }
function Write-Fail([string]$msg) { Write-Host "  [NG] $msg" -ForegroundColor Red; exit 1 }

function Get-VsWhere {
    $paths = @(
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\Installer\vswhere.exe"
    )
    foreach ($p in $paths) { if (Test-Path $p) { return $p } }
    return $null
}

function Find-Cmake {
    # 1. PATH に cmake があれば優先
    $cmd = Get-Command cmake -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    # 2. スタンドアロン CMake インストール先（インストーラ既定パス）
    $candidates = [System.Collections.Generic.List[string]]@(
        "$env:ProgramFiles\CMake\bin\cmake.exe",
        "${env:ProgramFiles(x86)}\CMake\bin\cmake.exe"
    )

    # 3. Visual Studio / Build Tools にバンドルされた CMake
    $vsw = Get-VsWhere
    if ($vsw) {
        $roots = & $vsw -products * -all -property installationPath 2>$null
        foreach ($root in @($roots)) {
            $bundled = Join-Path $root "Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
            $candidates.Add($bundled)
        }
    }

    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    return $null
}

function Invoke-Download([string]$url, [string]$dest) {
    if (Test-Path $dest) {
        Write-OK "既存ファイルを使用: $(Split-Path -Leaf $dest)"
        return
    }
    Write-Info "ダウンロード中: $(Split-Path -Leaf $dest) ..."
    try {
        Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing -MaximumRedirection 10
        Write-OK "ダウンロード完了: $(Split-Path -Leaf $dest)"
    } catch {
        Write-Fail "ダウンロード失敗: $url`n  エラー: $_"
    }
}

# git / cmake / tar / dotnet は進捗を stderr に書くため、
# $ErrorActionPreference = "Stop" 下では NativeCommandError が発生する。
# このラッパー内では一時的に "Continue" に切り替え、終了コードで成否を判定する。
function Invoke-Native {
    param(
        [Parameter(Mandatory)][scriptblock]$ScriptBlock,
        [string]$ErrorMessage = "コマンドが失敗しました"
    )
    $local:ErrorActionPreference = "Continue"
    & $ScriptBlock
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "$ErrorMessage (exit code: $LASTEXITCODE)"
    }
}

# ============================================================================
# [0] 前提ツール確認
# ============================================================================

Write-Step "0/5 前提ツール確認"

# Git
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Fail "git が見つかりません。https://git-scm.com/ からインストールしてください。"
}
Write-OK "git $(git --version)"

# .NET SDK
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Fail "dotnet が見つかりません。.NET 8 SDK をインストールしてください。"
}
$dotnetVer = dotnet --version
Write-OK "dotnet $dotnetVer"
if ($dotnetVer -notmatch "^8\.") {
    Write-Warn ".NET $dotnetVer を検出しました。本プロジェクトは net8.0-windows が対象です。"
}

# CMake（PATH 未登録でも定番インストール先・VSバンドル版を探す）
$cmake = Find-Cmake
if (-not $cmake) {
    Write-Fail @"
cmake が見つかりません。以下のいずれかを確認してください:
  - CMake 3.15 以上をインストール済みの場合は PATH に追加されているか確認
    （CMake インストーラの「Add CMake to the system PATH」を選ぶか
      システム環境変数 PATH に cmake.exe のフォルダを追加）
  - 未インストールの場合: https://cmake.org/download/
"@
}
$cmakeVer = & $cmake --version | Select-Object -First 1
Write-OK "$cmakeVer"
Write-Info "  $cmake"

# MSVC コンパイラ（Visual Studio または Build Tools）を vswhere で検出
# -products * を付けることで Build Tools（IDE なし軽量版）も対象に含める
$vsWhere = Get-VsWhere
if (-not $vsWhere) {
    Write-Fail @"
Visual Studio インストーラ (vswhere.exe) が見つかりません。
以下のいずれかをインストールしてください（「C++ によるデスクトップ開発」ワークロード必須）:
  推奨: Visual Studio Build Tools 2022（IDE 不要・軽量）
        https://visualstudio.microsoft.com/visual-cpp-build-tools/
  または: Visual Studio 2022 Community/Professional
"@
}
$vsInstallPath    = & $vsWhere -products * -latest -property installationPath
# installationVersion (例: "17.8.3.0") はフル VS・Build Tools 両方で確実に取得できる
# catalog_productLineVersion は Build Tools では不安定なため使わない
$vsInstallVersion = & $vsWhere -products * -latest -property installationVersion
if (-not $vsInstallPath) {
    Write-Fail @"
MSVC C++ コンパイラが検出できませんでした。
Visual Studio Build Tools または Visual Studio 2019/2022 に
「C++ によるデスクトップ開発」ワークロードを追加してください。
"@
}
$vsMajor = [int](($vsInstallVersion -split '\.')[0])
Write-OK "MSVC $vsInstallVersion (major=$vsMajor): $vsInstallPath"

# cmake --help のジェネレータ一覧から "Visual Studio {major} YYYY" を動的に取得する。
# 年号（2019/2022/2026...）は cmake バージョンごとに変わるためハードコードしない。
$savedEap = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$cmakeHelp = (& $cmake --help 2>&1) -join "`n"
$ErrorActionPreference = $savedEap

if ($cmakeHelp -match "Visual Studio $vsMajor (\d{4})") {
    $cmakeGen = "Visual Studio $vsMajor $($matches[1])"
} else {
    Write-Fail @"
cmake に対応するジェネレータ (Visual Studio $vsMajor) が見つかりません。
cmake --help で表示される Visual Studio 行を確認してください。
インストール済み Build Tools のバージョンと cmake のバージョンが合っていない可能性があります。
"@
}
Write-Info "CMake ジェネレータ: $cmakeGen"

# ============================================================================
# [1] jtalkDLL のビルド
# ============================================================================

Write-Step "1/5 jtalkDLL のビルド"

if (Test-Path $dllDest) {
    Write-OK "jtalk.dll は既に存在します（スキップ）"
    Write-Info "  $dllDest"
} else {
    $jtalkSrc = Join-Path $tmpDir "jtalkdll"
    $jtalkBld = Join-Path $jtalkSrc "build"

    New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null
    New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

    # --- クローン ---
    if (Test-Path (Join-Path $jtalkSrc ".git")) {
        Write-OK "既存クローンを使用: $jtalkSrc"
    } else {
        Write-Info "jtalkdll をクローン中 ..."
        Invoke-Native { git clone --recursive $jtalkRepo $jtalkSrc } "git clone に失敗しました"
        Write-OK "クローン完了"
    }

    # --- VS 2026 / MSVC 19.5x 互換パッチ ---
    # open_jtalk-1.11/mecab が std::binary_function を使用しており、
    # C++17 以降では削除済みのため error C2039/C2504 が発生する。
    # cmake の CXX_FLAGS や CXX_STANDARD では VS ジェネレータ経由で確実に届かないため、
    # クローン後にソースを直接修正する。
    $mecabSrcDir = Join-Path $jtalkSrc "open_jtalk-1.11\mecab\src"
    foreach ($fname in @("dictionary.cpp")) {
        $fpath = Join-Path $mecabSrcDir $fname
        if (-not (Test-Path $fpath)) { Write-Warn "パッチ対象が見つかりません: $fname"; continue }
        $original = [System.IO.File]::ReadAllText($fpath)
        # ": public std::binary_function<...>" を除去（C++17 で削除されたクラス）
        $patched = $original -replace ':\s*public\s+std::binary_function<[^{]+>', ''
        if ($original -ne $patched) {
            [System.IO.File]::WriteAllText($fpath, $patched)
            Write-OK "パッチ適用: $fname (std::binary_function 除去)"
        } else {
            Write-Info "パッチ不要: $fname (既に修正済みか対象コードなし)"
        }
    }

    # --- CMake 設定 ---
    if (Test-Path $jtalkBld) {
        Write-Info "前回のビルドディレクトリをクリア ..."
        Remove-Item -Recurse -Force $jtalkBld
        # Windows はディレクトリ削除を非同期で確定するため、
        # 直後に同パスへ書き込むと cmake 4.x の pkgRedirects 作成が失敗する
        Start-Sleep -Milliseconds 500
    }
    # CMake 4.x が pkgRedirects を作成する前に親ディレクトリを事前作成して競合を回避
    New-Item -ItemType Directory -Force -Path "$jtalkBld\CMakeFiles\pkgRedirects" | Out-Null
    Write-Info "CMake 設定中 ..."
    # -DCMAKE_POLICY_VERSION_MINIMUM=3.5:
    #   jtalkdll に同梱の portaudio が cmake_minimum_required に 3.5 未満を指定しており
    #   CMake 4.x ではそのバージョンサポートが削除されたためエラーになる。
    #   このフラグで 3.5 以上として扱うよう指示し回避する。
    # ※ 変数に格納して渡す（リテラルのまま書くと PowerShell が "=3.5" などを再トークン化し
    #   cmake に誤った値が渡される問題を回避）
    $policyArg = '-DCMAKE_POLICY_VERSION_MINIMUM=3.5'
    Invoke-Native { & $cmake -S $jtalkSrc -B $jtalkBld -G $cmakeGen -A x64 $policyArg } "CMake の設定に失敗しました"

    # --- ビルド ---
    Write-Info "ビルド中（Release / x64）..."
    Invoke-Native { & $cmake --build $jtalkBld --config Release } "jtalk.dll のビルドに失敗しました"

    # --- DLL 検索 ---
    # Release ディレクトリ内の DLL を列挙
    $releaseDlls = Get-ChildItem -Path $jtalkBld -Recurse -Filter "*.dll" |
                   Where-Object { $_.FullName -match "\\Release\\" }

    # jtalk.dll を優先検索、なければ jtalkdll.dll を探して jtalk.dll としてコピー
    $srcDll = $releaseDlls | Where-Object { $_.Name -eq "jtalk.dll" } | Select-Object -First 1
    if (-not $srcDll) {
        $srcDll = $releaseDlls | Where-Object { $_.Name -eq "jtalkdll.dll" } | Select-Object -First 1
    }
    if (-not $srcDll) {
        # フォールバック: 唯一の DLL があればそれを使う
        $srcDll = $releaseDlls | Select-Object -First 1
    }
    if (-not $srcDll) {
        Write-Fail "jtalk.dll のビルド結果が見つかりません。`n  $jtalkBld を確認してください。"
    }

    Copy-Item $srcDll.FullName $dllDest -Force
    Write-OK "jtalk.dll を配置: $dllDest"

    # --- 依存 DLL のコピー（VC++ ランタイム等）---
    $depDlls = $releaseDlls | Where-Object { $_.FullName -ne $srcDll.FullName }
    foreach ($dep in $depDlls) {
        $depDest = Join-Path $dataDir $dep.Name
        Copy-Item $dep.FullName $depDest -Force
        Write-OK "依存 DLL を配置: $($dep.Name)"
    }
}

# ============================================================================
# [2] MeCab UTF-8 辞書のダウンロード・配置
# ============================================================================

Write-Step "2/5 MeCab UTF-8 辞書"

if (Test-Path $dicDir) {
    $dicFileCount = (Get-ChildItem -Path $dicDir -File).Count
    Write-OK "辞書ディレクトリは既に存在します（$dicFileCount ファイル）（スキップ）"
    Write-Info "  $dicDir"
} else {
    New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null

    $dicTar  = Join-Path $tmpDir "open_jtalk_dic_utf_8-1.11.tar.gz"
    $dicWork = Join-Path $tmpDir "open_jtalk_dic_utf_8-1.11"

    Invoke-Download $dicUrl $dicTar

    if (-not (Test-Path $dicWork)) {
        Write-Info "展開中 ..."
        Push-Location $tmpDir
        try {
            Invoke-Native { tar -xzf $dicTar } "辞書の展開に失敗しました（tar）"
        } finally {
            Pop-Location
        }
    }

    if (-not (Test-Path $dicWork)) {
        Write-Fail "辞書の展開に失敗しました。手動で $dicTar を $tmpDir に展開してください。"
    }

    New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
    Move-Item $dicWork $dicDir
    $dicFileCount = (Get-ChildItem -Path $dicDir -File).Count
    Write-OK "辞書を配置: $dicDir ($dicFileCount ファイル)"
}

# ============================================================================
# [3] Mei 音声モデルのダウンロード・配置
# ============================================================================

Write-Step "3/5 Mei 音声モデル"

$meiVoice = Join-Path $voiceDir "mei_normal.htsvoice"

if (Test-Path $meiVoice) {
    Write-OK "mei_normal.htsvoice は既に存在します（スキップ）"
    Write-Info "  $meiVoice"
} else {
    New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null

    $mmdZip     = Join-Path $tmpDir "MMDAgent_Example-1.8.zip"
    $mmdWork    = Join-Path $tmpDir "MMDAgent_Example-1.8"

    Invoke-Download $mmdUrl $mmdZip

    if (-not (Test-Path $mmdWork)) {
        Write-Info "展開中（ZIP 約 30 MB）..."
        Expand-Archive -Path $mmdZip -DestinationPath $tmpDir
    }

    # Voice ディレクトリを探す（フォルダ構成が変わった場合を考慮）
    $htsVoices = Get-ChildItem -Path $mmdWork -Recurse -Filter "*.htsvoice" -ErrorAction SilentlyContinue
    if (-not $htsVoices) {
        Write-Fail ".htsvoice ファイルが見つかりません: $mmdWork`n  ZIP の展開内容を確認してください。"
    }

    New-Item -ItemType Directory -Force -Path $voiceDir | Out-Null
    foreach ($v in $htsVoices) {
        Copy-Item $v.FullName (Join-Path $voiceDir $v.Name) -Force
        Write-OK "音声モデルを配置: $($v.Name)"
    }
}

# ============================================================================
# [4] PoC ビルド
# ============================================================================

Write-Step "4/5 PoC ビルド（Release / x64）"

if (-not (Test-Path $csproj)) {
    Write-Fail "OpenJTalkPoC.csproj が見つかりません: $csproj`n  このスクリプトは poc\OpenJTalkPoC\ に置いてください。"
}

# jtalk.dll の存在を最終確認（Condition="Exists(...)" がビルド時に参照される）
if (-not (Test-Path $dllDest)) {
    Write-Warn "jtalk.dll が $dataDir に見つかりません。ビルドはされますが DLL が欠如します。"
}

Invoke-Native { dotnet build $csproj -c Release } "dotnet build に失敗しました"
Write-OK "ビルド完了"

# ============================================================================
# [5] PoC 実行
# ============================================================================

Write-Step "5/5 PoC 実行"

Invoke-Native { dotnet run --project $csproj -c Release --no-build } "PoC の実行に失敗しました"

# ============================================================================
# 完了メッセージ
# ============================================================================

Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "セットアップ・実行が完了しました。" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host ""

$outDir = Join-Path $pocDir "bin\Release\net8.0-windows"
Write-Host "生成された WAV ファイルを再生して音声品質を確認してください。"
Write-Host "  出力先: $outDir"
Write-Host ""
Write-Host "WAV を再生するには:"
Write-Host "  Invoke-Item `"$outDir\output_短文テスト.wav`""
Write-Host ""
