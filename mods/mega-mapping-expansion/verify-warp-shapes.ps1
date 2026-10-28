param([string]$GameDir='C:\Program Files (x86)\Steam\steamapps\common\Jump King', [string]$JumpKingPlus, [switch]$Optional)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if(-not $JumpKingPlus){
    $workshop=Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop/content/1061090'
    $JumpKingPlus=Join-Path $workshop '3140151035/JumpKingPlus.dll'
}
if(-not (Test-Path -LiteralPath $JumpKingPlus)){
    if($Optional){Write-Host '[SKIP] JK+ is not installed.';return}
    throw 'Pass -JumpKingPlus with the installed provider DLL.'
}
$internal=Join-Path $repo 'build/mega-mapping-expansion/_INTERNAL'
& (Join-Path $internal 'MegaMappingExpansionTests.exe') $internal (Join-Path $PSScriptRoot 'examples/minimal') warp-shapes $JumpKingPlus
if($LASTEXITCODE -ne 0){throw 'Native JK+ slope behavior failed.'}
