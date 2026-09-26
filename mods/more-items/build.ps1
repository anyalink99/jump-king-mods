param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [switch]$SkipTests,
    [switch]$Graphics,
    [string]$ChargePackage,
    [string]$CasualPackage
)

$ErrorActionPreference = 'Stop'
$ModRoot = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $ModRoot '..\..')).Path
$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$JumpKing = Join-Path $GameDir 'JumpKing.exe'
$MonoGame = Join-Path $GameDir 'MonoGame.Framework.dll'
$Language = Join-Path $GameDir 'LanguageJK.dll'
$UIApi = Join-Path $RepoRoot 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
$BuildRoot = Join-Path $RepoRoot 'build\more-items'
$UploadDir = Join-Path $BuildRoot 'UPLOAD_TO_WORKSHOP'
$InternalDir = Join-Path $BuildRoot '_INTERNAL'
$Assembly = Join-Path $InternalDir 'MoreItems.Module.dll'
$Runtime = Join-Path $RepoRoot 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
$PoseData = Join-Path $ModRoot 'assets\jetpack-poses.txt'
$JetpackAudio = Join-Path $ModRoot 'assets\audio\jetpack-loop-8bit.wav'
$HammerResources = @('wood', 'stone' | ForEach-Object {
    '/resource:' + (Join-Path $ModRoot "assets/hammer/hammer-$_-8bit.wav") + ",HammerKing.hammer-$_-8bit.wav"
})

& (Join-Path $RepoRoot 'mods\jk-runtime\build.ps1') -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { throw "JK Runtime dependency build failed with exit code $LASTEXITCODE" }

foreach ($required in @($Compiler, $JumpKing, $MonoGame, $Language, $UIApi, $PoseData, $JetpackAudio)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required build input is missing: $required"
    }
}

New-Item -ItemType Directory -Force -Path $UploadDir, $InternalDir | Out-Null
Copy-Item -LiteralPath $Runtime -Destination $InternalDir -Force
$Sources = Get-ChildItem -LiteralPath (Join-Path $ModRoot 'src') -Filter '*.cs' -File -Recurse |
    Sort-Object Name |
    ForEach-Object FullName

& $Compiler /nologo /optimize+ /target:library /langversion:5 `
    "/out:$Assembly" `
    "/resource:$PoseData,JumpKingJetpack.jetpack-poses.txt" `
    "/resource:$JetpackAudio,JumpKingJetpack.jetpack-loop-8bit.wav" `
    "/reference:$JumpKing" `
    "/reference:$MonoGame" `
    "/reference:$Runtime" `
    "/reference:$Language" `
    "/reference:$UIApi" `
    $HammerResources `
    $Sources
if ($LASTEXITCODE -ne 0) {
    throw "More Items compilation failed with exit code $LASTEXITCODE"
}

if (-not $SkipTests) {
    $TestExecutable = Join-Path $InternalDir 'MoreItemsTests.exe'
    & $Compiler /nologo /optimize+ /target:exe /langversion:5 `
        /main:MoreItemsTests `
        "/out:$TestExecutable" `
        "/resource:$PoseData,JumpKingJetpack.jetpack-poses.txt" `
        "/resource:$JetpackAudio,JumpKingJetpack.jetpack-loop-8bit.wav" `
        "/reference:$JumpKing" `
        "/reference:$MonoGame" `
    "/reference:$Runtime" `
        "/reference:$Language" `
        "/reference:$UIApi" `
        $HammerResources `
        $Sources `
        (Join-Path $ModRoot 'tests\MoreItemsTests.cs')
    if ($LASTEXITCODE -ne 0) {
        throw "More Items test compilation failed with exit code $LASTEXITCODE"
    }
    Copy-Item -LiteralPath $JumpKing, $MonoGame, $Language, $UIApi, (Join-Path $GameDir 'Steamworks.NET.dll') -Destination $InternalDir -Force
    & $TestExecutable
    if ($LASTEXITCODE -ne 0) {
        throw "More Items tests failed with exit code $LASTEXITCODE"
    }

    # Native discovery is checked for the complete release set by
    # jk-runtime/verify-packages.ps1, after SDK packaging below.
}

$Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Assembly).Hash.ToLowerInvariant()
Write-Host "[OK] More Items: $Assembly"
Write-Host "[OK] SHA-256: $Hash"

& (Join-Path $RepoRoot 'mods\jk-runtime\sdk\package.ps1') -Implementation $Assembly -Output (Join-Path $UploadDir 'MoreItems.dll') -GameDir $GameDir
if (-not $SkipTests) {
    & (Join-Path $ModRoot 'verify-hammer.ps1') -GameDir $GameDir -Graphics:$Graphics -ChargePackage $ChargePackage -CasualPackage $CasualPackage
}
