param([string]$PreviewPath = (Join-Path $PSScriptRoot 'workshop-preview-256.png'))
$ErrorActionPreference = 'Stop'
$previewBytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $PreviewPath).Path)
if ($previewBytes.Length -ge 34KB) { throw 'Workshop preview must be below 34 KiB.' }
if ($previewBytes.Length -lt 33 -or [BitConverter]::ToString($previewBytes[0..7]) -ne '89-50-4E-47-0D-0A-1A-0A') { throw 'Preview is not a PNG.' }
if ($previewBytes[24] -ne 8 -or $previewBytes[25] -ne 3) { throw 'Preview must use an 8-bit indexed palette.' }
Add-Type -AssemblyName System.Drawing
$previewStream = [IO.MemoryStream]::new($previewBytes, $false)
try {
    $previewImage = [Drawing.Image]::FromStream($previewStream)
    try {
        if ($previewImage.Width -ne 256 -or $previewImage.Height -ne 256) { throw 'Preview must be exactly 256x256.' }
    } finally { $previewImage.Dispose() }
} finally { $previewStream.Dispose() }
Write-Host "[OK] Workshop preview: 256x256, 8-bit indexed PNG, $($previewBytes.Length) bytes"
