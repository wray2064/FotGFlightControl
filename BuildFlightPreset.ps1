param(
    [Parameter(Mandatory = $true)][string]$GameRoot,
    [Parameter(Mandatory = $true)][string]$Destination,
    # Defaults to the newest supported release installed under GameRoot.
    [string]$GameVersion
)
$ErrorActionPreference = 'Stop'
# Knossos installs a release either as package folders (FotG_R-<version>\FG_Core\data\...)
# or merged into one data folder (FotG_R-<version>\data\...); accept both.
$tablePath = {
    param($version)
    $release = Join-Path $GameRoot "kn_portable\Library\FotG_R\FotG_R-$version"
    foreach ($candidate in @('FG_Core\data\tables\Controlconfigdefaults.tbl', 'data\tables\Controlconfigdefaults.tbl')) {
        $path = Join-Path $release $candidate
        if (Test-Path -LiteralPath $path) { return $path }
    }
    return (Join-Path $release 'FG_Core\data\tables\Controlconfigdefaults.tbl')
}
if (-not $GameVersion) {
    $GameVersion = @('1.1.2', '1.1.1', '1.1.0') | Where-Object { Test-Path -LiteralPath (& $tablePath $_) } | Select-Object -First 1
    if (-not $GameVersion) { throw 'Fate of the Galaxy 1.1.0, 1.1.1 or 1.1.2 control defaults were not found.' }
}
$stockPath = & $tablePath $GameVersion
if (-not (Test-Path -LiteralPath $stockPath)) { throw "FotG $GameVersion control defaults were not found." }
$stock = [IO.File]::ReadAllText($stockPath)
# The SDL3 gamepad D-pad is also the engine's native view hat. Keep the game's
# keyboard view arrows, but remove the hat only in this wheel overlay. The
# bridge sends those arrows after a configurable hold; taps belong to wheels.
foreach ($action in @('PADLOCK_UP', 'PADLOCK_DOWN', 'PADLOCK_LEFT', 'PADLOCK_RIGHT')) {
    $pattern = '(?ms)^\s*\$Bind:\s*' + $action + '\b.*?(?=^\s*\$Bind:|^#End)'
    $match = [regex]::Match($stock, $pattern)
    if (-not $match.Success -or $match.Value -notmatch '\$Input:\s*KEY_(UP|DOWN|LEFT|RIGHT)') {
        throw "Expected keyboard view binding was not found: $action"
    }
    $replacement = $match.Value.TrimEnd() + "`r`n        `$Secondary:`r`n            `$Controller: CID_NONE`r`n"
    $stock = $stock.Remove($match.Index, $match.Length).Insert($match.Index, $replacement)
}
$section = [regex]::Match($stock, '(?ms)^#ControlConfigOverride\s*.*?^#End')
if (-not $section.Success -or [regex]::Matches($stock, '(?m)^\s*\$Name:').Count -ne 1) {
    throw 'Unexpected control defaults layout; no flight preset generated.'
}
$classic = [regex]::Replace($section.Value, '(?m)^\$Name:\s*default\b[^\r\n]*', '$Name: FotG Classic Native', 1)
if ($classic -eq $section.Value) { throw 'Default control preset header was not found.' }
$fire = [regex]::Match($classic, '(?ms)^\s*\$Bind:\s*FIRE_PRIMARY\b.*?(?=^\s*\$Bind:|^#End)')
if (-not $fire.Success) { throw 'Primary fire binding was not found.' }
$safeFire = [regex]::Replace($fire.Value,
    '(?ms)(\$Primary:\s*\r?\n)\s*\$Controller:\s*CID_JOY0\s*\r?\n\s*\$Flags:[^\r\n]*\r?\n\s*\$Input:\s*0\s*\r?\n',
    '$1            $Controller: CID_NONE' + "`r`n", 1)
if ($safeFire -eq $fire.Value) { throw 'Expected A-button primary fire binding was not found.' }
$classic = $classic.Remove($fire.Index, $fire.Length).Insert($fire.Index, $safeFire)

function Set-ClassicBind([string]$table, [string]$action, [scriptblock]$change) {
    $pattern = '(?ms)^\s*\$Bind:\s*' + [regex]::Escape($action) + '\b.*?(?=^\s*\$Bind:|^#End)'
    $match = [regex]::Match($table, $pattern)
    if (-not $match.Success) { throw "Expected control binding was not found: $action" }
    $edited = & $change $match.Value
    if ($edited -eq $match.Value) { throw "Expected joystick binding was not changed: $action" }
    return $table.Remove($match.Index, $match.Length).Insert($match.Index, $edited)
}

# LB cycles secondary banks, RB selects ship mode, and Y fires a real third
# primary gun through Lua. Clear their old native bindings to prevent overlap.
$classic = Set-ClassicBind $classic 'CYCLE_PRIMARY_WEAPON_SEQUENCE' {
    param($bind)
    [regex]::Replace($bind,
        '(?ms)(\$Secondary:\s*\r?\n)\s*\$Controller:\s*CID_JOY0\s*\r?\n\s*\$Flags:\s*NONE\s*\r?\n\s*\$Input:\s*4\s*\r?\n',
        '$1            $Controller: CID_NONE' + "`r`n", 1)
}
$classic = Set-ClassicBind $classic 'CUSTOM_CONTROL_5' {
    param($bind)
    [regex]::Replace($bind,
        '(?ms)(\$Secondary:\s*\r?\n)\s*\$Controller:\s*CID_JOY0\s*\r?\n\s*\$Flags:\s*NONE\s*\r?\n\s*\$Input:\s*5\s*\r?\n',
        '$1            $Controller: CID_NONE' + "`r`n", 1)
}
# Start is pause; shield equalization remains on the systems wheel.
$classic = Set-ClassicBind $classic 'SHIELD_EQUALIZE' {
    param($bind)
    [regex]::Replace($bind,
        '(?ms)(\$Secondary:\s*\r?\n)\s*\$Controller:\s*CID_JOY0\s*\r?\n\s*\$Flags:\s*NONE\s*\r?\n\s*\$Input:\s*7\s*\r?\n',
        '$1            $Controller: CID_NONE' + "`r`n", 1)
}
# B cancels a wheel or targets the closest attacker in flight. A targets the
# ship in the reticle and X cycles the selected target class outside a wheel.
$classic = Set-ClassicBind $classic 'TARGET_SHIP_IN_RETICLE' {
    param($bind)
    [regex]::Replace($bind,
        '(?ms)(\$Secondary:\s*\r?\n)\s*\$Controller:\s*CID_JOY0\s*\r?\n\s*\$Flags:\s*NONE\s*\r?\n\s*\$Input:\s*1\s*\r?\n',
        '$1            $Controller: CID_NONE' + "`r`n", 1)
}
$classic = Set-ClassicBind $classic 'TARGET_CLOSEST_SHIP_ATTACKING_SELF' {
    param($bind)
    [regex]::Replace($bind,
        '(?ms)(\$Secondary:\s*\r?\n)\s*\$Controller:\s*CID_JOY0\s*\r?\n\s*\$Flags:\s*NONE\s*\r?\n\s*\$Input:\s*2\s*\r?\n',
        '$1            $Controller: CID_NONE' + "`r`n", 1)
}
$classic = Set-ClassicBind $classic 'TARGET_NEXT_CLOSEST_HOSTILE' {
    param($bind)
    [regex]::Replace($bind,
        '(?ms)(\$Secondary:\s*\r?\n)\s*\$Controller:\s*CID_JOY0\s*\r?\n\s*\$Flags:\s*NONE\s*\r?\n\s*\$Input:\s*3\s*\r?\n',
        '$1            $Controller: CID_NONE' + "`r`n", 1)
}
# The SDL3 FSO build reads recognized controllers through SDL_GetGamepadAxis,
# not SDL_GetJoystickAxis. Its canonical order is left X/Y, right X/Y,
# left/right trigger. Raw joystick axis 2 is a trigger on the Legion, but
# gamepad axis 2 is right X. This is the mapping used by the working v1 preset.
$axes = @'
    $Bind: JOY_HEADING_AXIS
        $Primary:
            $Controller: CID_JOY0
            $Flags: AXIS
            $Input: Z_AXIS
        $Secondary:
            $Controller: CID_NONE
    $Bind: JOY_PITCH_AXIS
        $Primary:
            $Controller: CID_JOY0
            $Flags: AXIS
            $Input: RX_AXIS
        $Secondary:
            $Controller: CID_NONE
    $Bind: JOY_BANK_AXIS
        $Primary:
            $Controller: CID_JOY0
            $Flags: AXIS
            $Input: X_AXIS
        $Secondary:
            $Controller: CID_NONE
    $Bind: JOY_REL_THROTTLE_AXIS
        $Primary:
            $Controller: CID_JOY0
            $Flags: AXIS, INVERTED
            $Input: Y_AXIS
        $Secondary:
            $Controller: CID_NONE
    $Bind: JOY_ABS_THROTTLE_AXIS
        $Primary:
            $Controller: CID_NONE
        $Secondary:
            $Controller: CID_NONE
'@
# Drop any axis bindings the game's own default already carries (1.1.2 lists
# JOY_BANK_AXIS as unbound), so each axis appears once, with the classic binding.
foreach ($axis in @('JOY_HEADING_AXIS', 'JOY_PITCH_AXIS', 'JOY_BANK_AXIS', 'JOY_REL_THROTTLE_AXIS', 'JOY_ABS_THROTTLE_AXIS')) {
    $classic = [regex]::Replace($classic, '(?ms)^[ \t]*\$Bind:\s*' + $axis + '\b.*?(?=^\s*\$Bind:|^#End)', '')
}
$classic = [regex]::Replace($classic, '(?m)^#End\b', $axes + "`r`n#End", 1)
$inverted = $classic.Replace('$Name: FotG Classic Native', '$Name: FotG Classic Inverted')
$pitch = [regex]::Match($inverted, '(?ms)^\s*\$Bind:\s*JOY_PITCH_AXIS\b.*?(?=^\s*\$Bind:|^#End)')
if (-not $pitch.Success) { throw 'Classic pitch binding was not found.' }
$reversedPitch = $pitch.Value.Replace('$Flags: AXIS', '$Flags: AXIS, INVERTED')
$inverted = $inverted.Remove($pitch.Index, $pitch.Length).Insert($pitch.Index, $reversedPitch)
$output = $stock.TrimEnd() + "`r`n`r`n" + $classic + "`r`n`r`n" + $inverted + "`r`n"
$output = [regex]::Replace($output, '\r?\n', "`r`n")
$folder = Split-Path -Parent $Destination
New-Item -ItemType Directory -Path $folder -Force | Out-Null
[IO.File]::WriteAllText($Destination, $output, [Text.UTF8Encoding]::new($false))
Write-Host "Generated FotG Classic Native preset at $Destination"
