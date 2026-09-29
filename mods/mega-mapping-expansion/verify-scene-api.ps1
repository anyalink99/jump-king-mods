param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$internal = Join-Path $repo 'build/mega-mapping-expansion/_INTERNAL'
$output = Join-Path $internal ('api-package-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null
$runtime = Join-Path $repo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$api = Join-Path $internal 'MegaMappingApi.dll'
$csc = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$module = Join-Path $output 'LanternExample.Module.dll'
& $csc /nologo /target:library /langversion:5 "/out:$module" "/reference:$runtime" "/reference:$api" (Join-Path $PSScriptRoot 'api/ModuleExample.cs')
if ($LASTEXITCODE -ne 0) { throw 'Scene module consumer failed to compile' }
$package = Join-Path $output 'LanternExample.dll'
& (Join-Path $repo 'mods/jk-runtime/sdk/package.ps1') -Implementation $module -Output $package -GameDir $GameDir -References @($api)
if ($LASTEXITCODE -ne 0) { throw 'Scene consumer packaging failed' }
$probe = Join-Path $output 'PackageTests.exe'
& $csc /nologo /target:exe /langversion:5 "/out:$probe" "/reference:$GameDir/JumpKing.exe" (Join-Path $repo 'mods/jk-runtime/tests/PackageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Scene discovery probe compilation failed' }
Copy-Item -LiteralPath "$GameDir/JumpKing.exe","$GameDir/MonoGame.Framework.dll","$GameDir/LanguageJK.dll" -Destination $output
$mapping = Join-Path $repo 'build/mega-mapping-expansion/UPLOAD_TO_WORKSHOP/MegaMappingExpansion.dll'
& $probe $runtime $package $mapping
if ($LASTEXITCODE -ne 0) { throw 'Consumer-first scene contract discovery failed' }
& $probe $runtime $mapping $package
if ($LASTEXITCODE -ne 0) { throw 'Mapping-first scene contract discovery failed' }
Write-Host '[OK] Public scene contract and native package discovery in both orders'
