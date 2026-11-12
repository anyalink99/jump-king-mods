param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [switch]$Graphics,
    [string]$ChargePackage,
    [string]$CasualPackage
)

$ErrorActionPreference = 'Stop'
$ModRoot = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $ModRoot '..\..')).Path
& (Join-Path $ModRoot 'build.ps1') -GameDir $GameDir -Graphics:$Graphics -ChargePackage $ChargePackage -CasualPackage $CasualPackage
if ($LASTEXITCODE -ne 0) { throw "More Items build failed with exit code $LASTEXITCODE" }

$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$Output = Join-Path $RepoRoot 'build\more-items\API_TEST\MoreItemsExample.Module.dll'
New-Item -ItemType Directory -Force -Path (Split-Path $Output) | Out-Null
& $Compiler /nologo /target:library /langversion:5 `
    "/out:$Output" `
    "/reference:$(Join-Path $GameDir 'JumpKing.exe')" `
    "/reference:$(Join-Path $GameDir 'MonoGame.Framework.dll')" `
    "/reference:$(Join-Path $RepoRoot 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll')" `
    "/reference:$(Join-Path $RepoRoot 'build\more-items\_INTERNAL\MoreItems.Module.dll')" `
    (Join-Path $ModRoot 'examples\ExampleItem.cs')
if ($LASTEXITCODE -ne 0) { throw "More Items example compilation failed with exit code $LASTEXITCODE" }
& (Join-Path $RepoRoot 'mods\jk-runtime\sdk\package.ps1') -Implementation $Output -Output (Join-Path (Split-Path $Output) 'MoreItemsExample.dll') -GameDir $GameDir -References (Join-Path $RepoRoot 'build\more-items\_INTERNAL\MoreItems.Module.dll')
Write-Host "[OK] More Items public example: $Output"
