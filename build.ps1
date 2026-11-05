# Compiles WeekTray with the C# compiler that ships with Windows; no SDK needed.
$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$bin = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

& $csc /nologo /target:winexe /optimize+ `
    "/out:$(Join-Path $bin 'WeekTray.exe')" `
    "/win32manifest:$(Join-Path $PSScriptRoot 'src\app.manifest')" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    (Join-Path $PSScriptRoot 'src\WeekTray.cs')
if ($LASTEXITCODE -ne 0) { throw "Build mislukt (exit $LASTEXITCODE)" }
Write-Output "Gebouwd: $(Join-Path $bin 'WeekTray.exe')"
