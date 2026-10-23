param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
& (Join-Path $taskRepo 'mods\jk-runtime\install-release.ps1') -GameDir $GameDir -Include 'replays'
