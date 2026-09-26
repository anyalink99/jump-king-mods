param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [switch]$SkipTests,
    [ValidateSet('Fast','Integration','Full')][string]$Tier = 'Fast',
    [string]$ChargeAssembly
)

$ErrorActionPreference = 'Stop'
$ModRoot = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $ModRoot '..\..')).Path
$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$JumpKing = Join-Path $GameDir 'JumpKing.exe'
$MonoGame = Join-Path $GameDir 'MonoGame.Framework.dll'
$Language = Join-Path $GameDir 'LanguageJK.dll'
$BuildRoot = Join-Path $RepoRoot 'build\morph-ball'
$UploadDir = Join-Path $BuildRoot 'UPLOAD_TO_WORKSHOP'
$InternalDir = Join-Path $BuildRoot '_INTERNAL'
$Assembly = Join-Path $InternalDir 'MorphBall.Module.dll'
$Runtime = Join-Path $RepoRoot 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
$TransformAudio = Join-Path $ModRoot 'assets\audio\ball-transform-8bit.wav'
$UntransformAudio = Join-Path $ModRoot 'assets\audio\ball-untransform-8bit.wav'

foreach ($required in @(
    $Compiler,
    $JumpKing,
    $MonoGame,
    $Language,
    $TransformAudio,
    $UntransformAudio
)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required build input is missing: $required"
    }
}

New-Item -ItemType Directory -Force -Path $UploadDir, $InternalDir | Out-Null
Copy-Item -LiteralPath $Runtime -Destination $InternalDir -Force
$Sources = Get-ChildItem -LiteralPath (Join-Path $ModRoot 'src') -Filter '*.cs' -File |
    Sort-Object Name |
    ForEach-Object FullName

& $Compiler /nologo /optimize+ /target:library /langversion:5 `
    "/out:$Assembly" `
    "/resource:$TransformAudio,MorphBallMod.ball-transform-8bit.wav" `
    "/resource:$UntransformAudio,MorphBallMod.ball-untransform-8bit.wav" `
    "/reference:$JumpKing" `
    "/reference:$MonoGame" `
    "/reference:$Runtime" `
    "/reference:$Language" `
    $Sources
if ($LASTEXITCODE -ne 0) {
    throw "Ball King compilation failed with exit code $LASTEXITCODE"
}

if (-not $SkipTests) {
    $ContractTestExecutable = Join-Path $InternalDir 'ContractSmokeTests.exe'
    & $Compiler /nologo /optimize+ /target:exe /langversion:5 `
        "/out:$ContractTestExecutable" `
        "/reference:$JumpKing" `
        "/reference:$MonoGame" `
    "/reference:$Runtime" `
        (Join-Path $ModRoot 'tests\ContractSmokeTests.cs')
    if ($LASTEXITCODE -ne 0) {
        throw "Ball King contract test compilation failed with exit code $LASTEXITCODE"
    }
    Copy-Item -LiteralPath $MonoGame -Destination $InternalDir -Force
    Copy-Item -LiteralPath $JumpKing -Destination $InternalDir -Force
    $SfcAssembly = $ChargeAssembly
    if ($SfcAssembly) {
        if (-not (Test-Path -LiteralPath $SfcAssembly -PathType Leaf)) { throw "Missing explicitly selected SFC fixture: $SfcAssembly" }
        & $ContractTestExecutable $Assembly $SfcAssembly
    } else {
        & $ContractTestExecutable $Assembly
        Write-Host '[SKIP Composition] Select Subframe Charge with Ball, or pass -ChargeAssembly, for their native discovery contract'
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Ball King installed-game contracts failed with exit code $LASTEXITCODE"
    }

    $TestExecutable = Join-Path $InternalDir 'MorphBallTests.exe'
    & $Compiler /nologo /optimize+ /target:exe /langversion:5 /nowarn:0649 `
        "/out:$TestExecutable" `
        "/reference:$JumpKing" `
        "/reference:$MonoGame" `
    "/reference:$Runtime" `
        (Join-Path $ModRoot 'src\MorphAirControlState.cs') `
        (Join-Path $ModRoot 'src\BallKingMapBlocks.cs') `
        (Join-Path $ModRoot 'src\MorphCoyoteJumpWindow.cs') `
        (Join-Path $ModRoot 'src\LevelPermission.cs') `
        (Join-Path $ModRoot 'src\MorphJumpPhysics.cs') `
        (Join-Path $ModRoot 'src\MorphLandingJumpBuffer.cs') `
        (Join-Path $ModRoot 'src\MorphFloorBounceState.cs') `
        (Join-Path $ModRoot 'src\MorphController.State.cs') `
        (Join-Path $ModRoot 'src\BallKingGeometryApi.cs') `
        (Join-Path $ModRoot 'src\MorphPipelineContract.cs') `
        (Join-Path $ModRoot 'src\MorphBlockGeometry.cs') `
        (Join-Path $ModRoot 'src\MorphCollisionWorld.cs') `
        (Join-Path $ModRoot 'src\MorphContourBuilder.cs') `
        (Join-Path $ModRoot 'src\MorphContourGeometry.cs') `
        (Join-Path $ModRoot 'src\MorphContourSegment.cs') `
        (Join-Path $ModRoot 'src\MorphSurfaceFollower.cs') `
        (Join-Path $ModRoot 'src\MorphSlopeContact.cs') `
        (Join-Path $ModRoot 'src\MorphTransition.cs') `
        (Join-Path $ModRoot 'src\OutfitCanvas.cs') `
        (Join-Path $ModRoot 'src\MorphWallBounceState.cs') `
        (Join-Path $ModRoot 'src\SurfaceMotion.cs') `
        (Join-Path $ModRoot 'tests\MorphBallTests.cs') `
        (Join-Path $ModRoot 'tests\MouldingManorGeometryTests.cs')
    if ($LASTEXITCODE -ne 0) {
        throw "Ball King test compilation failed with exit code $LASTEXITCODE"
    }
    if ($Tier -eq 'Fast') { & $TestExecutable }
    else { & $TestExecutable (Join-Path $GameDir 'Content\level.xnb') }
    if ($LASTEXITCODE -ne 0) {
        throw "Ball King tests failed with exit code $LASTEXITCODE"
    }
    if ($Tier -ne 'Fast') {
    Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $InternalDir -Force
    $AudioTests = Join-Path $InternalDir 'PreparedAudioTests.exe'
    & $Compiler /nologo /target:exe /langversion:5 "/out:$AudioTests" "/reference:$JumpKing" "/reference:$MonoGame" "/reference:$Runtime" (Join-Path $ModRoot 'tests/PreparedAudioTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Ball preparation audio fixture compilation failed' }
    & $AudioTests $Assembly
    if ($LASTEXITCODE -ne 0) { throw 'Ball preparation audio fixture failed' }
    } else { Write-Host '[SKIP Integration] Complete Moulding Manor contour grid and native audio' }
}

$Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Assembly).Hash.ToLowerInvariant()
Write-Host "[OK] Ball King: $Assembly"
Write-Host "[OK] SHA-256: $Hash"

& (Join-Path $RepoRoot 'mods\jk-runtime\sdk\package.ps1') -Implementation $Assembly -Output (Join-Path $UploadDir 'MorphBall.dll') -GameDir $GameDir
