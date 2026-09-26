param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$package = Join-Path $workspace 'build\mega-mapping-expansion\UPLOAD_TO_WORKSHOP'
$game = (Resolve-Path -LiteralPath $GameDir).Path
$running = Get-Process -Name JumpKing -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $game 'JumpKing.exe') }
if ($running) { throw 'Exit Jump King before installing. This command never closes the game or changes saves.' }
$destination = Join-Path $game 'Content\JKMods'
$workshop = Join-Path (Split-Path -Parent (Split-Path -Parent $game)) 'workshop\content\1061090'
$installed = @()
if (Test-Path -LiteralPath $workshop -PathType Container) {
    $installed = @(Get-ChildItem -LiteralPath $workshop -Directory | Where-Object {
        Test-Path -LiteralPath (Join-Path $_.FullName 'MegaMappingExpansion.dll') -PathType Leaf
    })
}
if ($installed.Count -gt 1 -or ($installed.Count -eq 1 -and (Test-Path -LiteralPath (Join-Path $destination 'MegaMappingExpansion.dll')))) {
    throw 'Multiple active Mapping copies found. Resolve duplicate installations explicitly before installing.'
}
if ($installed.Count -eq 1) { $destination = $installed[0].FullName }
$backup = Join-Path $workspace ('build\local-mod-backups\mega-mapping-install-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$files = @('MegaMappingExpansion.dll','MegaMappingApi.dll','0Harmony.dll','MegaMappingExpansion.THIRD_PARTY_NOTICES.md')
$sources = @{ 'MegaMappingExpansion.dll'='MegaMappingExpansion.dll'; 'MegaMappingApi.dll'='MegaMappingApi.dll'; '0Harmony.dll'='0Harmony.dll'; 'MegaMappingExpansion.THIRD_PARTY_NOTICES.md'='THIRD_PARTY_NOTICES.md' }
foreach ($name in $files) { if (-not (Test-Path -LiteralPath (Join-Path $package $sources[$name]))) { throw "Build the mod first: missing $($sources[$name])" } }
New-Item -ItemType Directory -Force -Path $destination,$backup | Out-Null
$changed = @()
try {
    foreach ($name in $files) {
        $source = Join-Path $package $sources[$name]; $target = Join-Path $destination $name
        $expected = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        if (Test-Path -LiteralPath $target) {
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $expected) { continue }
            if ($name -eq '0Harmony.dll') { throw 'A different shared Harmony DLL is installed. Resolve that dependency explicitly; it was not overwritten.' }
            Copy-Item -LiteralPath $target -Destination (Join-Path $backup $name)
        }
        $changed += $name
        Copy-Item -LiteralPath $source -Destination $target -Force
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $expected) { throw "Installed hash mismatch: $name" }
    }
    $receipt = foreach ($name in $files) { [pscustomobject]@{ File=$name; SHA256=(Get-FileHash -LiteralPath (Join-Path $destination $name)).Hash } }
    $receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'receipt.json') -Encoding UTF8
    $receipt
    Write-Host "[OK] Installed package; backup/receipt: $backup"
}
catch {
    foreach ($name in $changed) {
        $saved = Join-Path $backup $name; $target = Join-Path $destination $name
        if (Test-Path -LiteralPath $saved) { Copy-Item -LiteralPath $saved -Destination $target -Force }
        elseif (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
    }
    throw
}
