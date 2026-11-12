param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King', [switch]$Graphics, [string]$ChargePackage)
$ErrorActionPreference = 'Stop'
Write-Host '[INFO] Hammer is part of More Items; building the integrated package.'
& (Join-Path $PSScriptRoot '../more-items/build.ps1') -GameDir $GameDir -Graphics:$Graphics -ChargePackage $ChargePackage
