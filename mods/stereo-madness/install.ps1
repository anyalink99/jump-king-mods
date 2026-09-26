param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
if (Get-Process -Name JumpKing -ErrorAction SilentlyContinue) { throw 'Close Jump King before installing the map.' }
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$GameRoot = (Resolve-Path -LiteralPath $GameDir).Path
$Map = (Resolve-Path (Join-Path $RepoRoot 'build/stereo-madness/UPLOAD_TO_WORKSHOP')).Path
foreach ($Required in @('jk-runtime/modules/stereo-madness/StereoMadness.jkmod','level_settings.xml','props/stereo-madness/course.xml')) {
    if (-not (Test-Path -LiteralPath (Join-Path $Map $Required))) { throw 'Build the complete matching map package first.' }
}
$Local = Join-Path $GameRoot 'Content/JKMods'
$Target = [IO.Path]::GetFullPath((Join-Path $Local 'StereoMadnessMap'))
$LocalMap = $Target
$DebugLink = Join-Path $Local 'StereoMadnessMapDebug'
$Workshop = Join-Path (Split-Path (Split-Path $GameRoot -Parent) -Parent) 'workshop/content/1061090'
if (Test-Path -LiteralPath $Workshop) {
    $Matches = @(Get-ChildItem -LiteralPath $Workshop -Directory | Where-Object {
        (Test-Path -LiteralPath (Join-Path $_.FullName 'props/stereo-madness/course.xml')) -and (
            (Test-Path -LiteralPath (Join-Path $_.FullName 'StereoMadness.dll')) -or
            (Test-Path -LiteralPath (Join-Path $_.FullName 'jk-runtime/modules/stereo-madness/StereoMadness.jkmod')))
    })
    if ($Matches.Count -gt 1) { throw 'Multiple Workshop copies of Stereo Madness; resolve duplicate subscriptions first.' }
    if ($Matches.Count -eq 1) { $Target = [IO.Path]::GetFullPath($Matches[0].FullName) }
}
if (-not $Target.StartsWith($GameRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
    -not $Target.StartsWith([IO.Path]::GetFullPath($Workshop) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid installation target.' }
if (Test-Path -LiteralPath $DebugLink) {
    $Current = Get-Item -LiteralPath $DebugLink
    $Expected = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'build/stereo-madness/PLAYTEST'))
    if ($Current.LinkType -ne 'Junction' -or [IO.Path]::GetFullPath([string]$Current.Target) -ne $Expected) { throw 'Unexpected debug junction; leaving it unchanged.' }
}
foreach ($ExistingMap in @($Target, $LocalMap) | Select-Object -Unique) {
    if (-not (Test-Path -LiteralPath $ExistingMap)) { continue }
    if ((Get-Item -LiteralPath $ExistingMap).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unexpected map target link.' }
    if (-not (Test-Path -LiteralPath (Join-Path $ExistingMap 'props/stereo-madness/course.xml'))) { throw 'Unrecognized existing map folder.' }
}
$Backup = Join-Path $RepoRoot ('build/stereo-madness/_INTERNAL/local-install-' + [Guid]::NewGuid().ToString('N'))
$Stage = Join-Path $GameRoot ('Content/StereoMadnessStage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Stage, $Backup, $Local -Force | Out-Null
Get-ChildItem -LiteralPath $Map -Force | Copy-Item -Destination $Stage -Recurse -Force
foreach ($Source in Get-ChildItem -LiteralPath $Map -Recurse -File) {
    $Relative = $Source.FullName.Substring($Map.Length + 1)
    if ((Get-FileHash -LiteralPath $Source.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $Stage $Relative)).Hash) { throw "Copy verification failed: $Relative" }
}
foreach ($UserFolder in @('Saves','SavesPerma','Replays','ControllerBinds','OverlayPlus','RunVerifier','WardrobePlus')) {
    $ExistingFolder = Join-Path $Target $UserFolder
    if (Test-Path -LiteralPath $ExistingFolder) {
        New-Item -ItemType Directory -Force -Path (Join-Path $Stage $UserFolder) | Out-Null
        Get-ChildItem -LiteralPath $ExistingFolder -Force | Copy-Item -Destination (Join-Path $Stage $UserFolder) -Recurse -Force
    }
}
$Old = Join-Path $Local 'StereoMadness.dll'
@{Target=$Target; Source=$Map; DebugLink=$DebugLink; PreviousDll=$Old; LocalMap=$LocalMap} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Backup 'manifest.json') -Encoding UTF8
$MovedMap = $false; $MovedDll = $false; $RemovedLink = $false; $MovedLocal = $false
try {
    if (Get-Process -Name JumpKing -ErrorAction SilentlyContinue) { throw 'Jump King started during installation; close it and retry.' }
    if (Test-Path -LiteralPath $Target) { Move-Item -LiteralPath $Target -Destination (Join-Path $Backup 'previous-map'); $MovedMap = $true }
    if ($Target -ne $LocalMap -and (Test-Path -LiteralPath $LocalMap)) {
        Move-Item -LiteralPath $LocalMap -Destination (Join-Path $Backup 'previous-local-map'); $MovedLocal = $true
    }
    if (Test-Path -LiteralPath $Old) { Move-Item -LiteralPath $Old -Destination (Join-Path $Backup 'StereoMadness.dll'); $MovedDll = $true }
    if (Test-Path -LiteralPath $DebugLink) {
        # Nonrecursive deletion removes only the verified junction, never its target.
        [IO.Directory]::Delete($DebugLink); $RemovedLink = $true
    }
    Move-Item -LiteralPath $Stage -Destination $Target
} catch {
    if ($MovedMap -and -not (Test-Path -LiteralPath $Target)) { Move-Item -LiteralPath (Join-Path $Backup 'previous-map') -Destination $Target }
    if ($MovedDll) { Move-Item -LiteralPath (Join-Path $Backup 'StereoMadness.dll') -Destination $Old }
    if ($MovedLocal) { Move-Item -LiteralPath (Join-Path $Backup 'previous-local-map') -Destination $LocalMap }
    if ($RemovedLink) { New-Item -ItemType Junction -Path $DebugLink -Target $Expected | Out-Null }
    throw
}
Write-Host ('[OK] Installed: ' + $Target)
Write-Host '[OK] Map package ready. Existing Workshop installation preferred; no map-menu registration installed.'
Write-Host ('[OK] Rollback information: ' + $Backup)
