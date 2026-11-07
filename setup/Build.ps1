$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32icon:$workspace\console\Assets\FlightControl.ico" "/out:$PSScriptRoot\Setup.exe" (Join-Path $PSScriptRoot 'Setup.cs') (Join-Path $PSScriptRoot 'BuildInfo.cs') (Join-Path $PSScriptRoot 'Updater.cs')
if ($LASTEXITCODE -ne 0) { throw 'Setup build failed' }
foreach ($name in @('Play with Gamepad Wheel.exe', 'Configure Gamepad Wheel.exe')) {
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll "/win32icon:$workspace\console\Assets\FlightControl.ico" "/out:$PSScriptRoot\$name" (Join-Path $PSScriptRoot 'Launcher.cs')
    if ($LASTEXITCODE -ne 0) { throw "Launcher build failed: $name" }
}
