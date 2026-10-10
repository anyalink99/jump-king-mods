param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'verify-inspector.ps1') -GameDir $GameDir -Integration -Graphics
