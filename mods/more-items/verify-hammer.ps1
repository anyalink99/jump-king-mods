param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King', [switch]$Graphics, [string]$ChargePackage, [string]$CasualPackage)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$internalDir = Join-Path $repoRoot 'build/more-items/_INTERNAL'
$uploadDir = Join-Path $repoRoot 'build/more-items/UPLOAD_TO_WORKSHOP'
$runtime = Join-Path $repoRoot 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$dependencies = @((Join-Path $GameDir 'JumpKing.exe'), (Join-Path $GameDir 'MonoGame.Framework.dll'),
    (Join-Path $GameDir 'LanguageJK.dll'), $runtime)
foreach ($file in @($compiler) + $dependencies) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing build input: $file" }
}
New-Item -ItemType Directory -Force -Path $internalDir, $uploadDir | Out-Null
Copy-Item -LiteralPath $dependencies -Destination $internalDir -Force
# The native BT fixture constructs Game1 in memory; its type also references
# SharpDX even when no graphics device is created.
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' | Copy-Item -Destination $internalDir -Force
$references = @($dependencies | ForEach-Object { "/reference:$_" })
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -Recurse | Sort-Object FullName | ForEach-Object FullName)
$assembly = Join-Path $internalDir 'MoreItems.Module.dll'
$resources = @('wood', 'stone' | ForEach-Object {
    '/resource:' + (Join-Path $PSScriptRoot "assets/hammer/hammer-$_-8bit.wav") + ",HammerKing.hammer-$_-8bit.wav"
})
$resources += '/resource:' + (Join-Path $PSScriptRoot 'assets/jetpack-poses.txt') + ',JumpKingJetpack.jetpack-poses.txt'
$resources += '/resource:' + (Join-Path $PSScriptRoot 'assets/audio/jetpack-loop-8bit.wav') + ',JumpKingJetpack.jetpack-loop-8bit.wav'
$tests = Join-Path $internalDir 'HammerKingTests.exe'
$testSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests/Hammer') -Filter '*Tests.cs' | ForEach-Object FullName)
& $compiler /nologo /optimize+ /target:exe /langversion:5 /main:HammerKing.Tests "/out:$tests" $references $resources $sources $testSources
if ($LASTEXITCODE -ne 0) { throw 'Hammer King test compilation failed' }
& $tests
if ($LASTEXITCODE -ne 0) { throw 'Hammer King tests failed' }
& $tests --package (Join-Path $uploadDir 'MoreItems.dll') $assembly
if ($LASTEXITCODE -ne 0) { throw 'Hammer King package verification failed' }
if ($ChargePackage) {
    Copy-Item -LiteralPath (Join-Path $GameDir 'SlimDX.dll'), (Join-Path $GameDir 'Steamworks.NET.dll') -Destination $internalDir -Force
    $chargeArgs = @('--charge', $ChargePackage, (Join-Path $repoRoot 'mods/subframe-charge/lib/0Harmony.dll'))
    if ($CasualPackage) { $chargeArgs += $CasualPackage }
    & $tests @chargeArgs
    if ($LASTEXITCODE -ne 0) { throw 'Hammer King / Subframe Charge composition check failed' }
}
if ($Graphics) {
    & $tests --audio $GameDir
    if ($LASTEXITCODE -ne 0) { throw 'Hammer King native audio check failed' }
    & $tests --equipment
    if ($LASTEXITCODE -ne 0) { throw 'Hammer equipment lifecycle check failed' }
    $preview = Join-Path $internalDir 'HammerPreview.exe'
    & $compiler /nologo /optimize+ /target:exe /langversion:5 /main:HammerKing.Preview "/out:$preview" /reference:System.Windows.Forms.dll $references $resources $sources (Join-Path $PSScriptRoot 'tests/Hammer/Preview.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Hammer King preview compilation failed' }
    & $preview $GameDir (Join-Path $internalDir 'hammer-preview.png')
    if ($LASTEXITCODE -ne 0) { throw 'Hammer King graphics check failed' }
}
Write-Host "[OK] More Items Hammer: physics, equipment and package checks"
