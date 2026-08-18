Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([System.Drawing.Color]::FromArgb(8,10,13))

# Outer black/gold badge.
$outer = New-Object System.Drawing.Rectangle(8,8,240,240)
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$r = 44; $d = $r * 2
$path.AddArc($outer.X,$outer.Y,$d,$d,180,90)
$path.AddArc($outer.Right-$d,$outer.Y,$d,$d,270,90)
$path.AddArc($outer.Right-$d,$outer.Bottom-$d,$d,$d,0,90)
$path.AddArc($outer.X,$outer.Bottom-$d,$d,$d,90,90)
$path.CloseFigure()
$bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(16,19,24))
$g.FillPath($bgBrush,$path)
$goldPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(247,194,55),6)
$g.DrawPath($goldPen,$path)

# Stylized gold ingot.
$ingot = New-Object System.Drawing.Rectangle(55,42,146,118)
$grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($ingot,[System.Drawing.Color]::FromArgb(255,226,119),[System.Drawing.Color]::FromArgb(190,132,23),45)
$ingotPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$ir=24;$id=$ir*2
$ingotPath.AddArc($ingot.X,$ingot.Y,$id,$id,180,90)
$ingotPath.AddArc($ingot.Right-$id,$ingot.Y,$id,$id,270,90)
$ingotPath.AddArc($ingot.Right-$id,$ingot.Bottom-$id,$id,$id,0,90)
$ingotPath.AddArc($ingot.X,$ingot.Bottom-$id,$id,$id,90,90)
$ingotPath.CloseFigure()
$g.FillPath($grad,$ingotPath)
$ingotPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,238,153),4)
$g.DrawPath($ingotPen,$ingotPath)

$auFont = New-Object System.Drawing.Font('Segoe UI',44,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
$auBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(65,42,2))
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center
$g.DrawString('Au',$auFont,$auBrush,$ingot,$sf)

$titleFont = New-Object System.Drawing.Font('Segoe UI',25,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
$titleBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(247,211,112))
$titleRect = New-Object System.Drawing.RectangleF(20,176,216,48)
$g.DrawString('GOLD BAR',$titleFont,$titleBrush,$titleRect,$sf)

$g.Dispose(); $path.Dispose(); $ingotPath.Dispose(); $bgBrush.Dispose(); $goldPen.Dispose(); $grad.Dispose(); $ingotPen.Dispose(); $auFont.Dispose(); $auBrush.Dispose(); $titleFont.Dispose(); $titleBrush.Dispose(); $sf.Dispose()

$out = Join-Path $PSScriptRoot 'AppIcon.ico'
$hicon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hicon)
$fs = [System.IO.File]::Open($out,[System.IO.FileMode]::Create)
$icon.Save($fs)
$fs.Dispose(); $icon.Dispose(); $bmp.Dispose()
Write-Host "Generated $out"
