param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$MultiplayerDir
)
$ErrorActionPreference = 'Stop'
$labRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $MultiplayerDir) { $MultiplayerDir = Join-Path (Split-Path (Split-Path $GameDir)) 'workshop/content/1061090/3190590114' }
$labBuild = Join-Path $labRepo 'build/multiplayer-expansion/LAB'
$labInternal = Join-Path $labRepo 'build/multiplayer-expansion/_INTERNAL'
$labCompiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$labDependencies = @("$GameDir/JumpKing.exe", "$GameDir/MonoGame.Framework.dll", "$GameDir/Steamworks.NET.dll", "$MultiplayerDir/JumpKingMultiplayer.dll", "$labRepo/mods/subframe-charge/lib/0Harmony.dll")
foreach ($labFile in $labDependencies) { if (-not (Test-Path -LiteralPath $labFile)) { throw "Missing lab dependency: $labFile" } }
New-Item -ItemType Directory -Force $labBuild,$labInternal | Out-Null
$labRefs = @($labDependencies | ForEach-Object { "/reference:$_" })
$labSources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$labSources += Join-Path $labRepo 'mods/jk-runtime/sdk/BoundedTextLog.cs'
& $labCompiler /nologo /target:winexe /platform:x64 /langversion:5 /optimize+ /nowarn:1685 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$labInternal/MultiplayerExpansion.exe" $labRefs $labSources
if ($LASTEXITCODE -ne 0) { throw 'Multiplayer Expansion compilation failed' }
& $labCompiler /nologo /target:library /platform:x64 /langversion:5 /optimize+ /nowarn:1685 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$labInternal/MultiplayerExpansion.dll" $labRefs $labSources
if ($LASTEXITCODE -ne 0) { throw 'Multiplayer Expansion native mod compilation failed' }
Copy-Item $labDependencies $labInternal -Force
Copy-Item "$GameDir/LanguageJK.dll","$MultiplayerDir/Newtonsoft.Json.dll" $labInternal -Force
Copy-Item (Join-Path $PSScriptRoot 'MultiplayerExpansion.exe.config') "$labInternal/ContractTests.exe.config" -Force
Get-ChildItem $GameDir -Filter 'SharpDX*.dll' | Copy-Item -Destination $labInternal -Force
$labTests = @(Get-ChildItem (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | ForEach-Object FullName)
& $labCompiler /nologo /target:exe /main:MultiplayerExpansion.ContractTests /platform:x64 /langversion:5 /optimize+ /nowarn:1685 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$labInternal/ContractTests.exe" $labRefs $labSources $labTests
if ($LASTEXITCODE -ne 0) { throw 'Lab compatibility test compilation failed' }
$labTestSession = Join-Path $labRepo ('build/_work/multiplayer-expansion/contracts/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $labTestSession | Out-Null
$labRuntime = Join-Path (Split-Path $MultiplayerDir) '3793086563/JKRuntime.dll'
$labPreviousTestRuntime = $env:MPEX_TEST_RUNTIME
try {
    if (Test-Path -LiteralPath $labRuntime) { $env:MPEX_TEST_RUNTIME = $labRuntime }
    & "$labInternal/ContractTests.exe" $labTestSession
} finally { $env:MPEX_TEST_RUNTIME = $labPreviousTestRuntime }
if ($LASTEXITCODE -ne 0) { throw 'Lab and installed Multiplayer hook compatibility failed' }
& (Join-Path $PSScriptRoot 'tests/Staging.Tests.ps1')
if (-not $?) { throw 'Lab staging regression failed' }
& (Join-Path $PSScriptRoot 'tests/Retention.Tests.ps1')
if (-not $?) { throw 'Lab retention regression failed' }
Copy-Item "$labInternal/MultiplayerExpansion.exe" $labBuild -Force
Copy-Item "$labInternal/MultiplayerExpansion.dll" $labBuild -Force
Copy-Item (Join-Path $PSScriptRoot 'stage-native.ps1') $labBuild -Force
Copy-Item (Join-Path $PSScriptRoot 'README.md') $labBuild -Force
Copy-Item (Join-Path $PSScriptRoot 'CHANGELOG.md') $labBuild -Force
Copy-Item (Join-Path $PSScriptRoot 'start-lab.ps1') $labBuild -Force
Copy-Item (Join-Path $PSScriptRoot 'stage-lab.ps1') $labBuild -Force
Copy-Item "$labRepo/mods/subframe-charge/lib/0Harmony.dll" $labBuild -Force
Copy-Item (Join-Path $PSScriptRoot 'MultiplayerExpansion.exe.config') $labBuild -Force
Copy-Item (Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') $labBuild -Force
Copy-Item "$labRepo/mods/subframe-charge/THIRD_PARTY_NOTICES.md" "$labBuild/Harmony-LICENSE.txt" -Force
Write-Host "[OK] Multiplayer Expansion lab: $labBuild"
