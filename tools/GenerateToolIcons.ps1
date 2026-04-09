param(
    [string]$OutDir = "$(Split-Path -Parent $PSScriptRoot)\src\ProjectSPlus.App\Assets\ToolIcons"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

[System.IO.Directory]::CreateDirectory($OutDir) | Out-Null

$size = 64
$outlineColor = [System.Drawing.Color]::FromArgb(235, 10, 11, 14)
$strokeColor = [System.Drawing.Color]::FromArgb(250, 240, 235, 222)
$accentColor = [System.Drawing.Color]::FromArgb(245, 110, 165, 118)
$softAccentColor = [System.Drawing.Color]::FromArgb(235, 187, 146, 98)

function New-Pen([System.Drawing.Color]$color, [float]$width) {
    $pen = New-Object System.Drawing.Pen($color, $width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function Stroke-Path($graphics, $path, [float]$outlineWidth, [float]$strokeWidth) {
    $outline = New-Pen $outlineColor $outlineWidth
    $stroke = New-Pen $strokeColor $strokeWidth
    try {
        $graphics.DrawPath($outline, $path)
        $graphics.DrawPath($stroke, $path)
    }
    finally {
        $outline.Dispose()
        $stroke.Dispose()
    }
}

function Stroke-Line($graphics, [float]$x1, [float]$y1, [float]$x2, [float]$y2, [float]$outlineWidth, [float]$strokeWidth, [System.Drawing.Color]$strokeColorValue = $strokeColor) {
    $outline = New-Pen $outlineColor $outlineWidth
    $stroke = New-Pen $strokeColorValue $strokeWidth
    try {
        $graphics.DrawLine($outline, $x1, $y1, $x2, $y2)
        $graphics.DrawLine($stroke, $x1, $y1, $x2, $y2)
    }
    finally {
        $outline.Dispose()
        $stroke.Dispose()
    }
}

function Fill-And-StrokePolygon($graphics, [System.Drawing.PointF[]]$points, [System.Drawing.Color]$fillColor) {
    $brush = New-Object System.Drawing.SolidBrush($fillColor)
    $outline = New-Pen $outlineColor 6
    $stroke = New-Pen $strokeColor 3
    try {
        $graphics.FillPolygon($brush, $points)
        $graphics.DrawPolygon($outline, $points)
        $graphics.DrawPolygon($stroke, $points)
    }
    finally {
        $brush.Dispose()
        $outline.Dispose()
        $stroke.Dispose()
    }
}

function New-Icon($name, [scriptblock]$drawAction) {
    $path = Join-Path $OutDir "$name.png"
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)
        & $drawAction $graphics
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

New-Icon "select" {
    param($g)
    $rect = New-Object System.Drawing.RectangleF(13, 13, 30, 30)
    $outline = New-Pen $outlineColor 6
    $stroke = New-Pen $strokeColor 3
    $outline.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    $stroke.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    try {
        $g.DrawRectangle($outline, $rect.X, $rect.Y, $rect.Width, $rect.Height)
        $g.DrawRectangle($stroke, $rect.X, $rect.Y, $rect.Width, $rect.Height)
    }
    finally {
        $outline.Dispose()
        $stroke.Dispose()
    }

    $cursor = New-Object System.Drawing.Drawing2D.GraphicsPath
    $cursor.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(38, 34),
        [System.Drawing.PointF]::new(49, 48),
        [System.Drawing.PointF]::new(43, 49),
        [System.Drawing.PointF]::new(40, 56),
        [System.Drawing.PointF]::new(36, 54),
        [System.Drawing.PointF]::new(39, 47),
        [System.Drawing.PointF]::new(33, 45)
    ))
    try { Stroke-Path $g $cursor 6 3 } finally { $cursor.Dispose() }
}

New-Icon "hand" {
    param($g)
    Stroke-Line $g 32 12 32 50 8 4
    Stroke-Line $g 14 32 50 32 8 4
    Stroke-Line $g 20 20 44 44 8 4
    Stroke-Line $g 44 20 20 44 8 4
    $centerBrush = New-Object System.Drawing.SolidBrush($accentColor)
    $outline = New-Pen $outlineColor 4
    $stroke = New-Pen $strokeColor 2
    try {
        $g.FillEllipse($centerBrush, 23, 23, 18, 18)
        $g.DrawEllipse($outline, 23, 23, 18, 18)
        $g.DrawEllipse($stroke, 23, 23, 18, 18)
    }
    finally {
        $centerBrush.Dispose()
        $outline.Dispose()
        $stroke.Dispose()
    }
}

New-Icon "pencil" {
    param($g)
    $body = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(16, 43),
        [System.Drawing.PointF]::new(34, 25),
        [System.Drawing.PointF]::new(43, 34),
        [System.Drawing.PointF]::new(25, 52)
    )
    Fill-And-StrokePolygon $g $body $accentColor
    $tip = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(34, 25),
        [System.Drawing.PointF]::new(48, 12),
        [System.Drawing.PointF]::new(52, 16),
        [System.Drawing.PointF]::new(43, 34)
    )
    Fill-And-StrokePolygon $g $tip $softAccentColor
    Stroke-Line $g 19 48 11 56 7 3
}

New-Icon "eraser" {
    param($g)
    $shape = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(18, 43),
        [System.Drawing.PointF]::new(32, 20),
        [System.Drawing.PointF]::new(48, 30),
        [System.Drawing.PointF]::new(34, 53)
    )
    Fill-And-StrokePolygon $g $shape ([System.Drawing.Color]::FromArgb(245, 230, 158, 158))
    Stroke-Line $g 28 27 42 37 6 3
}

New-Icon "line" {
    param($g)
    Stroke-Line $g 14 48 48 16 8 4
    $brush = New-Object System.Drawing.SolidBrush($accentColor)
    try {
        $g.FillEllipse($brush, 10, 44, 8, 8)
        $g.FillEllipse($brush, 44, 12, 8, 8)
    }
    finally { $brush.Dispose() }
}

New-Icon "rectangle" {
    param($g)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddRectangle([System.Drawing.RectangleF]::new(14, 16, 36, 30))
    try { Stroke-Path $g $path 7 3 } finally { $path.Dispose() }
}

New-Icon "ellipse" {
    param($g)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse([System.Drawing.RectangleF]::new(13, 17, 38, 28))
    try { Stroke-Path $g $path 7 3 } finally { $path.Dispose() }
}

New-Icon "fill" {
    param($g)
    $bucket = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(18, 28),
        [System.Drawing.PointF]::new(31, 16),
        [System.Drawing.PointF]::new(45, 29),
        [System.Drawing.PointF]::new(32, 42)
    )
    Fill-And-StrokePolygon $g $bucket $softAccentColor
    Stroke-Line $g 29 17 42 30 6 3
    $dropBrush = New-Object System.Drawing.SolidBrush($accentColor)
    $dropPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    try {
        $dropPath.AddBezier(41, 37, 50, 28, 55, 43, 47, 49)
        $dropPath.AddBezier(47, 49, 39, 43, 42, 39, 41, 37)
        $g.FillPath($dropBrush, $dropPath)
        Stroke-Path $g $dropPath 5 2
    }
    finally {
        $dropBrush.Dispose()
        $dropPath.Dispose()
    }
}

New-Icon "picker" {
    param($g)
    Stroke-Line $g 18 44 45 17 8 4
    $outline = New-Pen $outlineColor 6
    $stroke = New-Pen $strokeColor 3
    try {
        $g.DrawEllipse($outline, 39, 11, 12, 12)
        $g.DrawEllipse($stroke, 39, 11, 12, 12)
        $g.DrawEllipse($outline, 14, 39, 10, 10)
        $g.DrawEllipse($stroke, 14, 39, 10, 10)
    }
    finally {
        $outline.Dispose()
        $stroke.Dispose()
    }
}
