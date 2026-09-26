param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
& python (Join-Path $PSScriptRoot 'tools\render_preview.py') --game-dir $GameDir
if ($LASTEXITCODE -ne 0) { throw 'JK Runtime Workshop preview generation failed' }
