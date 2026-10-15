param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$wardrobeRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
& (Join-Path $wardrobeRepo 'mods\jk-runtime\install-release.ps1') -GameDir $GameDir -Include 'wardrobe-plus'
