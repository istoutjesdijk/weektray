# Removes WeekTray, its autostart entry, its settings and its tray icon settings.
$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\WeekTray'

Get-Process WeekTray -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process WeekTray -ErrorAction SilentlyContinue | Wait-Process -Timeout 5
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name WeekTray -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\WeekTray' -Recurse -ErrorAction SilentlyContinue
Get-ChildItem 'HKCU:\Control Panel\NotifyIconSettings' -ErrorAction SilentlyContinue |
    Where-Object { (Get-ItemProperty $_.PSPath).ExecutablePath -like '*\WeekTray.exe' } |
    Remove-Item -Recurse
if (Test-Path $installDir) { Remove-Item $installDir -Recurse -Force }
Write-Output 'WeekTray verwijderd.'
