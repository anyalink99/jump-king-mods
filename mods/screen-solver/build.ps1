param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$VerticalWindDll = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\1061090\3437222016\VerticalWindMod.dll',
    [string]$WorkshopDir = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\1061090'
)
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$taskOutput = Join-Path $taskRepo 'build\screen-solver\_INTERNAL'
$taskRuntime = Join-Path $taskRepo 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
$taskCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskRefs = @("/reference:$taskRuntime", "/reference:$GameDir\JumpKing.exe", "/reference:$GameDir\MonoGame.Framework.dll", "/reference:$GameDir\LanguageJK.dll")
$taskSources = @(Get-ChildItem -LiteralPath "$PSScriptRoot\src" -Filter '*.cs' -Recurse | ForEach-Object FullName)
Copy-Item -LiteralPath $taskRuntime,"$GameDir\JumpKing.exe","$GameDir\MonoGame.Framework.dll","$GameDir\LanguageJK.dll" -Destination $taskOutput -Force
$taskHarmony = "$GameDir\Content\JKMods\0Harmony.dll"
Copy-Item -LiteralPath $taskHarmony -Destination $taskOutput -Force
# Harmony imports native presentation method metadata for the installed-mod
# fixtures. These dependencies stay in the headless test directory.
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $taskOutput -Force
Copy-Item -LiteralPath "$GameDir\Steamworks.NET.dll" -Destination $taskOutput -Force
Copy-Item -LiteralPath "$GameDir\Content\level.xnb" -Destination $taskOutput -Force
$taskTests = @(Get-ChildItem -LiteralPath "$PSScriptRoot\tests" -Filter '*.cs' -Recurse | ForEach-Object FullName)
if (-not (Test-Path -LiteralPath $VerticalWindDll -PathType Leaf)) { throw 'Vertical Wind parity fixture is required; supply -VerticalWindDll.' }
if ((Get-FileHash -LiteralPath $VerticalWindDll -Algorithm SHA256).Hash -ne '425A617B79FBA61BA40D39E70F264C5CEB32E069C79330ED3F451276F07A976D') {
    throw 'Vertical Wind fixture changed. Audit it before updating adapter coverage.'
}
Copy-Item -LiteralPath $VerticalWindDll -Destination $taskOutput -Force
$taskFixtures = @(
    @('3140151035\JumpKingPlus.dll', 'AC98BBB0B1EC70FF447DBB4592EFE7D921A6F0733A05F107970F6A6BDC8B5BD9'),
    @('3168137234\HighGravityBlockMod.dll', '43047F7656020FFE8990E7CCD36DC670B019185E3842145A3F8703F884923867'),
    @('3168137234\SampleJkMod.dll', '71D354376FDA88C515E41B74448833837C813D1D358E6F43857F11B295CAA123'),
    @('3414190327\JumpKing-UpsideDownBlocks.dll', 'ADD7B7D2674BE8F507D6FB22948B085DAC98062F9264EE716AD2C9E65AB579F7'),
    @('3158935297\JumpKingLastJumpValue.dll', 'E7FD77E35380E6552DF67890063424F2F0963CDC008E9359E880FC42583C0538'),
    @('3545167426\MuteJumpSfxBlock.dll', 'A90E615E3675192F2751484AD7BE46FF6D6DFE6EE9AB5BAF1E1545899F345026'),
    @('3470750355\MoreBlockSizes.dll', '2318F08EDD9D7EA8B039D3271E53C4320CE20280A8897DBF19D4259D009619CF'),
    @('3353090188\ForcedSlopeBlocks.dll', '5AF16339E2D0E20E49569727A78006FC58E20E156B722313F52CCB73E7844C2D'),
    @('3188962826\SwitchBlocks.dll', 'BBD6A1E0DA5653A0082E54ACA40C73CE4FA282D92BDEFA8E50D4BE2F1F599CED'),
    @('3214349391\JumpKing-Expansion-Blocks.dll', 'D2CA8D04D7B444FD7F03B92277F2596F75162F5201A3E3EA1E9ACBCA8F47FEA1'),
    @('3272060739\JumpKing-GhostOfTheImmortalBabeBlocks.dll', 'E4D7EA3E4C9F10082B7D584BE166752BB536C49402100366D94F300C3C83FCAE'),
    @('3276018062\AntiBlocks.dll', 'DC7C5434CC2ECD5B9F1FE666E2FA63F7455DA39DA4B5A092214D9E9FD3DF3040'),
    @('3315144485\MovementControlBlocks.dll', '121CAA1CF6CC5F7FFF75346EE7793B76C631AB6613F3F5362111ED0A4F1AF03D'),
    @('3330536917\ConveyorBlockMod.dll', '9ED9CAF95217D6D44F5AB580A5D118E4EC6A79AE2B3CA048E97ACE13D1709725'),
    @('3410235901\UpSideDownCore.dll', 'AF695A166246D2CD0E597514E5ED9EA4D9E5664F75F6CF5DEAF43677B2287B36'),
    @('3427553081\CustomWindSwitch.dll', '8153F2317E58C7C7E42A963301EC7921E96589CEA72E03B617A5F9CF0D560EDC'),
    @('3779149753\Sprinting.dll', '8B4F46A03F0505DF6437369C446DCBE47EC9BE201345E13B203C332C47B8D3EE')
)
foreach ($taskFixture in $taskFixtures) {
    $taskFixturePath = Join-Path $WorkshopDir $taskFixture[0]
    if ((Get-FileHash -LiteralPath $taskFixturePath -Algorithm SHA256).Hash -ne $taskFixture[1]) { throw "Unaudited parity fixture: $taskFixturePath" }
    Copy-Item -LiteralPath $taskFixturePath -Destination $taskOutput -Force
}
# Extract coordinated first-party build bytes for capture-contract tests.
# The generated pins enter the binary only if its native conformance tests pass.
# Never learn acceptable IDs from arbitrary installed modules at runtime.
# Do not initialize their entry points, input samplers, graphics or settings.
$taskPackages = @(
    "$taskRepo\build\mega-gameplay-expansion\UPLOAD_TO_WORKSHOP\MegaGameplayExpansion.dll",
    "$taskRepo\build\subframe-charge\UPLOAD_TO_WORKSHOP\SubframeCharge.dll",
    "$taskRepo\build\more-items\UPLOAD_TO_WORKSHOP\MoreItems.dll",
    "$taskRepo\build\replays\UPLOAD_TO_WORKSHOP\Replays.dll",
    "$taskRepo\build\morph-ball\UPLOAD_TO_WORKSHOP\MorphBall.dll"
)
$taskPins = [Collections.Generic.List[string]]::new()
foreach ($taskPackage in $taskPackages) {
    $taskAssembly = [Reflection.Assembly]::LoadFile($taskPackage)
    $taskStream = $taskAssembly.GetManifestResourceStream('JKRuntime.Module')
    $taskDestination = Join-Path $taskOutput (([IO.Path]::GetFileNameWithoutExtension($taskPackage)) + '.Module.dll')
    $taskBinary = [IO.File]::Create($taskDestination)
    try { $taskStream.CopyTo($taskBinary) } finally { $taskBinary.Dispose(); $taskStream.Dispose() }
    $taskModule = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($taskDestination))
    $taskPins.Add(('{{ "{0}", "{1}" }}' -f $taskModule.GetName().Name, $taskModule.ManifestModule.ModuleVersionId))
}
$taskPinSource = Join-Path $taskOutput 'FirstPartyBuilds.g.cs'
$taskPinText = 'namespace ScreenSolver { internal static class FirstPartyBuilds { internal static readonly System.Collections.Generic.Dictionary<string,string> Values = new System.Collections.Generic.Dictionary<string,string> { ' + ($taskPins -join ',') + ' }; } }'
[IO.File]::WriteAllText($taskPinSource, $taskPinText)
$taskSources += $taskPinSource
& $taskCompiler /nologo /optimize+ /target:library /langversion:5 "/out:$taskOutput\ScreenSolver.Module.dll" $taskRefs $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Screen Solver compilation failed' }
Copy-Item -LiteralPath "$WorkshopDir\3161216998\JumpKingSaveStates.dll","$WorkshopDir\3169568082\JumpKingManager.dll" -Destination $taskOutput -Force
& $taskCompiler /nologo /nowarn:1685 /optimize+ /target:exe /langversion:5 /main:ScreenSolver.Tests "/out:$taskOutput\ScreenSolverTests.exe" "/reference:$taskHarmony" $taskRefs $taskSources $taskTests
if ($LASTEXITCODE -ne 0) { throw 'Screen Solver tests compilation failed' }
& "$taskOutput\ScreenSolverTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Screen Solver tests failed' }
$taskUpload = Join-Path $taskRepo 'build\screen-solver\UPLOAD_TO_WORKSHOP'
New-Item -ItemType Directory -Force -Path $taskUpload | Out-Null
& "$taskRepo\mods\jk-runtime\sdk\package.ps1" -Implementation "$taskOutput\ScreenSolver.Module.dll" -Output "$taskUpload\ScreenSolver.dll" -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Screen Solver packaging failed' }
Write-Host '[OK] Screen Solver gameplay package and focused verification'
