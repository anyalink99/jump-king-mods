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
$labDependencies += Join-Path $labRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
foreach ($labFile in $labDependencies) { if (-not (Test-Path -LiteralPath $labFile)) { throw "Missing lab dependency: $labFile" } }
New-Item -ItemType Directory -Force $labBuild,$labInternal | Out-Null
$labRefs = @($labDependencies | ForEach-Object { "/reference:$_" })
$labSources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$labSources += Join-Path $labRepo 'mods/jk-runtime/sdk/BoundedTextLog.cs'
& $labCompiler /nologo /target:winexe /platform:x64 /langversion:5 /optimize+ /reference:System.Windows.Forms.dll "/reference:$GameDir/JumpKing.exe" "/out:$labInternal/MultiplayerExpansion.Client.exe" (Join-Path $PSScriptRoot 'client-bootstrap/Entry.cs') (Join-Path $PSScriptRoot 'src/Version.cs')
if ($LASTEXITCODE -ne 0) { throw 'Multiplayer Expansion compilation failed' }
Copy-Item "$labInternal/MultiplayerExpansion.Client.exe" "$labInternal/MultiplayerExpansion.exe" -Force
& $labCompiler /nologo /target:library /platform:x64 /langversion:5 /optimize+ /nowarn:1685 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$labInternal/MultiplayerExpansion.Module.dll" $labRefs $labSources
if ($LASTEXITCODE -ne 0) { throw 'Multiplayer Expansion native mod compilation failed' }
Copy-Item $labDependencies $labInternal -Force
Copy-Item "$GameDir/LanguageJK.dll","$MultiplayerDir/Newtonsoft.Json.dll" $labInternal -Force
Copy-Item (Join-Path $PSScriptRoot 'MultiplayerExpansion.exe.config') "$labInternal/ContractTests.exe.config" -Force
& $labCompiler /nologo /target:exe /platform:x64 /langversion:5 /optimize+ /nowarn:1685 /reference:System.Xml.Linq.dll /reference:Microsoft.CSharp.dll "/out:$labInternal/PackageBuilder.exe" $labRefs (Join-Path $labRepo 'mods/jk-runtime/sdk/PackageBuilder.cs')
if ($LASTEXITCODE -ne 0) { throw 'World package tool compilation failed' }
# native discovery sees only game types; Runtime loads the implementation afterwards
& "$labInternal/PackageBuilder.exe" "$labInternal/MultiplayerExpansion.Module.dll" "$labInternal/MultiplayerExpansion.dll" $GameDir "$labInternal/0Harmony.dll" "$MultiplayerDir/JumpKingMultiplayer.dll" "$MultiplayerDir/Newtonsoft.Json.dll"
if ($LASTEXITCODE -ne 0) { throw 'Native module packaging failed' }
Get-ChildItem $GameDir -Filter 'SharpDX*.dll' | Copy-Item -Destination $labInternal -Force
$labTests = @(Get-ChildItem (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | ForEach-Object FullName)
& $labCompiler /nologo /target:exe /main:MultiplayerExpansion.ContractTests /platform:x64 /langversion:5 /optimize+ /nowarn:1685 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$labInternal/ContractTests.exe" $labRefs $labSources $labTests
if ($LASTEXITCODE -ne 0) { throw 'Lab compatibility test compilation failed' }
$labTestSession = Join-Path $labRepo ('build/_work/multiplayer-expansion/contracts/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $labTestSession | Out-Null
$labRuntime = Join-Path $labRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$labPreviousTestRuntime = $env:MPEX_TEST_RUNTIME
$labPreviousSwitchFixture = $env:MPEX_TEST_SWITCH_BLOCKS
try {
    if (Test-Path -LiteralPath $labRuntime) { $env:MPEX_TEST_RUNTIME = $labRuntime }
    $env:MPEX_TEST_SWITCH_BLOCKS = Join-Path (Split-Path $MultiplayerDir) "3188962826/SwitchBlocks.dll"
    & "$labInternal/ContractTests.exe" $labTestSession
} finally { $env:MPEX_TEST_RUNTIME = $labPreviousTestRuntime; $env:MPEX_TEST_SWITCH_BLOCKS = $labPreviousSwitchFixture }
if ($LASTEXITCODE -ne 0) { throw 'Lab and installed Multiplayer hook compatibility failed' }
& (Join-Path $PSScriptRoot 'tests/Staging.Tests.ps1')
if (-not $?) { throw 'Lab staging regression failed' }
& (Join-Path $PSScriptRoot 'tests/Retention.Tests.ps1')
if (-not $?) { throw 'Lab retention regression failed' }
Copy-Item "$labInternal/MultiplayerExpansion.exe" $labBuild -Force
Copy-Item "$labInternal/MultiplayerExpansion.dll" $labBuild -Force
$oldWorldShell = Join-Path $labBuild 'MultiplayerExpansion.World.dll'
if (Test-Path -LiteralPath $oldWorldShell) {
    Move-Item -LiteralPath $oldWorldShell -Destination (Join-Path $labInternal ('retired-world-' + [Guid]::NewGuid().ToString('N') + '.dll'))
}
Copy-Item (Join-Path $PSScriptRoot 'stage-native.ps1') $labBuild -Force
Copy-Item (Join-Path $PSScriptRoot 'README.md') $labBuild -Force
Copy-Item (Join-Path $PSScriptRoot 'CHANGELOG.md') $labBuild -Force
New-Item -ItemType Directory -Force (Join-Path $labBuild 'docs') | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'docs/*') (Join-Path $labBuild 'docs') -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'start-lab.ps1') $labBuild -Force
Copy-Item (Join-Path $PSScriptRoot 'stage-lab.ps1') $labBuild -Force
Copy-Item "$labRepo/mods/subframe-charge/lib/0Harmony.dll" $labBuild -Force
Copy-Item (Join-Path $PSScriptRoot 'MultiplayerExpansion.exe.config') $labBuild -Force
Copy-Item (Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') $labBuild -Force
Copy-Item "$labRepo/mods/subframe-charge/THIRD_PARTY_NOTICES.md" "$labBuild/Harmony-LICENSE.txt" -Force
$release = Join-Path $labRepo ('build/_work/multiplayer-expansion/package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $release | Out-Null
# stage only release inputs, never scoop up settings or test sessions
foreach ($name in @('MultiplayerExpansion.exe','MultiplayerExpansion.dll',
    'MultiplayerExpansion.exe.config','0Harmony.dll','stage-native.ps1','stage-lab.ps1',
    'README.md','CHANGELOG.md','THIRD_PARTY_NOTICES.md','Harmony-LICENSE.txt','docs')) {
    Copy-Item -LiteralPath (Join-Path $labBuild $name) -Destination $release -Recurse
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'workshop-preview.png'),(Join-Path $PSScriptRoot 'assets'),(Join-Path $PSScriptRoot 'WORKSHOP.md') -Destination $release -Recurse
& (Join-Path $PSScriptRoot 'tests/Release.Tests.ps1') -Package $release
if (-not $?) { throw 'Release package validation failed' }
$discoveryRoot = Join-Path $labRepo ('build/_work/multiplayer-expansion/discovery-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $discoveryRoot | Out-Null
$discoveryExe = Join-Path $discoveryRoot 'ColdDiscoveryTests.exe'
& $labCompiler /nologo /target:exe /platform:x64 /langversion:5 "/out:$discoveryExe" "/reference:$GameDir/JumpKing.exe" (Join-Path $PSScriptRoot 'tests/ColdDiscoveryTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Cold discovery test compilation failed' }
Copy-Item -LiteralPath "$GameDir/JumpKing.exe","$GameDir/MonoGame.Framework.dll" -Destination $discoveryRoot
Push-Location $discoveryRoot
try { & $discoveryExe $release $GameDir $MultiplayerDir $labRuntime }
finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw 'Cold native discovery or SDK activation failed' }
$releaseFinal = Join-Path $labRepo 'build/multiplayer-expansion/UPLOAD_TO_WORKSHOP'
if (Test-Path -LiteralPath $releaseFinal) {
    Move-Item -LiteralPath $releaseFinal -Destination (Join-Path $labInternal ('previous-release-' + [Guid]::NewGuid().ToString('N')))
}
Move-Item -LiteralPath $release -Destination $releaseFinal
Write-Host "[OK] Multiplayer Expansion release: $releaseFinal"
Write-Host "[OK] Multiplayer Expansion lab: $labBuild"
