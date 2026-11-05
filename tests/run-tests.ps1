# Checks the ISO week logic against known dates. Run build.ps1 first.
param([string]$PreviewDir)
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot '..\bin\WeekTray.exe'
$assembly = [Reflection.Assembly]::LoadFile((Resolve-Path $exe).Path)
$isoWeek = $assembly.GetType('WeekTray.IsoWeek')
$invariant = [Globalization.CultureInfo]::InvariantCulture

# date, expected ISO year, expected ISO week
$cases = @(
    @('2026-11-05', 2026, 45),
    @('2026-01-01', 2026, 1),
    @('2025-12-29', 2026, 1),
    @('2025-12-28', 2025, 52),
    @('2021-01-03', 2020, 53),
    @('2021-01-04', 2021, 1),
    @('2020-12-31', 2020, 53),
    @('2024-12-30', 2025, 1),
    @('2023-01-01', 2022, 52),
    @('2026-12-31', 2026, 53),
    @('2027-01-03', 2026, 53),
    @('2027-01-04', 2027, 1)
)

$failures = 0
foreach ($case in $cases) {
    $date = [datetime]::ParseExact($case[0], 'yyyy-MM-dd', $invariant)
    $year = $isoWeek.GetMethod('GetYear').Invoke($null, @($date))
    $week = $isoWeek.GetMethod('GetWeek').Invoke($null, @($date))
    if ($year -ne $case[1] -or $week -ne $case[2]) {
        Write-Output "FOUT $($case[0]): verwacht $($case[1])-W$($case[2]), kreeg $year-W$week"
        $failures++
    }
}

# today, weeks ahead, expected rows as "week|period"; the first row is always the current week.
$overviews = @(
    @('2026-11-05', 4, @('45|2 t/m 8 november', '46|9 t/m 15 november', '47|16 t/m 22 november', '48|23 t/m 29 november', '49|30 november t/m 6 december')),
    @('2026-12-30', 1, @('53|28 december t/m 3 januari 2027', '1|4 t/m 10 januari 2027')),
    @('2027-01-02', 1, @('53|28 december t/m 3 januari 2027', '1|4 t/m 10 januari'))
)
foreach ($case in $overviews) {
    $date = [datetime]::ParseExact($case[0], 'yyyy-MM-dd', $invariant)
    $rows = $isoWeek.GetMethod('GetOverview').Invoke($null, @($date, [int]$case[1]))
    $actual = @($rows | ForEach-Object { "$($_.Week)|$($_.Period)" })
    if (($actual -join ';') -cne ($case[2] -join ';') -or -not $rows[0].IsCurrent -or $rows[1].IsCurrent) {
        Write-Output "FOUT overzicht $($case[0]): kreeg '$($actual -join ';')'"
        $failures++
    }
}

if ($PreviewDir) {
    Add-Type -AssemblyName System.Drawing
    New-Item -ItemType Directory -Force $PreviewDir | Out-Null
    $renderer = $assembly.GetType('WeekTray.IconRenderer')
    foreach ($week in 1, 8, 45, 53) {
        foreach ($size in 16, 20, 24, 32) {
            foreach ($light in $true, $false) {
                $bitmap = $renderer.GetMethod('Render').Invoke($null, @([int]$week, [int]$size, [bool]$light))
                $theme = if ($light) { 'light' } else { 'dark' }
                $bitmap.Save((Join-Path $PreviewDir "w$week-$size-$theme.png"))
                $bitmap.Dispose()
            }
        }
    }
}

if ($failures -gt 0) { throw "$failures test(s) mislukt" }
Write-Output "OK: $($cases.Count) weeknummers en $($overviews.Count) overzichten correct"
