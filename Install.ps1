param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,
    [switch]$SkipPilotReservation
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($GameRoot).TrimEnd('\')
# Supported game releases, newest first; keep in step with setup\Setup.cs and bridge\WheelBridge.cs.
$supportedVersions = @('1.1.1', '1.1.0')
$gameVersion = $supportedVersions | Where-Object {
    Test-Path -LiteralPath (Join-Path $root "kn_portable\Library\FotG_R\FotG_R-$_\mod.json")
} | Select-Object -First 1
if (-not $gameVersion) {
    throw "Select a complete Fate of the Galaxy $($supportedVersions[-1..0] -join ' or ') Windows installation (the folder containing kn_portable)."
}
$manifest = Join-Path $root "kn_portable\Library\FotG_R\FotG_R-$gameVersion\mod.json"
$engine = Join-Path $root 'kn_portable\Library\bin\FSO_2601SDL3-1.0.0\windows-x64\fs2_open_26_0_1_sdl3_x64_SSE2.exe'
if (-not (Test-Path -LiteralPath $engine)) {
    throw "Fate of the Galaxy $gameVersion was found, but not its FSO 26.0.1 SDL3 engine."
}
if ((Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).version -ne $gameVersion) {
    throw "The FotG_R-$gameVersion manifest reports a different version. Repair the game in Knossos first."
}
Write-Host "Fate of the Galaxy $gameVersion found."
if (Get-Process fs2_open* -ErrorAction SilentlyContinue) {
    throw 'Close Fate of the Galaxy before installing the command wheel.'
}
if (Get-Process ControllerSetup,WheelBridge -ErrorAction SilentlyContinue) {
    throw 'Close the Gamepad Wheel settings app and controller bridge before installing or repairing.'
}
if (-not $SkipPilotReservation) {
    $prefsData = Join-Path $root 'kn_portable\HardLightProductions\FreeSpaceOpen\data'
    $players = Join-Path $prefsData 'players'
    $settings = Join-Path $prefsData 'FotG_R_settings.ini'
    if (-not (Test-Path -LiteralPath $players) -or -not (Test-Path -LiteralPath $settings)) {
        throw 'Run Fate of the Galaxy once, create a pilot, then close it before installing the wheel.'
    }
    $nativePilots = @(Get-ChildItem -LiteralPath $players -Filter '*.json' -File | Where-Object {
        try { (Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json).version -eq 4 }
        catch { $false }
    })
    if ($nativePilots.Count -eq 0) {
        throw 'No native version-4 pilot was found. Run the game, create a pilot, then close it before installing the wheel.'
    }
}

$source = Join-Path $PSScriptRoot 'FotG_Gamepad_Wheel'
$required = @(
    'WheelBridge.exe',
    'ControllerSetup\ControllerSetup.exe',
    'data\tables\lua\zzz_fotg_wheel-sct.lua',
    'data\tables\lua\zz_fotg_wheel_config-sct.lua'
)
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $name))) {
        throw "Package is incomplete: $name"
    }
}
$launcherNames = @('Play with Gamepad Wheel.exe', 'Configure Gamepad Wheel.exe')
foreach ($name in $launcherNames) {
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $name))) {
        throw "Package is incomplete: $name"
    }
}

# FSO names its INI after the first -mod path component. SDL3 may give the
# same physical pad a different GUID than the old game's SDL2 build, so ask
# the bundled SDL3.dll instead of copying a stale selection between profiles.
$prefsData = Join-Path $root 'kn_portable\HardLightProductions\FreeSpaceOpen\data'
$activeSettings = Join-Path $prefsData 'GamepadMods_settings.ini'
$existingActive = if (Test-Path -LiteralPath $activeSettings) {
    [IO.File]::ReadAllText($activeSettings)
} else { $null }
$probe = Join-Path $PSScriptRoot 'tools\ProbeSdl3Guid.ps1'
if (-not (Test-Path -LiteralPath $probe)) { throw 'SDL3 joystick probe is missing from the package.' }
$devices = @(& $probe -GameRoot $root)
$selection = $null
if ($devices.Count -gt 0) {
    $chosen = if ($devices.Count -eq 1) { $devices[0] } else {
        $devices | Where-Object { $_.Name -match 'XInput|Controller|Gamepad' } | Select-Object -First 1
    }
    if (-not $chosen) { throw 'Multiple joysticks were found. Connect only the gamepad you want to use, then install again.' }
    $selection = '{"guid":"' + $chosen.Guid + '","id":' + $chosen.Id + '}'
} elseif ($null -eq $existingActive -or $existingActive -notmatch '(?m)^Joystick\s*=') {
    throw 'Connect the gamepad and run Install / Repair again so SDL3 can identify its flight axes.'
} else {
    Write-Warning 'No connected gamepad was found. Existing joystick selection was kept; run Install / Repair with the gamepad connected before testing flight.'
}
$oldSelection = if ($null -ne $existingActive) {
    [regex]::Match($existingActive, '(?m)^Joystick\s*=\s*([^\r\n]+)').Groups[1].Value.Trim()
} else { '' }
if ($selection -and $selection -ne $oldSelection) {
    $profileBackup = Join-Path $prefsData ('..\ControllerSetupBackups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-before-wheel-profile')
    New-Item -ItemType Directory -Path $profileBackup -Force | Out-Null
    if ($null -ne $existingActive) {
        Copy-Item -LiteralPath $activeSettings -Destination (Join-Path $profileBackup 'GamepadMods_settings.ini')
    }
    $updatedActive = if ($null -eq $existingActive) {
        "[Input]`r`nJoystick=$selection`r`n"
    } elseif ($existingActive -match '(?m)^Joystick\s*=') {
        [regex]::Replace($existingActive, '(?m)^Joystick\s*=[^\r\n]*', "Joystick=$selection")
    } elseif ($existingActive -match '(?m)^\[Input\][ \t]*\r?$') {
        [regex]::Replace($existingActive, '(?m)^(\[Input\][ \t]*\r?\n)', ('$1' + "Joystick=$selection`r`n"))
    } else {
        $existingActive.TrimEnd() + "`r`n`r`n[Input]`r`nJoystick=$selection`r`n"
    }
    [IO.File]::WriteAllText($activeSettings, $updatedActive, [Text.UTF8Encoding]::new($false))
    Write-Host "Selected SDL3 joystick: $($chosen.Name) ($($chosen.Guid))"
}
$destination = Join-Path $root 'GamepadMods\FotG_Gamepad_Wheel'
$parent = Split-Path -Parent $destination
New-Item -ItemType Directory -Path $parent -Force | Out-Null
$existingConfig = Join-Path $destination 'data\tables\lua\zz_fotg_wheel_config-sct.lua'
$savedConfig = if (Test-Path -LiteralPath $existingConfig) { [System.IO.File]::ReadAllBytes($existingConfig) } else { $null }

# Keep one dated install snapshot away from the launchers and active mod.
$backupRoot = Join-Path $parent '_WheelInstallBackups'
$snapshot = $null
if (Test-Path -LiteralPath $destination) {
    $snapshot = Join-Path $backupRoot (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
    New-Item -ItemType Directory -Path $snapshot -Force | Out-Null
    Copy-Item -LiteralPath $destination -Destination (Join-Path $snapshot 'FotG_Gamepad_Wheel') -Recurse -Force
    # A repair replaces the prior addon tree after a full snapshot. Keeping
    # the old tree caused stale nested data/source folders to accumulate.
    $resolvedDestination = [IO.Path]::GetFullPath($destination).TrimEnd('\')
    $resolvedParent = [IO.Path]::GetFullPath($parent).TrimEnd('\')
    if ((-not $resolvedDestination.StartsWith($resolvedParent + '\', [StringComparison]::OrdinalIgnoreCase)) -or
        ([IO.Path]::GetFileName($resolvedDestination) -ne 'FotG_Gamepad_Wheel')) {
        throw 'The addon destination did not resolve inside GamepadMods.'
    }
    Remove-Item -LiteralPath $resolvedDestination -Recurse -Force
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Get-ChildItem -LiteralPath $source -File -Recurse -Force | ForEach-Object {
    $relative = $_.FullName.Substring($source.Length + 1)
    $targetFile = Join-Path $destination $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $targetFile) -Force | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $targetFile -Force
}
if ($null -ne $savedConfig) {
    [System.IO.File]::WriteAllBytes($existingConfig, $savedConfig)
}
$presetBuilder = Join-Path $PSScriptRoot 'BuildFlightPreset.ps1'
if (-not (Test-Path -LiteralPath $presetBuilder)) { throw 'The flight preset builder is missing from the package.' }
& $presetBuilder -GameRoot $root -GameVersion $gameVersion -Destination (Join-Path $destination 'data\tables\Controlconfigdefaults.tbl')
if ($null -ne $savedConfig) {
    $configText = [IO.File]::ReadAllText($existingConfig)
    if ($configText -notmatch '\bflight_axes\s*=') {
        $configText = $configText.Replace('FotGWheelConfig = {', 'FotGWheelConfig = {' + "`r`n    flight_axes = `"classic`",")
        [IO.File]::WriteAllText($existingConfig, $configText, [Text.UTF8Encoding]::new($false))
    }
}
if (-not $SkipPilotReservation) {
    $reserve = Start-Process -FilePath (Join-Path $destination 'ControllerSetup\ControllerSetup.exe') -ArgumentList @('--reserve-wheel', '--game', ('"' + $root + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($reserve.ExitCode -ne 0) {
        throw 'Pilot control reservation failed. The addon was copied, but launchers were not created. See Controller Setup for details.'
    }
}

$legacyNames = @('Play with Gamepad Wheel.cmd', 'Configure Gamepad Wheel.cmd')
foreach ($name in @($launcherNames + $legacyNames)) {
    $file = [IO.Path]::GetFullPath((Join-Path $root $name))
    if (-not $file.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Launcher path escaped the selected game folder.'
    }
    if (Test-Path -LiteralPath $file) {
        if (-not $snapshot) {
            $snapshot = Join-Path $backupRoot (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
            New-Item -ItemType Directory -Path $snapshot -Force | Out-Null
        }
        Copy-Item -LiteralPath $file -Destination (Join-Path $snapshot $name) -Force
    }
}
foreach ($name in $launcherNames) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $root $name) -Force
}
foreach ($name in $legacyNames) {
    $old = Join-Path $root $name
    if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Force }
}
$launch = Join-Path $root $launcherNames[0]
$configure = Join-Path $root $launcherNames[1]

Write-Host "Installed addon at $destination"
Write-Host "Launch with: $launch"
Write-Host "Configure with: $configure"
if ($SkipPilotReservation) {
    Write-Host 'Before flight, open Controller Setup and use Reserve wheel buttons for each pilot.'
} else {
    Write-Host 'Native pilot A, B, X, Y, LB, RB, Start, L3, R3 and D-pad bindings were reserved with backups where needed.'
}
