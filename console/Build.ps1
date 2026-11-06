$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'BuildIcon.ps1')
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ "/win32icon:$PSScriptRoot\Assets\FlightControl.ico" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/out:$PSScriptRoot\ControllerSetup.exe" "$PSScriptRoot\src\ControllerSetup.cs" "$PSScriptRoot\src\Cockpit.cs" "$PSScriptRoot\src\MacroDeck.cs" "$PSScriptRoot\src\LayoutProfiles.cs" "$PSScriptRoot\src\GamepadView.cs" "$PSScriptRoot\src\TargetingVisual.cs" "$PSScriptRoot\src\WindowChrome.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }


