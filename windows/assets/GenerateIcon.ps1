Add-Type -AssemblyName System.Drawing

$projectDir = Join-Path $PSScriptRoot '..\GoldBar.Windows'
$icoPath = Join-Path $projectDir 'appicon.ico'
$pngPath = Join-Path $projectDir 'appicon-preview.png'

$bmp = New-Object System.Drawing.Bitmap 256,256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([System.Drawing.Color]::FromArgb(8,10,14))

$outer = New-Object System.Drawing.RectangleF 10,10,236,236
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$r = 42.0
$d = $r * 2
$path.AddArc($outer.X,$outer.Y,$d,$d,180,90)
$path.AddArc($outer.Right-$d,$outer.Y,$d,$d,270,90)
$path.AddArc($outer.Right-$d,$outer.Bottom-$d,$d,$d,0,90)
$path.AddArc($outer.X,$outer.Bottom-$d,$d,$d,90,90)
$path.CloseFigure()
$borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(177,133,42)),3
$g.DrawPath($borderPen,$path)

# stylized gold ingot
$poly = [System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF 77,67),
    (New-Object System.Drawing.PointF 174,67),
    (New-Object System.Drawing.PointF 198,175),
    (New-Object System.Drawing.PointF 58,175)
)
$gradRect = New-Object System.Drawing.RectangleF 56,62,145,118
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $gradRect,([System.Drawing.Color]::FromArgb(255,221,105)),([System.Drawing.Color]::FromArgb(177,112,17)),35
$g.FillPolygon($brush,$poly)
$edge = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255,242,171)),4
$g.DrawPolygon($edge,$poly)

$font = New-Object System.Drawing.Font 'Segoe UI',42,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center
$textRect = New-Object System.Drawing.RectangleF 60,78,134,78
$textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(92,55,8))
$g.DrawString('Au',$font,$textBrush,$textRect,$sf)

$accent = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(247,211,112)),6
$g.DrawLine($accent,62,213,194,213)

$bmp.Save($pngPath,[System.Drawing.Imaging.ImageFormat]::Png)
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$fs = [System.IO.File]::Open($icoPath,[System.IO.FileMode]::Create)
$icon.Save($fs)
$fs.Close()

$icon.Dispose(); $bmp.Dispose(); $g.Dispose(); $path.Dispose(); $borderPen.Dispose(); $brush.Dispose(); $edge.Dispose(); $font.Dispose(); $sf.Dispose(); $textBrush.Dispose(); $accent.Dispose()
Write-Host "Generated $icoPath"
