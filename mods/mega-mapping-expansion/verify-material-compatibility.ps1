param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$ExpansionBlocks,
    [switch]$Optional
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $ExpansionBlocks) {
    $workshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop/content/1061090'
    $matches = @(if (Test-Path -LiteralPath $workshop) { Get-ChildItem -LiteralPath $workshop -Filter JumpKing-Expansion-Blocks.dll -File -Recurse })
    if ($matches.Count -eq 0 -and $Optional) { Write-Host '[SKIP] Expansion Blocks is not installed.'; return }
    if ($matches.Count -ne 1) { throw 'Pass -ExpansionBlocks with the installed provider DLL path.' }
    $ExpansionBlocks = $matches[0].FullName
}
$test = Join-Path $repo 'build/mega-mapping-expansion/_INTERNAL/MegaMappingExpansionTests.exe'
if (-not (Test-Path -LiteralPath $test)) { throw 'Build Mega Mapping Expansion first.' }
$scratch = Join-Path $repo 'build/_work/mme-material-provider'
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
# one overwritten receipt, no staged game copies or growing fixture history
& $test $scratch (Join-Path $PSScriptRoot 'examples/minimal') material-provider $ExpansionBlocks
if ($LASTEXITCODE -ne 0) { throw 'Installed material-provider compatibility failed.' }
[pscustomobject]@{ Provider=$ExpansionBlocks; SHA256=(Get-FileHash -LiteralPath $ExpansionBlocks -Algorithm SHA256).Hash; Checked=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $scratch 'receipt.json') -Encoding UTF8
$checkpoint = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop/content/1061090/3444736257/CheckpointBlock.dll'
if (Test-Path -LiteralPath $checkpoint) {
    & $test $scratch (Join-Path $PSScriptRoot 'examples/minimal') material-checkpoint $checkpoint
    if ($LASTEXITCODE -ne 0) { throw 'Installed moving-checkpoint compatibility failed.' }
    [pscustomobject]@{ Provider=$checkpoint; SHA256=(Get-FileHash -LiteralPath $checkpoint -Algorithm SHA256).Hash; Checked=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $scratch 'checkpoint-receipt.json') -Encoding UTF8
} else { Write-Host '[SKIP] Checkpoint Block is not installed.' }
