$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @('hangar\HangarBay.cs','hangar\HangarAssembly.cs','console\src\ControllerSetup.cs','console\src\ControllerDevices.cs','console\src\Cockpit.cs','console\src\MacroDeck.cs','console\src\LayoutProfiles.cs','console\src\ButtonsAndAxes.cs','console\src\ConsoleUpdate.cs','console\src\GamepadView.cs','console\src\TargetingVisual.cs','console\src\WindowChrome.cs','setup\GameInstall.cs','setup\BuildInfo.cs','setup\Updater.cs','setup\AssemblyInfo.cs') | ForEach-Object { Join-Path $workspace $_ }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /main:HangarProgram /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32icon:$workspace\console\Assets\FlightControl.ico" "/out:$workspace\setup\Hangar Bay.exe" $sources
if ($LASTEXITCODE -ne 0) { throw 'Hangar Bay build failed' }
