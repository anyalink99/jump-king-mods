param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King', [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$MappingApi = Join-Path $RepoRoot 'build/mega-mapping-expansion/UPLOAD_TO_WORKSHOP/MegaMappingApi.dll'
$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$Internal = Join-Path $RepoRoot 'build/stereo-madness-mod/_INTERNAL'
$Output = Join-Path $RepoRoot 'build/stereo-madness-mod/UPLOAD_TO_WORKSHOP'
$Runtime = Join-Path $RepoRoot 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$Harmony = Join-Path $RepoRoot 'mods/subframe-charge/lib/0Harmony.dll'
New-Item -ItemType Directory -Force -Path $Internal, $Output | Out-Null
Copy-Item -LiteralPath $Runtime -Destination $Internal -Force
$Sources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName)
$Assembly = Join-Path $Internal 'StereoMadness.Module.dll'
& $Compiler /nologo /optimize+ /target:library /langversion:5 "/out:$Assembly" /reference:System.Xml.Linq.dll "/reference:$GameDir/JumpKing.exe" "/reference:$GameDir/MonoGame.Framework.dll" "/reference:$GameDir/Steamworks.NET.dll" "/reference:$GameDir/LanguageJK.dll" "/reference:$Runtime" "/reference:$Harmony" "/reference:$MappingApi" $Sources
if ($LASTEXITCODE -ne 0) { throw 'Stereo Madness compilation failed' }
if (-not $SkipTests) {
    $Test = Join-Path $Internal 'SimulationTests.exe'
    & $Compiler /nologo /optimize+ /target:exe /langversion:5 "/out:$Test" /reference:System.Xml.Linq.dll (Join-Path $PSScriptRoot 'src/Simulation.cs') (Join-Path $PSScriptRoot 'tests/SimulationTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Simulation test compilation failed' }
    & $Test
    if ($LASTEXITCODE -ne 0) { throw 'Simulation checks failed' }
}
Copy-Item -LiteralPath $Harmony, $MappingApi -Destination $Output -Force
Copy-Item -LiteralPath $MappingApi -Destination $Internal -Force
& (Join-Path $RepoRoot 'mods/jk-runtime/sdk/package.ps1') -Implementation $Assembly -Output (Join-Path $Output 'StereoMadness.dll') -GameDir $GameDir -References @($Harmony, $MappingApi)
Copy-Item (Join-Path $PSScriptRoot 'README.md') $Output -Force
Copy-Item (Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') $Output -Force
Copy-Item -LiteralPath (Join-Path $GameDir 'JumpKing.exe'), (Join-Path $GameDir 'MonoGame.Framework.dll') -Destination $Internal -Force
$PackageTest = Join-Path $Internal 'PackageTests.exe'
& $Compiler /nologo /target:exe /langversion:5 "/out:$PackageTest" "/reference:$GameDir/JumpKing.exe" (Join-Path $RepoRoot 'mods/jk-runtime/tests/PackageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Package fixture compilation failed' }
& $PackageTest (Join-Path $Internal 'JKRuntime.dll') (Join-Path $Output 'StereoMadness.dll')
if ($LASTEXITCODE -ne 0) { throw 'Package discovery failed' }
Write-Host '[OK] Native Stereo Madness package built'
