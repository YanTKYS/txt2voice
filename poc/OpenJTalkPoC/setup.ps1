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
      - Visual Studio 2019 または 2022（C++ によるデスクトップ開発 ワークロード）

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
$tmpDir    = Join-Path $env:TEMP "openjtalk-poc-setup"

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

# CMake
if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
    Write-Fail "cmake が見つかりません。CMake 3.15 以上をインストールしてください: https://cmake.org/"
}
Write-OK "cmake $(cmake --version | Select-Object -First 1)"

# Visual Studio（vswhere 経由）
$vsWhere = Get-VsWhere
if (-not $vsWhere) {
    Write-Fail "Visual Studio インストーラが見つかりません。`n  Visual Studio 2019/2022 の「C++ によるデスクトップ開発」ワークロードをインストールしてください。"
}
$vsInstallPath = & $vsWhere -latest -property installationPath
$vsVersion     = & $vsWhere -latest -property catalog_productLineVersion
if (-not $vsInstallPath) {
    Write-Fail "Visual Studio が検出できませんでした。`n  C++ ワークロード付きで Visual Studio 2019/2022 をインストールしてください。"
}
Write-OK "Visual Studio $vsVersion: $vsInstallPath"

# CMake ジェネレータ選択
$cmakeGen = if ([int]$vsVersion -ge 2022) { "Visual Studio 17 2022" } else { "Visual Studio 16 2019" }
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
        git clone --recursive $jtalkRepo $jtalkSrc
        Write-OK "クローン完了"
    }

    # --- CMake 設定 ---
    New-Item -ItemType Directory -Force -Path $jtalkBld | Out-Null
    Write-Info "CMake 設定中 ..."
    Push-Location $jtalkBld
    try {
        cmake .. -G $cmakeGen -A x64
    } finally {
        Pop-Location
    }

    # --- ビルド ---
    Write-Info "ビルド中（Release / x64）..."
    cmake --build $jtalkBld --config Release

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
            tar -xzf $dicTar
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

dotnet build $csproj -c Release
Write-OK "ビルド完了"

# ============================================================================
# [5] PoC 実行
# ============================================================================

Write-Step "5/5 PoC 実行"

dotnet run --project $csproj -c Release --no-build

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
