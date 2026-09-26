$ErrorActionPreference = 'Stop'
& python (Join-Path $PSScriptRoot 'tools\prepare_preview.py')
if ($LASTEXITCODE -ne 0) { throw 'Wardrobe+ preview preparation failed.' }
& (Join-Path $PSScriptRoot 'verify-preview.ps1')
try {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'workshop-preview-256.png') -Destination (Join-Path $PSScriptRoot 'workshop-preview.png') -Force
} catch {
    Write-Warning 'The canonical preview is open in another application. The validated workshop-preview-256.png is ready; close the viewer and rerun to update the alias.'
}
