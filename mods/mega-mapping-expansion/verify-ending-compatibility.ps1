param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$MoreEndingOptions,
    [switch]$Optional
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$internal = Join-Path $repo 'build/mega-mapping-expansion/_INTERNAL'
if (-not $MoreEndingOptions) {
    $workshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop/content/1061090'
    $matches = @(if (Test-Path -LiteralPath $workshop) { Get-ChildItem -LiteralPath $workshop -Filter MoreEndingOptions.dll -File -Recurse })
    if ($matches.Count -eq 0 -and $Optional) { Write-Host '[SKIP] MoreEndingOptions is not installed.'; return }
    if ($matches.Count -ne 1) { throw 'Pass -MoreEndingOptions with the installed provider DLL path.' }
    $MoreEndingOptions = $matches[0].FullName
}
$test = Join-Path $internal 'MegaMappingExpansionTests.exe'
if (-not (Test-Path -LiteralPath $test)) { throw 'Build Mega Mapping Expansion first.' }
& $test $internal (Join-Path $PSScriptRoot 'examples/minimal') ending-compatibility $MoreEndingOptions
if ($LASTEXITCODE -ne 0) { throw 'Installed ending-provider compatibility failed.' }
