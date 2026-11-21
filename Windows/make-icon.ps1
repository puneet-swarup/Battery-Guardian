Add-Type -AssemblyName System.Drawing

$assetsDir = "Assets"
if (-not (Test-Path $assetsDir)) { New-Item -ItemType Directory -Path $assetsDir | Out-Null }

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# Battery body
$bodyWidth = 170; $bodyHeight = 110
$bodyX = 30; $bodyY = ($size - $bodyHeight) / 2
$corner = 16

$bodyPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$bodyPath.AddArc($bodyX, $bodyY, $corner, $corner, 180, 90)
$bodyPath.AddArc($bodyX + $bodyWidth - $corner, $bodyY, $corner, $corner, 270, 90)
$bodyPath.AddArc($bodyX + $bodyWidth - $corner, $bodyY + $bodyHeight - $corner, $corner, $corner, 0, 90)
$bodyPath.AddArc($bodyX, $bodyY + $bodyHeight - $corner, $corner, $corner, 90, 90)
$bodyPath.CloseFigure()

$shellPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 40, 40, 40), 12)
$g.DrawPath($shellPen, $bodyPath)

# Battery nub
$nubWidth = 18; $nubHeight = 44
$nubX = $bodyX + $bodyWidth + 4
$nubY = ($size - $nubHeight) / 2
$g.FillRectangle([System.Drawing.Brushes]::Black, $nubX, $nubY, $nubWidth, $nubHeight)

# Green fill
$pad = 20
$g.FillRectangle(
    (New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 60, 200, 90))),
    $bodyX + $pad, $bodyY + $pad, $bodyWidth - ($pad*2), $bodyHeight - ($pad*2)
)

$g.Dispose()

# Save PNG to memory
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $ms.ToArray()
$bmp.Dispose(); $ms.Dispose()

# Build ICO
$icoPath = Join-Path $assetsDir "app.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)   # ICONDIR
$bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0)  # entry
$bw.Write([uint16]1); $bw.Write([uint16]32)
$bw.Write([uint32]$pngBytes.Length); $bw.Write([uint32]22)
$bw.Write($pngBytes)

$bw.Close(); $fs.Close()

Write-Host "Wrote $icoPath ($($pngBytes.Length) bytes)"