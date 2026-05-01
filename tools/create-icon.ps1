#Requires -Version 5.1
<#
.SYNOPSIS
    声の広報アイコン (app.ico) を生成するスクリプト

.DESCRIPTION
    System.Drawing を使ってアプリアイコンを描画し、
    TxtToVoice/app.ico として出力します。
    生成後は csproj が自動的に参照します。

    デザイン:
      - 背景: アプリブルー (#2C5F8A) の角丸正方形
      - 前面: 白い「声」の漢字（Yu Gothic UI Bold）
      - サイズ: 16 / 32 / 48 / 256 px の 4 種を ICO にまとめる

.NOTES
    実行方法（プロジェクトルートから）:
      Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
      .\tools\create-icon.ps1

    出力: TxtToVoice\app.ico
    生成後: 'git add TxtToVoice/app.ico' してコミットすること。
#>

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

# ---- 出力パス ----------------------------------------------------------------
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { $PWD.Path }
$outPath   = Join-Path $scriptDir "..\TxtToVoice\app.ico"
$outPath   = [System.IO.Path]::GetFullPath($outPath)

# ---- アイコン描画 ------------------------------------------------------------

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)

    $g.SmoothingMode    = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    # --- 背景: 角丸正方形（アプリブルー） ---
    $blue   = [System.Drawing.Color]::FromArgb(255, 44, 95, 138)   # #2C5F8A
    $bgBrush = New-Object System.Drawing.SolidBrush($blue)
    $radius  = [int]([Math]::Max(2, $size * 0.18))
    $d       = $radius * 2

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0,          0,          $d, $d, 180, 90)
    $path.AddArc($size - $d, 0,          $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d, $d, $d, 0,   90)
    $path.AddArc(0,          $size - $d, $d, $d, 90,  90)
    $path.CloseFigure()
    $g.FillPath($bgBrush, $path)

    # --- 前面: 「声」を中央に描画 ---
    $fontSizePx = [float]($size * 0.60)
    # GraphicsUnit.Pixel を使い px 単位で指定する
    $font = New-Object System.Drawing.Font(
        "Yu Gothic UI",
        $fontSizePx,
        [System.Drawing.FontStyle]::Bold,
        [System.Drawing.GraphicsUnit]::Pixel)

    $whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $sf         = New-Object System.Drawing.StringFormat
    $sf.Alignment     = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center

    # わずかに上寄せ（視覚的中央に見せる）
    $offsetY = -([float]($size * 0.03))
    $rect    = [System.Drawing.RectangleF]::new(0, $offsetY, $size, $size)
    $g.DrawString("声", $font, $whiteBrush, $rect, $sf)

    # --- 32px 以上: 右下に小さな音波ドット 3 つ ---
    if ($size -ge 32) {
        $dotR  = [float]([Math]::Max(1.2, $size * 0.040))
        $baseX = [float]($size * 0.765)
        $baseY = [float]($size * 0.615)
        $gap   = [float]($size * 0.095)

        $dotBrush = New-Object System.Drawing.SolidBrush(
            [System.Drawing.Color]::FromArgb(200, 255, 255, 255))

        for ($i = 0; $i -lt 3; $i++) {
            $cx = $baseX + $gap * $i
            $cy = $baseY - $gap * $i * 0.3
            $g.FillEllipse($dotBrush, ($cx - $dotR), ($cy - $dotR), $dotR*2, $dotR*2)
        }
        $dotBrush.Dispose()
    }

    $font.Dispose()
    $whiteBrush.Dispose()
    $bgBrush.Dispose()
    $path.Dispose()
    $sf.Dispose()
    $g.Dispose()

    return $bmp
}

# ---- ICO ファイル構築 --------------------------------------------------------
# ICO 形式: ICONDIR(6B) + N×ICONDIRENTRY(16B) + N×画像データ(PNG)

$sizes = @(16, 32, 48, 256)
$pngDataList = [System.Collections.Generic.List[byte[]]]::new()

foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms  = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngDataList.Add($ms.ToArray())
    $bmp.Dispose()
    $ms.Dispose()
    Write-Host "  描画完了: ${s}x${s}" -ForegroundColor Gray
}

$outMs  = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($outMs)

# ICONDIR
$writer.Write([uint16]0)                     # Reserved
$writer.Write([uint16]1)                     # Type: ICO
$writer.Write([uint16]$sizes.Count)          # ImageCount

# 画像データ開始オフセット = 6(ICONDIR) + 16×count(ICONDIRENTRY)
$offset = [uint32](6 + 16 * $sizes.Count)

# ICONDIRENTRY × count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s    = $sizes[$i]
    $data = $pngDataList[$i]
    $w    = if ($s -ge 256) { [byte]0 } else { [byte]$s }
    $h    = if ($s -ge 256) { [byte]0 } else { [byte]$s }
    $writer.Write($w)                         # Width  (0=256)
    $writer.Write($h)                         # Height (0=256)
    $writer.Write([byte]0)                    # ColorCount
    $writer.Write([byte]0)                    # Reserved
    $writer.Write([uint16]1)                  # Planes
    $writer.Write([uint16]32)                 # BitCount
    $writer.Write([uint32]$data.Length)       # BytesInRes
    $writer.Write($offset)                    # ImageOffset
    $offset += [uint32]$data.Length
}

# 画像データ本体
foreach ($data in $pngDataList) {
    $writer.Write($data)
}

$writer.Flush()
$icoBytes = $outMs.ToArray()
$writer.Dispose()
$outMs.Dispose()

[System.IO.File]::WriteAllBytes($outPath, $icoBytes)

Write-Host ""
Write-Host "[OK] アイコンを生成しました: $outPath" -ForegroundColor Green
Write-Host "     サイズ: $([math]::Round($icoBytes.Length / 1KB, 1)) KB  ($($sizes -join ', ') px)" -ForegroundColor Gray
Write-Host ""
Write-Host "次のステップ:" -ForegroundColor Cyan
Write-Host "  git add TxtToVoice/app.ico"
Write-Host "  git commit -m 'feat: アプリアイコンを追加'"
