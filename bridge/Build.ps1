$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$PSScriptRoot\WheelBridge.exe" "$PSScriptRoot\WheelBridge.cs" "$PSScriptRoot\..\setup\GameInstall.cs" "$PSScriptRoot\..\setup\BuildInfo.cs" "$PSScriptRoot\..\setup\AssemblyInfo.cs"
if ($LASTEXITCODE -ne 0) { throw 'Wheel bridge build failed' }
$selfTest = Start-Process -FilePath (Join-Path $PSScriptRoot 'WheelBridge.exe') -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($selfTest.ExitCode -ne 0) { throw "Wheel bridge self-test failed with exit code $($selfTest.ExitCode)" }
