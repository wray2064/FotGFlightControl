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

# The manual ETS view is assembled from a panel, three nested segmented
# semicircles, and a selection marker. This lets VR show live values
# with colored artwork rather than HUD draw primitives, which render white.
$powerSize = 448
$powerCenter = $powerSize / 2
$powerArcY = 254
$powerRadii = @(105, 150, 195)
function Save-PowerImage([string]$name, [scriptblock]$paint) {
    $bitmap = [System.Drawing.Bitmap]::new($powerSize, $powerSize)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    & $paint $graphics
    $bitmap.Save((Join-Path $effects "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose()
}
Save-PowerImage 'fotg_vr_power_base' {
    param($g)
    $dark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(237, 2, 11, 20))
    $g.FillEllipse($dark, 6, 6, 436, 436)
    $dark.Dispose()
}
for ($arm = 1; $arm -le 3; $arm++) {
    $radius = $powerRadii[$arm - 1]
    for ($level = 0; $level -le 12; $level++) {
        $filled = $level
        Save-PowerImage "fotg_vr_power_${arm}_$level" {
            param($g)
            $on = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 77, 225, 255), 5)
            $off = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 35, 75, 95), 5)
            for ($step = 1; $step -le 12; $step++) {
                $start = 182 + ($step - 1) * 15
                $g.DrawArc($(if ($step -le $filled) { $on } else { $off }),
                    $powerCenter - $radius, $powerArcY - $radius,
                    2 * $radius, 2 * $radius, $start, 11)
            }
            $on.Dispose(); $off.Dispose()
        }
    }
    Save-PowerImage "fotg_vr_power_selected_$arm" {
        param($g)
        $marker = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 255, 205, 92), 2)
        for ($step = 1; $step -le 12; $step++) {
            $start = 182 + ($step - 1) * 15
            $g.DrawArc($marker, $powerCenter - $radius - 6, $powerArcY - $radius - 6,
                2 * ($radius + 6), 2 * ($radius + 6), $start, 11)
        }
        $marker.Dispose()
    }
}

# Shield allocation uses the actual section count: front/rear on FotG
# starfighters, or the standard front/right/rear/left layout on other ships.
Save-PowerImage 'fotg_vr_shield_base' {
    param($g)
    $dark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(237, 2, 11, 20))
    $g.FillEllipse($dark, 6, 6, 436, 436)
    $dark.Dispose()
}
foreach ($count in @(2, 4)) {
    $starts = if ($count -eq 2) { @(-174, 6) } else { @(-132, -42, 48, 138) }
    $angleStep = if ($count -eq 2) { 14 } else { 7 }
    $arcSpan = if ($count -eq 2) { 10 } else { 5 }
    for ($section = 1; $section -le $count; $section++) {
        $startAngle = $starts[$section - 1]
        for ($level = 0; $level -le 12; $level++) {
            $filled = $level
            Save-PowerImage "fotg_vr_shield_${count}_${section}_$level" {
                param($g)
                $on = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 77, 225, 255), 5)
                $off = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 35, 75, 95), 5)
                for ($part = 0; $part -lt 12; $part++) {
                    $angle = $startAngle + $part * $angleStep
                    $g.DrawArc($(if ($part -lt $filled) { $on } else { $off }),
                        49, 49, 350, 350, $angle, $arcSpan)
                }
                $on.Dispose(); $off.Dispose()
            }
        }
        Save-PowerImage "fotg_vr_shield_selected_${count}_$section" {
            param($g)
            $marker = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 255, 205, 92), 2)
            for ($part = 0; $part -lt 12; $part++) {
                $g.DrawArc($marker, 43, 43, 362, 362,
                    $startAngle + $part * $angleStep, $arcSpan)
            }
            $marker.Dispose()
        }
    }
}
