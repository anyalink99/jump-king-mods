param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$wardrobeRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
# Build the actual consumers first; compatibility must not depend on stale DLLs.
& (Join-Path $wardrobeRepo 'scripts\check-mods.ps1') -Mod @('morph-ball','replays','mega-mapping-expansion','smooth-camera') -GameDir $GameDir
if (-not $?) { throw 'Wardrobe+ consumer builds failed' }
& (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir -Compatibility
if (-not $?) { throw 'Wardrobe+ consumer integration failed' }
