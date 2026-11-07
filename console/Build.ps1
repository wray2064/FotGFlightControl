$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'BuildIcon.ps1')
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ "/win32icon:$PSScriptRoot\Assets\FlightControl.ico" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/out:$PSScriptRoot\ControllerSetup.exe" "$PSScriptRoot\..\setup\BuildInfo.cs" "$PSScriptRoot\..\setup\Updater.cs" "$PSScriptRoot\src\ControllerSetup.cs" "$PSScriptRoot\src\Cockpit.cs" "$PSScriptRoot\src\MacroDeck.cs" "$PSScriptRoot\src\LayoutProfiles.cs" "$PSScriptRoot\src\ButtonsAndAxes.cs" "$PSScriptRoot\src\ConsoleUpdate.cs" "$PSScriptRoot\src\GamepadView.cs" "$PSScriptRoot\src\TargetingVisual.cs" "$PSScriptRoot\src\WindowChrome.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }


