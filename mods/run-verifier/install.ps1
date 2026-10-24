param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot '../jk-runtime/install-release.ps1') -GameDir $GameDir -Include run-verifier,replays -OnlyRequested
