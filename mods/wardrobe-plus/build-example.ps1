param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King', [string]$Output = '', [ValidateSet('ashen-king','eclipse-king','vessel-king')][string]$Example = 'ashen-king', [ValidateSet('current','graphite','platinum','white-gold','bronze','garnet','ivory')][string]$Finish = 'ivory')
$ErrorActionPreference = 'Stop'
$exampleRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $Output) { $Output = Join-Path $exampleRepo ('build/wardrobe-plus/' + $Example + '-' + [Guid]::NewGuid().ToString('N')) }
if (Test-Path -LiteralPath $Output) { throw 'Use a new output directory; existing packages are preserved.' }
& (Join-Path $PSScriptRoot 'build-authoring.ps1') -GameDir $GameDir
$exampleTool = Join-Path $exampleRepo 'build/wardrobe-plus/_INTERNAL/authoring/SkinTool.exe'
New-Item -ItemType Directory -Path $Output | Out-Null
$exampleLayout = Join-Path $Output 'native-layout.json'
& $exampleTool layout $exampleLayout
if ($LASTEXITCODE -ne 0) { throw 'Native layout export failed' }
$exampleFinish = @()
if ($Example -eq 'eclipse-king') { $exampleFinish = @('--finish', $Finish) }
elseif ($PSBoundParameters.ContainsKey('Finish') -and $Finish -ne 'current') { throw 'Finish proposals apply only to Eclipse King.' }
& python (Join-Path $PSScriptRoot "examples/$Example/build_assets.py") --native (Join-Path $GameDir 'Content/king/base.xnb') --layout $exampleLayout --output (Join-Path $Output 'source') @exampleFinish
if ($LASTEXITCODE -ne 0) { throw "$Example asset generation failed" }
if ($Example -eq 'eclipse-king') {
    & python (Join-Path $PSScriptRoot 'tests/check_eclipse_masks.py') --native (Join-Path $GameDir 'Content/king/base.xnb') --layout $exampleLayout --proof (Join-Path $Output 'visor-coverage.png')
    if ($LASTEXITCODE -ne 0) { throw 'Eclipse King native visor coverage failed' }
}
& python (Join-Path $PSScriptRoot 'tests/check_king_mask.py') --native (Join-Path $GameDir 'Content/king/base.xnb') --layout $exampleLayout --reskin (Join-Path $Output "source/$Example.png")
if ($LASTEXITCODE -ne 0) { throw 'Native sprite ownership validation failed' }
& (Join-Path $PSScriptRoot 'build-effect.ps1') -Effect Advanced
$exampleCompiler = Join-Path $exampleRepo 'build/wardrobe-plus/_INTERNAL/effect-tools/sdk/$PROGRAMFILES/MSBuild/MonoGame/v3.0/Tools/2MGFX.exe'
& $exampleCompiler (Join-Path $PSScriptRoot "examples/$Example/visor.fx") (Join-Path $Output 'source/wardrobe/visor.mgfxo') /Profile:DirectX_11
if ($LASTEXITCODE -ne 0) { throw "$Example visor shader compilation failed" }
if ($Example -eq 'vessel-king') {
    $exampleRenderer = Join-Path $exampleRepo 'build/wardrobe-plus/_INTERNAL/VesselPreview.exe'
    & "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /platform:x64 /langversion:5 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/reference:$GameDir/MonoGame.Framework.dll" "/out:$exampleRenderer" (Join-Path $PSScriptRoot 'examples/vessel-king/VesselPreview.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Vessel preview renderer compilation failed' }
    $exampleRawPreview = Join-Path $Output 'native-preview.png'
    & $exampleRenderer (Join-Path $Output 'source') $exampleRawPreview
    if ($LASTEXITCODE -ne 0) { throw 'Native Vessel preview rendering failed' }
    & python (Join-Path $PSScriptRoot 'examples/vessel-king/prepare_preview.py') --source $exampleRawPreview --output (Join-Path $Output 'source/workshop-preview.png')
    if ($LASTEXITCODE -ne 0) { throw 'Vessel preview palette conversion failed' }
    & (Join-Path $PSScriptRoot 'verify-preview.ps1') -PreviewPath (Join-Path $Output 'source/workshop-preview.png')
}
& $exampleTool build (Join-Path $Output 'source') (Join-Path $Output 'SKIN_PACKAGE')
if ($LASTEXITCODE -ne 0) { throw "$Example package validation failed" }
& (Join-Path $PSScriptRoot 'verify-skin-package.ps1') -Package (Join-Path $Output 'SKIN_PACKAGE')
if ($LASTEXITCODE -ne 0) { throw 'Worldsmith skin validation failed' }
Write-Host "[OK] ${Example}: $Output/SKIN_PACKAGE"
