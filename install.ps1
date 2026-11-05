# Builds and installs WeekTray for the current user, starts it at logon and keeps the icon visible.
$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\WeekTray'
$exe = Join-Path $installDir 'WeekTray.exe'

& (Join-Path $PSScriptRoot 'build.ps1')

Get-Process WeekTray -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process WeekTray -ErrorAction SilentlyContinue | Wait-Process -Timeout 5
New-Item -ItemType Directory -Force $installDir | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'bin\WeekTray.exe') $exe -Force

Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name WeekTray -Value "`"$exe`""
Start-Process $exe

# Windows registers a tray icon a moment after it first appears; only then can it be pinned outside the overflow menu.
$pinned = $false
foreach ($attempt in 1..20) {
    Start-Sleep -Milliseconds 500
    $entries = Get-ChildItem 'HKCU:\Control Panel\NotifyIconSettings' -ErrorAction SilentlyContinue |
        Where-Object { (Get-ItemProperty $_.PSPath).ExecutablePath -like '*\WeekTray.exe' }
    foreach ($entry in $entries) {
        Set-ItemProperty $entry.PSPath -Name IsPromoted -Value 1 -Type DWord
        $pinned = $true
    }
    if ($pinned) { break }
}

Write-Output "Geinstalleerd: $exe"
if ($pinned) {
    Write-Output 'Pictogram vastgezet op de taakbalk.'
} else {
    Write-Output 'Pictogram niet automatisch vastgezet; doe dit via Instellingen > Persoonlijke instellingen > Taakbalk > Andere systeemvakpictogrammen.'
}
