$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32icon:$workspace\console\Assets\FlightControl.ico" "/out:$PSScriptRoot\Setup.exe" (Join-Path $PSScriptRoot 'Setup.cs') (Join-Path $PSScriptRoot 'BuildInfo.cs') (Join-Path $PSScriptRoot 'Updater.cs') (Join-Path $PSScriptRoot 'GameInstall.cs') (Join-Path $PSScriptRoot 'Installer.cs') (Join-Path $PSScriptRoot 'AssemblyInfo.cs')
if ($LASTEXITCODE -ne 0) { throw 'Setup build failed' }
foreach ($name in @('Play with Gamepad Wheel.exe', 'Configure Gamepad Wheel.exe')) {
    # Each launcher gets its own title in its file properties.
    $label = [IO.Path]::GetFileNameWithoutExtension($name)
    $title = Join-Path ([IO.Path]::GetTempPath()) ('FlightControlLauncherTitle-' + [guid]::NewGuid().ToString('N') + '.cs')
    [IO.File]::WriteAllText($title, "[assembly: System.Reflection.AssemblyTitle(`"Flight Control: $label`")]`r`n[assembly: System.Reflection.AssemblyDescription(`"$label, part of Flight Control for Fate of the Galaxy.`")]`r`n")
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll "/win32icon:$workspace\console\Assets\FlightControl.ico" "/out:$PSScriptRoot\$name" (Join-Path $PSScriptRoot 'Launcher.cs') (Join-Path $PSScriptRoot 'BuildInfo.cs') (Join-Path $PSScriptRoot 'AssemblyInfo.cs') $title
    Remove-Item -LiteralPath $title -Force
    if ($LASTEXITCODE -ne 0) { throw "Launcher build failed: $name" }
}
