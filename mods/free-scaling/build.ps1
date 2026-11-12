param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King', [switch]$Graphics)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../scripts/build-stages.ps1')
$modStageRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$modStageLease = Enter-ModBuild $modStageRoot 'free-scaling'
try {
$scaleRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$scaleBuild = Join-Path $scaleRepo 'build/free-scaling/_INTERNAL'
$scalePackage = Join-Path $scaleRepo 'build/free-scaling/UPLOAD_TO_WORKSHOP'
# fixed check output is overwritten; no per-run game copies or unbounded history
$scaleChecks = Join-Path $scaleRepo 'build/_work/free-scaling/checks'
$scaleCompiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$scaleRuntime = Join-Path $scaleRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$scaleHarmony = Join-Path $scaleRepo 'mods/subframe-charge/lib/0Harmony.dll'
$scaleDependencies = @("$GameDir/JumpKing.exe", "$GameDir/MonoGame.Framework.dll", "$GameDir/LanguageJK.dll", "$GameDir/Steamworks.NET.dll", $scaleRuntime, $scaleHarmony)
foreach ($scaleFile in $scaleDependencies) { if (-not (Test-Path -LiteralPath $scaleFile)) { throw "Missing build dependency: $scaleFile" } }
New-Item -ItemType Directory -Force $scaleBuild,$scalePackage,$scaleChecks | Out-Null
$scaleRefs = @($scaleDependencies | ForEach-Object { "/reference:$_" }) + @('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll')
$scaleSources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$scaleAssembly = Join-Path $scaleBuild 'FreeScaling.Module.dll'
Invoke-ModCompiler $scaleCompiler /nologo /target:library /langversion:5 /optimize+ /nowarn:1685 "/out:$scaleAssembly" $scaleRefs $scaleSources
if ($LASTEXITCODE -ne 0) { throw 'Graphics+ compilation failed' }
$scaleTests = @(Get-ChildItem (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | ForEach-Object FullName)
Invoke-ModCompiler $scaleCompiler /nologo /target:exe /platform:x64 /langversion:5 /optimize+ /nowarn:1685 "/out:$scaleChecks/Tests.exe" $scaleRefs $scaleSources $scaleTests
if ($LASTEXITCODE -ne 0) { throw 'Graphics+ test compilation failed' }
Copy-Item -LiteralPath $scaleDependencies -Destination $scaleChecks -Force
Get-ChildItem $GameDir -Filter 'SharpDX*.dll' | Copy-Item -Destination $scaleChecks -Force
& "$scaleChecks/Tests.exe" $scaleChecks $(if ($Graphics) { '--graphics' } else { '--core' })
if ($LASTEXITCODE -ne 0) { throw 'Graphics+ tests failed' }
& (Join-Path $scaleRepo 'mods/jk-runtime/sdk/package.ps1') -Implementation $scaleAssembly -Output (Join-Path $scalePackage 'FreeScaling.dll') -GameDir $GameDir -References @($scaleHarmony)
if (-not $?) { throw 'Graphics+ packaging failed' }
Copy-Item -LiteralPath $scaleHarmony,(Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'CHANGELOG.md'),(Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') -Destination $scalePackage -Force
Invoke-ModCompiler $scaleCompiler /nologo /target:exe /langversion:5 "/out:$scaleChecks/PackageTests.exe" "/reference:$GameDir/JumpKing.exe" (Join-Path $scaleRepo 'mods/jk-runtime/tests/PackageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Package test compilation failed' }
& "$scaleChecks/PackageTests.exe" $scaleRuntime (Join-Path $scalePackage 'FreeScaling.dll')
if ($LASTEXITCODE -ne 0) { throw 'Graphics+ native discovery failed' }
$scaleDlls = @(Get-ChildItem $scalePackage -Filter '*.dll' -Recurse)
if ($scaleDlls.Count -ne 2 -or @($scaleDlls | Where-Object Name -notin @('FreeScaling.dll','0Harmony.dll')).Count) { throw 'Unexpected Graphics+ release dependencies' }
Write-Host "[OK] Graphics+ package: $scalePackage"
} finally { $modStageLease.ReleaseMutex(); $modStageLease.Dispose() }
