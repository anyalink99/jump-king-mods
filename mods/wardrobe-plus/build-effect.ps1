param([string]$Compiler = '', [ValidateSet('Crystal','Cosmic')][string]$Effect = 'Crystal')
$ErrorActionPreference = 'Stop'
$effectRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$effectCache = Join-Path $effectRepo 'build/wardrobe-plus/_INTERNAL/effect-tools'
$effectOutput = Join-Path $effectRepo ('build/wardrobe-plus/_INTERNAL/' + $Effect + '.mgfxo')
if (-not $Compiler) {
    $Compiler = Join-Path $effectCache 'sdk/$PROGRAMFILES/MSBuild/MonoGame/v3.0/Tools/2MGFX.exe'
    if (-not (Test-Path -LiteralPath $Compiler)) {
        New-Item -ItemType Directory -Force -Path $effectCache | Out-Null
        $effectSetup = Join-Path $effectCache 'MonoGameSetup.exe'
        if (-not (Test-Path -LiteralPath $effectSetup)) {
            Invoke-WebRequest -Uri 'https://github.com/MonoGame/MonoGame/releases/download/v3.7.1/MonoGameSetup.exe' -OutFile $effectSetup
        }
        if ((Get-FileHash -LiteralPath $effectSetup -Algorithm SHA256).Hash -ne '0F1B9B049027392A290827DC0458A2F9783C22B19DF972567D52020ACDF7D262') { throw 'Unexpected MonoGame compiler archive hash' }
        $effect7Zip = Join-Path $env:ProgramFiles '7-Zip/7z.exe'
        if (-not (Test-Path -LiteralPath $effect7Zip)) { throw 'Install 7-Zip or pass -Compiler with a MonoGame 3.7.1 2MGFX.exe path' }
        & $effect7Zip x $effectSetup ('-o' + (Join-Path $effectCache 'sdk')) -y | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not extract MonoGame compiler' }
    }
}
& $Compiler (Join-Path $PSScriptRoot ('assets/' + $Effect + '.fx')) $effectOutput /Profile:DirectX_11
if ($LASTEXITCODE -ne 0) { throw 'Crystal shader compilation failed' }
