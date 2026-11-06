$ErrorActionPreference = 'Stop'
$builder = Join-Path $env:TEMP ('FotGIconBuilder-' + [guid]::NewGuid().ToString('N') + '.exe')
try {
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /reference:System.Drawing.dll "/out:$builder" (Join-Path $PSScriptRoot 'src\IconBuilder.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Icon builder compilation failed.' }
    & $builder (Join-Path $PSScriptRoot 'Assets')
    if ($LASTEXITCODE -ne 0) { throw 'Icon conversion failed.' }
} finally {
    if (Test-Path -LiteralPath $builder) { Remove-Item -LiteralPath $builder -Force }
}
