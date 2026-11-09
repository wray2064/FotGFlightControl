$ErrorActionPreference = 'Stop'
$workspace = [System.IO.Path]::GetFullPath($PSScriptRoot)
# Setup installs by itself (setup\Installer.cs) and runs no scripts.
& (Join-Path $workspace 'bridge\Build.ps1')
& (Join-Path $workspace 'console\Build.ps1')
& (Join-Path $workspace 'setup\Build.ps1')
$setupTest = Start-Process -FilePath (Join-Path $workspace 'setup\Setup.exe') -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($setupTest.ExitCode -ne 0) { throw "Setup self-test failed with exit code $($setupTest.ExitCode)" }
$selfTest = Start-Process -FilePath (Join-Path $workspace 'console\ControllerSetup.exe') -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($selfTest.ExitCode -ne 0) { throw "Controller Setup self-test failed with exit code $($selfTest.ExitCode)" }

# Release version: change it here for each public release.
$version = '1.1.4'
$buildInfo = [IO.File]::ReadAllText((Join-Path $workspace 'setup\BuildInfo.cs'))
if ($buildInfo -notmatch ('Version = "' + [regex]::Escape($version) + '"')) { throw "setup\BuildInfo.cs does not say version $version." }
$name = 'FotG-Flight-Control-' + $version
$dist = Join-Path $workspace 'dist'
$stage = Join-Path $dist $name
if (Test-Path -LiteralPath $stage) {
    $resolvedDist = [IO.Path]::GetFullPath($dist).TrimEnd('\')
    $resolvedStage = [IO.Path]::GetFullPath($stage).TrimEnd('\')
    if (-not $resolvedStage.StartsWith($resolvedDist + '\', [StringComparison]::OrdinalIgnoreCase) -or
        -not [String]::Equals([IO.Path]::GetFileName($resolvedStage), $name, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Package staging path escaped dist.'
    }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$sourceMod = Join-Path $workspace 'mod\FotG_Gamepad_Wheel'
$targetMod = Join-Path $stage 'FotG_Gamepad_Wheel'
if (-not (Test-Path -LiteralPath (Join-Path $sourceMod 'data\tables\lua\zzz_fotg_wheel-sct.lua'))) {
    throw 'Wheel Lua script is missing.'
}
Copy-Item -LiteralPath $sourceMod -Destination $targetMod -Recurse
Copy-Item -LiteralPath (Join-Path $workspace 'bridge\WheelBridge.exe') -Destination (Join-Path $targetMod 'WheelBridge.exe')
New-Item -ItemType Directory -Path (Join-Path $targetMod 'source') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $workspace 'bridge\WheelBridge.cs') -Destination (Join-Path $targetMod 'source\WheelBridge.cs')
Copy-Item -LiteralPath (Join-Path $workspace 'bridge\Build.ps1') -Destination (Join-Path $targetMod 'source\BuildBridge.ps1')
Copy-Item -LiteralPath (Join-Path $workspace 'setup\GameInstall.cs') -Destination (Join-Path $targetMod 'source\GameInstall.cs')

$setup = Join-Path $workspace 'console'
$targetSetup = Join-Path $targetMod 'ControllerSetup'
New-Item -ItemType Directory -Path $targetSetup -Force | Out-Null
foreach ($item in @('ControllerSetup.exe', 'Build.ps1', 'BuildIcon.ps1', 'Help.html', 'About.html', 'README.md', 'LICENSE', 'NOTICE.md', 'ARTWORK-LICENSE.md')) {
    Copy-Item -LiteralPath (Join-Path $setup $item) -Destination (Join-Path $targetSetup $item)
}
Copy-Item -LiteralPath (Join-Path $setup 'src') -Destination (Join-Path $targetSetup 'src') -Recurse
Copy-Item -LiteralPath (Join-Path $setup 'Assets') -Destination (Join-Path $targetSetup 'Assets') -Recurse
foreach ($item in @('README.md', 'Help.html', 'About.html', 'LICENSE', 'NOTICE.md', 'ARTWORK-LICENSE.md', 'CONTRIBUTING.md', 'SECURITY.md')) {
    Copy-Item -LiteralPath (Join-Path $workspace $item) -Destination (Join-Path $stage $item)
}
Copy-Item -LiteralPath (Join-Path $workspace 'setup\Setup.exe') -Destination (Join-Path $stage 'Setup.exe')
Copy-Item -LiteralPath (Join-Path $workspace 'setup\Hangar Bay.exe') -Destination (Join-Path $stage 'Hangar Bay.exe')
foreach ($launcherName in @('Play with Gamepad Wheel.exe', 'Play with Gamepad Wheel VR.exe', 'Configure Gamepad Wheel.exe')) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $workspace 'setup') $launcherName) -Destination (Join-Path $stage $launcherName)
}
New-Item -ItemType Directory -Path (Join-Path $stage 'Assets') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $workspace 'console\Assets\FlightControl.ico') -Destination (Join-Path $stage 'Assets\FlightControl.ico')
Copy-Item -LiteralPath (Join-Path $workspace 'console\Assets\FlightControl.png') -Destination (Join-Path $stage 'Assets\FlightControl.png')
New-Item -ItemType Directory -Path (Join-Path $stage 'source') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $workspace 'setup\Setup.cs') -Destination (Join-Path $stage 'source\Setup.cs')
Copy-Item -LiteralPath (Join-Path $workspace 'setup\Launcher.cs') -Destination (Join-Path $stage 'source\Launcher.cs')
Copy-Item -LiteralPath (Join-Path $workspace 'hangar\HangarBay.cs') -Destination (Join-Path $stage 'source\HangarBay.cs')
Copy-Item -LiteralPath (Join-Path $workspace 'hangar\Build.ps1') -Destination (Join-Path $stage 'source\BuildHangar.ps1')
foreach ($shared in @('BuildInfo.cs', 'Updater.cs', 'GameInstall.cs', 'Installer.cs', 'AssemblyInfo.cs')) {
    Copy-Item -LiteralPath (Join-Path $workspace "setup\$shared") -Destination (Join-Path $stage "source\$shared")
}
Copy-Item -LiteralPath (Join-Path $workspace 'setup\Build.ps1') -Destination (Join-Path $stage 'source\BuildSetup.ps1')
New-Item -ItemType Directory -Path (Join-Path $stage 'docs') -Force | Out-Null
# Raw local diagnostic logs are investigation evidence, not player downloads.
Get-ChildItem -LiteralPath (Join-Path $workspace 'docs') | Where-Object { $_.Name -ne 'steamvr-triage-20261108' } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $stage 'docs') -Recurse
}
Copy-Item -LiteralPath (Join-Path $workspace 'Help.html') -Destination (Join-Path $targetMod 'Help.html')
Copy-Item -LiteralPath (Join-Path $workspace 'About.html') -Destination (Join-Path $targetMod 'About.html')
Copy-Item -LiteralPath (Join-Path $workspace 'LICENSE') -Destination (Join-Path $targetMod 'LICENSE')
Copy-Item -LiteralPath (Join-Path $workspace 'NOTICE.md') -Destination (Join-Path $targetMod 'NOTICE.md')
Copy-Item -LiteralPath (Join-Path $workspace 'ARTWORK-LICENSE.md') -Destination (Join-Path $targetMod 'ARTWORK-LICENSE.md')

$checksums = Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object {
    $relative = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative
}
$checksums | Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Encoding Ascii
$archive = Join-Path $dist ($name + '.zip')
Compress-Archive -LiteralPath $stage -DestinationPath $archive -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Package: $archive"
Write-Host "SHA256: $hash"
# update.json for the product site. Publish it only after the ZIP is live at the URL below.
# verifiedGameVersions lists, per Flight Control version, game versions checked after release.
$manifest = [ordered]@{
    schema = 1
    latest = [ordered]@{
        version = $version
        date = (Get-Date -Format 'yyyy-MM-dd')
        url = "https://manictrashpanda.dev/games/FotGFlightControl/download/$name.zip"
        size = (Get-Item -LiteralPath $archive).Length
        sha256 = $hash
        notes = "https://github.com/wray2064/FotGFlightControl/releases/tag/v$version"
    }
    verifiedGameVersions = [ordered]@{ $version = @() }
}
[IO.File]::WriteAllText((Join-Path $dist 'update.json'), ($manifest | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
Write-Host "Update manifest: $(Join-Path $dist 'update.json')"
