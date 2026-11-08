Add-Type -AssemblyName System.Drawing

$effects = Join-Path $PSScriptRoot '..\mod\FotG_Gamepad_Wheel\data\effects'
New-Item -ItemType Directory -Path $effects -Force | Out-Null

function Save-Panel([string]$name, [int]$width, [int]$height, [bool]$selected) {
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $dark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(245, 2, 11, 20))
    $graphics.FillRectangle($dark, 0, 0, $width, $height)
    if ($selected) {
        $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 77, 225, 255), 3)
        $graphics.DrawRectangle($pen, 2, 2, $width - 5, $height - 5)
        $pen.Dispose()
    }
    $bitmap.Save((Join-Path $effects "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $dark.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}

$size = 448
$bitmap = [System.Drawing.Bitmap]::new($size, $size)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$dark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(244, 2, 11, 20))
$cyan = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 77, 225, 255), 3)
$dim = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(170, 33, 131, 159), 2)
$graphics.FillEllipse($dark, 4, 4, $size - 8, $size - 8)
$graphics.DrawEllipse($cyan, 5, 5, $size - 11, $size - 11)
$graphics.DrawEllipse($dim, 64, 64, $size - 128, $size - 128)
for ($sector = 0; $sector -lt 8; $sector++) {
    $angle = (-90 + $sector * 45) * [Math]::PI / 180
    $inner = 160; $outer = 218; $center = $size / 2
    $graphics.DrawLine($dim,
        [int]($center + [Math]::Cos($angle) * $inner),
        [int]($center + [Math]::Sin($angle) * $inner),
        [int]($center + [Math]::Cos($angle) * $outer),
        [int]($center + [Math]::Sin($angle) * $outer))
}
$bitmap.Save((Join-Path $effects 'fotg_vr_wheel.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$dim.Dispose(); $cyan.Dispose(); $dark.Dispose(); $graphics.Dispose(); $bitmap.Dispose()

foreach ($width in @(128, 192, 256, 320, 384, 448, 512, 640)) {
    Save-Panel "fotg_vr_label_$width" $width 48 $false
    Save-Panel "fotg_vr_selected_$width" $width 48 $true
}
