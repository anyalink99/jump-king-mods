param([Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference = 'Stop'
$filterRepo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$filterCache = Join-Path $filterRepo 'build/_work/tools/monogame-3.7.1'
$filterCompiler = Join-Path $filterCache 'sdk/$PROGRAMFILES/MSBuild/MonoGame/v3.0/Tools/2MGFX.exe'
# reuse the pinned compiler already extracted by Wardrobe when it's available
$filterExisting = Join-Path $filterRepo 'build/wardrobe-plus/_INTERNAL/effect-tools/sdk/$PROGRAMFILES/MSBuild/MonoGame/v3.0/Tools/2MGFX.exe'
if (-not (Test-Path -LiteralPath $filterCompiler) -and (Test-Path -LiteralPath $filterExisting)) { $filterCompiler = $filterExisting }
if (-not (Test-Path -LiteralPath $filterCompiler)) {
    New-Item -ItemType Directory -Force $filterCache | Out-Null
    $filterSetup = Join-Path $filterCache 'MonoGameSetup.exe'
    if (-not (Test-Path -LiteralPath $filterSetup)) { Invoke-WebRequest 'https://github.com/MonoGame/MonoGame/releases/download/v3.7.1/MonoGameSetup.exe' -OutFile $filterSetup }
    if ((Get-FileHash -LiteralPath $filterSetup).Hash -ne '0F1B9B049027392A290827DC0458A2F9783C22B19DF972567D52020ACDF7D262') { throw 'Unexpected MonoGame compiler archive' }
    $filter7zip = Join-Path $env:ProgramFiles '7-Zip/7z.exe'
    if (-not (Test-Path -LiteralPath $filter7zip)) { throw 'Install 7-Zip to extract the pinned MonoGame compiler.' }
    & $filter7zip x $filterSetup ('-o' + (Join-Path $filterCache 'sdk')) -y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Shader compiler extraction failed' }
}
& $filterCompiler (Join-Path $PSScriptRoot '../assets/PixelScale.fx') $Output /Profile:DirectX_11
if ($LASTEXITCODE -ne 0) { throw 'Pixel filter compilation failed' }
