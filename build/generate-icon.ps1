#Requires -Version 5.1
<#
    Generates app.ico for Markdown Viewer.

    Design: rounded-square gradient tile (indigo #6366F1 -> blue #3B82F6)
    with the classic Markdown "M" + downward arrow in white.

    Multi-resolution .ico with PNG-encoded entries (Windows Vista+),
    sizes: 16, 24, 32, 48, 64, 128, 256.

    Flat inline code (no function wrappers) to avoid PowerShell's silent
    exception swallowing inside functions that return GDI+ objects.
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$OutPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = @()

foreach ($px in $sizes) {
    # High-DPI source render at 4x then downscale for crisp small sizes.
    $scale = 4
    $srcSize = $px * $scale
    $src = [System.Drawing.Bitmap]::new($srcSize, $srcSize)
    $src.SetResolution(96.0 * $scale, 96.0 * $scale)
    $g = [System.Drawing.Graphics]::FromImage($src)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $u  = [float]$scale          # source-space unit multiplier
    $sz = [float]$srcSize        # full source size

    # ---- rounded-square gradient background ----------------------------
    $radius = 0.22 * $sz
    $rect = [System.Drawing.RectangleF]::new(0.0, 0.0, $sz, $sz)
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $r = $radius
    $path.AddArc($rect.X, $rect.Y, $r, $r, 180, 90)
    $path.AddArc($rect.Right - $r, $rect.Y, $r, $r, 270, 90)
    $path.AddArc($rect.Right - $r, $rect.Bottom - $r, $r, $r, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $r, $r, $r, 90, 90)
    $path.CloseFigure()

    $c1 = [System.Drawing.ColorTranslator]::FromHtml('#6366F1')
    $c2 = [System.Drawing.ColorTranslator]::FromHtml('#3B82F6')
    $fwd = [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($rect, $c1, $c2, $fwd)
    $g.FillPath($brush, $path)
    $brush.Dispose()
    $path.Dispose()

    # subtle top sheen
    $sheenRect = [System.Drawing.RectangleF]::new(0.0, 0.0, $sz, $sz * 0.5)
    $sc1 = [System.Drawing.Color]::FromArgb(40, 255, 255, 255)
    $sc2 = [System.Drawing.Color]::FromArgb(0,  255, 255, 255)
    $vert = [System.Drawing.Drawing2D.LinearGradientMode]::Vertical
    $sheen = [System.Drawing.Drawing2D.LinearGradientBrush]::new($sheenRect, $sc1, $sc2, $vert)
    $g.FillRectangle($sheen, $sheenRect)
    $sheen.Dispose()

    # ---- white "M" + down arrow (Markdown logo style) -----------------
    $white = [System.Drawing.Color]::White
    $penW  = [float](0.14 * $sz)
    $wPen  = [System.Drawing.Pen]::new($white, $penW)
    $wPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $wPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $wPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $cx      = $sz / 2
    $mTop    = 0.26 * $sz
    $mBottom = 0.58 * $sz
    $mHalfW  = 0.17 * $sz
    $mDip    = 0.10 * $sz

    $g.DrawLine($wPen, ($cx - $mHalfW), $mTop, ($cx - $mHalfW), $mBottom)
    $g.DrawLine($wPen, ($cx - $mHalfW), $mTop,  $cx, ($mTop + $mDip))
    $g.DrawLine($wPen, ($cx + $mHalfW), $mTop, ($cx + $mHalfW), $mBottom)
    $g.DrawLine($wPen, ($cx + $mHalfW), $mTop,  $cx, ($mTop + $mDip))

    $aTop    = 0.62 * $sz
    $aBottom = 0.78 * $sz
    $aHalfW  = 0.16 * $sz
    $aChev   = 0.12 * $sz

    $g.DrawLine($wPen, $cx, $aTop, $cx, $aBottom)
    $g.DrawLine($wPen, $cx, $aBottom, ($cx - $aHalfW), ($aBottom - $aChev))
    $g.DrawLine($wPen, $cx, $aBottom, ($cx + $aHalfW), ($aBottom - $aChev))

    $wPen.Dispose()
    $g.Dispose()

    # downscale to target size
    $out = [System.Drawing.Bitmap]::new($px, $px)
    $out.SetResolution(96.0, 96.0)
    $og = [System.Drawing.Graphics]::FromImage($out)
    $og.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $og.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $og.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $og.DrawImage($src, 0, 0, $px, $px)
    $og.Dispose()
    $src.Dispose()

    # encode as PNG
    $ms = [System.IO.MemoryStream]::new()
    $out.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $png = $ms.ToArray()
    $ms.Dispose()
    $out.Dispose()

    Write-Host ("  {0,4}px -> {1,6} bytes" -f $px, $png.Length)
    $images += [pscustomobject]@{ Size = $px; Bytes = $png }
}

# ---------------------------------------------------------------- build ICO
# ICONDIR: reserved(2)=0, type(2)=1, count(2)  => 6 bytes
# ICONDIRENTRY: 16 bytes each, then PNG blobs.
$dataOffset = 6 + 16 * $images.Count

$header = [System.IO.MemoryStream]::new()
$bw     = [System.IO.BinaryWriter]::new($header)
$bw.Write([uint16]0)            # reserved
$bw.Write([uint16]1)            # type = icon
$bw.Write([uint16]$images.Count)

$entries    = [System.IO.MemoryStream]::new()
$bw2        = [System.IO.BinaryWriter]::new($entries)
$dataStream = [System.IO.MemoryStream]::new()
$bwData     = [System.IO.BinaryWriter]::new($dataStream)

foreach ($img in $images) {
    $w = if ($img.Size -ge 256) { 0 } else { $img.Size }
    $bw2.Write([byte]$w)                  # width (0 = 256)
    $bw2.Write([byte]$w)                  # height
    $bw2.Write([byte]0)                   # palette
    $bw2.Write([byte]0)                   # reserved
    $bw2.Write([uint16]1)                 # planes
    $bw2.Write([uint16]32)                # bpp
    $bw2.Write([uint32]$img.Bytes.Length) # size
    $bw2.Write([uint32]$dataOffset)       # offset
    $bwData.Write($img.Bytes)
    $dataOffset += $img.Bytes.Length
}

$outDir = Split-Path -Parent $OutPath
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$fs = [System.IO.File]::Create($OutPath)
try {
    $fs.Write($header.ToArray(),     0, [int]$header.Length)
    $fs.Write($entries.ToArray(),    0, [int]$entries.Length)
    $fs.Write($dataStream.ToArray(), 0, [int]$dataStream.Length)
} finally {
    $fs.Dispose()
    $header.Dispose()
    $entries.Dispose()
    $dataStream.Dispose()
}

$fi = Get-Item $OutPath
Write-Host ("Wrote {0} ({1:N0} bytes) with {2} sizes: {3}" -f `
    $fi.FullName, $fi.Length, $images.Count, ($sizes -join ', '))