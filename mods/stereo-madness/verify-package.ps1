param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$Map = Join-Path $RepoRoot 'build/stereo-madness/UPLOAD_TO_WORKSHOP'
$Runtime = Join-Path $RepoRoot 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$Internal = Join-Path $RepoRoot 'build/stereo-madness-mod/_INTERNAL/map-package-check'
New-Item -ItemType Directory -Force -Path $Internal | Out-Null
foreach ($Name in @('JumpKing.exe','MonoGame.Framework.dll','LanguageJK.dll','Steamworks.NET.dll')) {
    Copy-Item -LiteralPath (Join-Path $GameDir $Name) -Destination $Internal -Force
}
Copy-Item -LiteralPath $Runtime -Destination $Internal -Force
Copy-Item -LiteralPath (Join-Path $RepoRoot 'mods/subframe-charge/lib/0Harmony.dll') -Destination $Internal -Force
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $Internal -Force
$Test = Join-Path $Internal 'MapPackageTests.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /platform:x64 /langversion:5 "/out:$Test" "/reference:$Runtime" "/reference:$GameDir/JumpKing.exe" "/reference:$GameDir/MonoGame.Framework.dll" (Join-Path $RepoRoot 'mods/jk-runtime/tests/MapPackageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Map package fixture compilation failed' }
& $Test (Join-Path $Internal 'JKRuntime.dll') $Map (Join-Path $RepoRoot 'build/mega-mapping-expansion/UPLOAD_TO_WORKSHOP/MegaMappingExpansion.dll') (Join-Path $Internal '0Harmony.dll')
if ($LASTEXITCODE -ne 0) { throw 'Embedded map package discovery failed' }
& (Join-Path $RepoRoot 'scripts/check-worldsmith-package.ps1') -LevelRoot $Map
if ($LASTEXITCODE -ne 0) { throw 'Worldsmith map classification failed' }
