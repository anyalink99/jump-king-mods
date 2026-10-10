param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$ModRoot = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $ModRoot '..\..')).Path
$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$JumpKing = Join-Path $GameDir 'JumpKing.exe'
$MonoGame = Join-Path $GameDir 'MonoGame.Framework.dll'
$BuildRoot = Join-Path $RepoRoot 'build\casual-jumping'
$UploadDir = Join-Path $BuildRoot 'UPLOAD_TO_WORKSHOP'
$InternalDir = Join-Path $BuildRoot '_INTERNAL'
$Assembly = Join-Path $InternalDir 'CasualJumping.Module.dll'
$Runtime = Join-Path $RepoRoot 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'

foreach ($required in @($Compiler, $JumpKing, $MonoGame)) {
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
    "/reference:$JumpKing" `
    "/reference:$MonoGame" `
    "/reference:$Runtime" `
    $Sources
if ($LASTEXITCODE -ne 0) {
    throw "Casual Jumping compilation failed with exit code $LASTEXITCODE"
}

if (-not $SkipTests) {
    $TestExecutable = Join-Path $InternalDir 'CasualJumpingTests.exe'
    & $Compiler /nologo /optimize+ /target:exe /langversion:5 `
        "/out:$TestExecutable" `
        "/reference:$Runtime" `
        (Join-Path $ModRoot 'src\AirControlState.cs') `
        (Join-Path $ModRoot 'src\CasualPhysics.cs') `
        (Join-Path $ModRoot 'src\CoyoteJumpWindow.cs') `
        (Join-Path $ModRoot 'src\LandingJumpBuffer.cs') `
        (Join-Path $ModRoot 'src\JumpSupport.cs') `
        (Join-Path $ModRoot 'src\LevelPermission.cs') `
        (Join-Path $ModRoot 'src\SurfaceControlState.cs') `
        (Join-Path $ModRoot 'tests\CasualPhysicsTests.cs')
    if ($LASTEXITCODE -ne 0) {
        throw "Casual Jumping test compilation failed with exit code $LASTEXITCODE"
    }
    & $TestExecutable
    if ($LASTEXITCODE -ne 0) {
        throw "Casual Jumping physics tests failed with exit code $LASTEXITCODE"
    }
}

$Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Assembly).Hash.ToLowerInvariant()
Write-Host "[OK] Casual Jumping: $Assembly"
Write-Host "[OK] SHA-256: $Hash"

& (Join-Path $RepoRoot 'mods\jk-runtime\sdk\package.ps1') -Implementation $Assembly -Output (Join-Path $UploadDir 'CasualJumping.dll') -GameDir $GameDir
