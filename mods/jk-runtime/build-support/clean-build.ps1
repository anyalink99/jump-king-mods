param(
    [ValidateSet('All','Runtime','Mapping','Worldsmith','Wardrobe','Multiplayer','StereoMadness','TotalEclipse','Prism','ScreenSolver','Exports')][string[]]$Component = @('All'),
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'build-retention.ps1')
$removed = @(Remove-WorkspaceBuildHistory -Component $Component -Preview:(-not $Apply))
$removed | Select-Object Path, @{Name='MiB'; Expression={[math]::Round($_.Bytes / 1MB, 2)}}, Removed
Write-Host ('{0}: {1} directories, {2:F2} GiB' -f $(if ($Apply) { 'Removed' } else { 'Preview' }), $removed.Count, (($removed | Measure-Object Bytes -Sum).Sum / 1GB))
if ($Component -contains 'All') { Invoke-WorkspaceMaintenance -Logs -Preview:(-not $Apply) | Out-Null }
