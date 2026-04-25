#Requires -Version 5.1
<#
.SYNOPSIS
    テキスト読み上げツール — OpenJTalk エンジン セットアップスクリプト

.DESCRIPTION
    OpenJTalk エンジンの動作に必要な以下のファイルを自動取得・配置します。
      [1] jtalk.dll    — GitHub (jtalkdll) からビルド
      [2] MeCab 辞書  — jtalkdll ビルド成果物から取得
      [3] Mei 音声モデル — MMDAgent (SourceForge) からダウンロード

    このスクリプトは TxtToVoice.exe と同じフォルダに置いて実行してください。

.PARAMETER VerifyOnly
    セットアップを実行せず、現在のファイル配置状態を [OK]/[NG] で確認して終了します。
    終了コード: 0 = 全コンポーネント OK、1 = 不足あり

.NOTES
    前提ツール:
      - Git
      - CMake 3.15 以上
      - MSVC C++ コンパイラ（Visual Studio Build Tools 2019/2022 以上）
          インストーラ: https://visualstudio.microsoft.com/visual-cpp-build-tools/
          ワークロード: 「C++ によるデスクトップ開発」にチェック

    ライセンス（使用コンポーネント）:
      jtalkdll       : MIT License
      Open JTalk     : Modified BSD License
      HTS Voice "Mei": Creative Commons Attribution 3.0（クレジット表示必須）
                        © 2009-2015 名古屋工業大学

    クレジット表示義務のため THIRD_PARTY_LICENSES.txt を必ず同梱してください。

.EXAMPLE
    Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
    .\setup_openjtalk.ps1

.EXAMPLE
    .\setup_openjtalk.ps1 -VerifyOnly
    # ファイルが揃っているか確認だけしたい場合
#>
param(
    [switch]$VerifyOnly
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# ---- パス定義 ----------------------------------------------------------------

# スクリプトの場所 = TxtToVoice.exe の場所
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { $PWD.Path }
$dataDir   = Join-Path $scriptDir "Data\openjtalk"
$dicDir    = Join-Path $dataDir "open_jtalk_dic_utf_8"
$voiceDir  = Join-Path $dataDir "voice"
$dllDest   = Join-Path $dataDir "jtalk.dll"
$tmpDir    = Join-Path $env:USERPROFILE "openjtalk-setup"

$jtalkRepo = "https://github.com/rosmarinus/jtalkdll.git"
$mmdUrl    = "https://downloads.sourceforge.net/project/mmdagent/MMDAgent_Example/MMDAgent_Example-1.8/MMDAgent_Example-1.8.zip"

# ---- -VerifyOnly モード -------------------------------------------------------

if ($VerifyOnly) {
    $dllOk   = Test-Path $dllDest
    $dicOk   = Test-Path (Join-Path $dicDir "sys.dic")
    $htsFiles = @(Get-ChildItem $voiceDir -Filter "*.htsvoice" -Recurse -ErrorAction SilentlyContinue)
    $voiceOk  = $htsFiles.Count -gt 0

    function Write-Check([bool]$ok, [string]$label, [string]$path) {
        $mark  = if ($ok) { "[OK]" } else { "[NG]" }
        $color = if ($ok) { "Green" } else { "Red" }
        Write-Host "$mark  $label" -ForegroundColor $color
        Write-Host "       $path"  -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "==[ OpenJTalk セットアップ状態確認 ]" -ForegroundColor Cyan
    Write-Host ""
    Write-Check $dllOk   "jtalk.dll"                          $dllDest
    Write-Check $dicOk   "MeCab 辞書 (open_jtalk_dic_utf_8)" $dicDir
    Write-Check $voiceOk "音声モデル (.htsvoice)"             $voiceDir
    Write-Host ""

    if ($dllOk -and $dicOk -and $voiceOk) {
        Write-Host "すべてのコンポーネントが揃っています。OpenJTalk は使用可能です。" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "不足コンポーネントがあります。以下を実行してセットアップしてください:" -ForegroundColor Yellow
        Write-Host "  .\setup_openjtalk.ps1" -ForegroundColor White
        exit 1
    }
}

# ---- ユーティリティ関数 ------------------------------------------------------

function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host "==[ $msg ]" -ForegroundColor Cyan
}
function Write-OK([string]$msg)   { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Info([string]$msg) { Write-Host "  [..] $msg" -ForegroundColor Gray }
function Write-Warn([string]$msg) { Write-Host "  [!!] $msg" -ForegroundColor Yellow }
function Write-Fail([string]$msg) { Write-Host "  [NG] $msg" -ForegroundColor Red; exit 1 }

function Test-ArchiveFile([string]$path) {
    if (-not (Test-Path $path)) { return $false }
    $b = [System.IO.File]::ReadAllBytes($path)
    if ($b.Length -lt 2) { return $false }
    return ($b[0] -eq 0x1F -and $b[1] -eq 0x8B) -or ($b[0] -eq 0x50 -and $b[1] -eq 0x4B)
}

function Get-VsWhere {
    $paths = @(
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\Installer\vswhere.exe"
    )
    foreach ($p in $paths) { if (Test-Path $p) { return $p } }
    return $null
}

function Find-Cmake {
    $cmd = Get-Command cmake -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $candidates = @(
        "$env:ProgramFiles\CMake\bin\cmake.exe",
        "${env:ProgramFiles(x86)}\CMake\bin\cmake.exe"
    )
    $vsw = Get-VsWhere
    if ($vsw) {
        $roots = & $vsw -products * -all -property installationPath 2>$null
        foreach ($root in @($roots)) {
            $candidates += Join-Path $root "Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
        }
    }
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    return $null
}

function Invoke-Native {
    param([Parameter(Mandatory)][scriptblock]$ScriptBlock, [string]$ErrorMessage = "コマンドが失敗しました")
    $local:ErrorActionPreference = "Continue"
    & $ScriptBlock
    if ($LASTEXITCODE -ne 0) { Write-Fail "$ErrorMessage (exit code: $LASTEXITCODE)" }
}

# ============================================================================
# [0] 前提ツール確認
# ============================================================================

Write-Step "0/3 前提ツール確認"

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Fail "git が見つかりません。https://git-scm.com/ からインストールしてください。"
}
Write-OK "git $(git --version)"

$cmake = Find-Cmake
if (-not $cmake) {
    Write-Fail "cmake が見つかりません。https://cmake.org/download/ からインストールしてください。"
}
Write-OK "$(& $cmake --version | Select-Object -First 1)"

$vsWhere = Get-VsWhere
if (-not $vsWhere) {
    Write-Fail @"
Visual Studio Build Tools が見つかりません。
https://visualstudio.microsoft.com/visual-cpp-build-tools/ からインストールし、
「C++ によるデスクトップ開発」ワークロードを追加してください。
"@
}
$vsInstallVersion = & $vsWhere -products * -latest -property installationVersion
$vsMajor = [int](($vsInstallVersion -split '\.')[0])
Write-OK "MSVC $vsInstallVersion"

$savedEap = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$cmakeHelp = (& $cmake --help 2>&1) -join "`n"
$ErrorActionPreference = $savedEap
if ($cmakeHelp -match "Visual Studio $vsMajor (\d{4})") {
    $cmakeGen = "Visual Studio $vsMajor $($matches[1])"
} else {
    Write-Fail "cmake に対応する Visual Studio ジェネレータが見つかりません。"
}
Write-Info "CMake ジェネレータ: $cmakeGen"

# ============================================================================
# [1] jtalk.dll のビルドと辞書取得
# ============================================================================

Write-Step "1/3 jtalk.dll・辞書のビルド"

if (Test-Path $dllDest) {
    Write-OK "jtalk.dll は既に存在します（スキップ）"
} else {
    $jtalkSrc = Join-Path $tmpDir "jtalkdll"
    $jtalkBld = Join-Path $jtalkSrc "build"

    New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null
    New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

    if (Test-Path (Join-Path $jtalkSrc ".git")) {
        Write-OK "既存クローンを使用: $jtalkSrc"
    } else {
        Write-Info "jtalkdll をクローン中 ..."
        Invoke-Native { git clone --recursive $jtalkRepo $jtalkSrc } "git clone に失敗しました"
        Write-OK "クローン完了"
    }

    # VS 2026 / MSVC 19.5x 互換パッチ（std::binary_function 除去）
    $mecabSrcDir = Join-Path $jtalkSrc "open_jtalk-1.11\mecab\src"
    foreach ($fname in @("dictionary.cpp")) {
        $fpath = Join-Path $mecabSrcDir $fname
        if (-not (Test-Path $fpath)) { continue }
        $original = [System.IO.File]::ReadAllText($fpath)
        $patched = $original -replace ':\s*public\s+std::binary_function<[^{]+>', ''
        if ($original -ne $patched) {
            [System.IO.File]::WriteAllText($fpath, $patched)
            Write-OK "パッチ適用: $fname"
        }
    }

    if (Test-Path $jtalkBld) { Remove-Item -Recurse -Force $jtalkBld; Start-Sleep -Milliseconds 500 }
    New-Item -ItemType Directory -Force -Path "$jtalkBld\CMakeFiles\pkgRedirects" | Out-Null
    Write-Info "CMake 設定中 ..."
    $policyArg = '-DCMAKE_POLICY_VERSION_MINIMUM=3.5'
    Invoke-Native { & $cmake -S $jtalkSrc -B $jtalkBld -G $cmakeGen -A x64 $policyArg } "CMake の設定に失敗しました"

    Write-Info "ビルド中（Release / x64）..."
    Invoke-Native { & $cmake --build $jtalkBld --config Release } "jtalk.dll のビルドに失敗しました"

    $releaseDlls = Get-ChildItem -Path $jtalkBld -Recurse -Filter "*.dll" |
                   Where-Object { $_.FullName -match "\\Release\\" }
    $srcDll = $releaseDlls | Where-Object { $_.Name -eq "jtalk.dll" } | Select-Object -First 1
    if (-not $srcDll) { $srcDll = $releaseDlls | Where-Object { $_.Name -eq "jtalkdll.dll" } | Select-Object -First 1 }
    if (-not $srcDll) { $srcDll = $releaseDlls | Select-Object -First 1 }
    if (-not $srcDll) { Write-Fail "jtalk.dll のビルド結果が見つかりません。$jtalkBld を確認してください。" }

    Copy-Item $srcDll.FullName $dllDest -Force
    Write-OK "jtalk.dll を配置: $dllDest"

    $depDlls = $releaseDlls | Where-Object { $_.FullName -ne $srcDll.FullName }
    foreach ($dep in $depDlls) {
        Copy-Item $dep.FullName (Join-Path $dataDir $dep.Name) -Force
        Write-OK "依存 DLL を配置: $($dep.Name)"
    }

    # jtalkdll ビルド済み辞書を使用
    $jtalkDicBuilt = Join-Path $jtalkBld "dic"
    if (Test-Path (Join-Path $jtalkDicBuilt "sys.dic")) {
        if (-not (Test-Path $dicDir)) {
            Copy-Item -Path $jtalkDicBuilt -Destination $dicDir -Recurse -Force
            Write-OK "辞書を配置（jtalkdll ビルドから）: $dicDir"
        }
    }
}

if (-not (Test-Path $dicDir)) {
    Write-Fail "MeCab 辞書が見つかりません: $dicDir`n  jtalkdll のビルドが完了しているか確認してください。"
}
Write-OK "辞書確認OK: $dicDir"

# ============================================================================
# [2] Mei 音声モデルのダウンロード・配置
# ============================================================================

Write-Step "2/3 Mei 音声モデル"

$meiVoice = Join-Path $voiceDir "mei_normal.htsvoice"

if (Test-Path $meiVoice) {
    Write-OK "mei_normal.htsvoice は既に存在します（スキップ）"
} else {
    New-Item -ItemType Directory -Force -Path $voiceDir | Out-Null

    # ── 同梱ファイル優先（SourceForge ダウンロード不要）──────────────────────
    # 配布 ZIP に bundled\mei_normal.htsvoice が含まれていればそれを使用する
    $bundledVoice = Join-Path $scriptDir "bundled\mei_normal.htsvoice"
    if (Test-Path $bundledVoice) {
        Copy-Item $bundledVoice $meiVoice -Force
        Write-OK "同梱ファイルから配置: mei_normal.htsvoice"
    } else {

    $mmdZip  = Join-Path $tmpDir "MMDAgent_Example-1.8.zip"
    $mmdWork = Join-Path $tmpDir "MMDAgent_Example-1.8"

    if (-not (Test-ArchiveFile $mmdZip)) {
        if (Test-Path $mmdZip) { Remove-Item $mmdZip -Force }
        Write-Info "ダウンロード試行中: MMDAgent_Example-1.8.zip ..."
        $dlOk = $false
        try {
            $headers = @{ 'User-Agent' = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36' }
            Invoke-WebRequest -Uri $mmdUrl -OutFile $mmdZip -UseBasicParsing -MaximumRedirection 10 -Headers $headers
            $dlOk = Test-ArchiveFile $mmdZip
        } catch { }

        if (-not $dlOk) {
            if (Test-Path $mmdZip) { Remove-Item $mmdZip -Force }
            Write-Host ""
            Write-Host "  [!!] SourceForge が Cloudflare JS チャレンジで保護されており自動ダウンロードできません。" -ForegroundColor Yellow
            Write-Host "       以下の手順で手動ダウンロードして再実行してください:" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "  [手順 A] ZIP をダウンロードして配置する場合:" -ForegroundColor Cyan
            Write-Host "    1. ブラウザで以下を開いてダウンロード:" -ForegroundColor White
            Write-Host "       $mmdUrl" -ForegroundColor White
            Write-Host "    2. ダウンロードした ZIP を以下のパスに配置:" -ForegroundColor White
            Write-Host "       $mmdZip" -ForegroundColor White
            Write-Host "    3. このスクリプトを再実行" -ForegroundColor White
            Write-Host ""
            Write-Host "  [手順 B] htsvoice を直接配置する場合:" -ForegroundColor Cyan
            Write-Host "    1. ZIP から Voice\mei\mei_normal.htsvoice を取り出す" -ForegroundColor White
            Write-Host "    2. 以下のパスに配置:" -ForegroundColor White
            Write-Host "       $meiVoice" -ForegroundColor White
            Write-Host "    3. このスクリプトを再実行" -ForegroundColor White
            Write-Host ""
            exit 1
        }
        Write-OK "ダウンロード完了: MMDAgent_Example-1.8.zip"
    } else {
        Write-OK "既存 ZIP を使用: $mmdZip"
    }

    if (-not (Test-Path $mmdWork)) {
        Write-Info "展開中（ZIP 約 200 MB）..."
        Expand-Archive -Path $mmdZip -DestinationPath $tmpDir
    }

    $htsVoices = Get-ChildItem -Path $mmdWork -Recurse -Filter "*.htsvoice" -ErrorAction SilentlyContinue
    if (-not $htsVoices) {
        Write-Fail ".htsvoice ファイルが見つかりません: $mmdWork"
    }
    foreach ($v in $htsVoices) {
        Copy-Item $v.FullName (Join-Path $voiceDir $v.Name) -Force
        Write-OK "音声モデルを配置: $($v.Name)"
    }

    } # end: 同梱ファイルがない場合の else ブロック
}

# ============================================================================
# 完了
# ============================================================================

Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "セットアップ完了！" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host ""
Write-Host "配置されたファイル:"
Write-Host "  $dllDest"
Write-Host "  $dicDir"
Write-Host "  $voiceDir"
Write-Host ""
Write-Host "TxtToVoice.exe を起動し、「設定」→「音声エンジン」で"
Write-Host "「OpenJTalk」を選択して再起動してください。"
Write-Host ""
Write-Host "[ライセンス] HTS Voice ""Mei"" は CC BY 3.0 ライセンスです。"
Write-Host "  © 2009-2015 名古屋工業大学"
Write-Host "  詳細: THIRD_PARTY_LICENSES.txt"
Write-Host ""
