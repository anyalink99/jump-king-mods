param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$constructionRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$constructionBuild = Join-Path $constructionRepo 'build/mega-mapping-expansion/_INTERNAL'
$constructionOutput = Join-Path $constructionRepo ('build/_work/mme-construction/graphics/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $constructionOutput | Out-Null
$constructionDependencies = @("$GameDir/JumpKing.exe", "$GameDir/MonoGame.Framework.dll", "$GameDir/LanguageJK.dll", "$GameDir/Steamworks.NET.dll", "$constructionRepo/build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll", "$PSScriptRoot/lib/0Harmony.dll", "$constructionBuild/MegaMappingApi.dll")
foreach ($constructionDependency in $constructionDependencies) { Copy-Item -LiteralPath $constructionDependency -Destination $constructionOutput -Force }
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $constructionOutput -Force
$constructionSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Recurse -Filter '*.cs' -File | ForEach-Object FullName)
$constructionSources += Join-Path $PSScriptRoot '../jk-runtime/sdk/BoundedTextLog.cs'
$constructionSources += Join-Path $PSScriptRoot 'sdk/NativeAdapterGraphics.cs'
$constructionReferences = @($constructionDependencies | ForEach-Object { "/reference:$_" })
$constructionResources = @("/resource:$constructionBuild/compiler-identity.txt,MegaMapping.CompilerIdentity")
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'recipes') -Filter '*.xml' -File | ForEach-Object { $constructionResources += "/resource:$($_.FullName),MegaMapping.Recipes.$($_.Name)" }
$constructionExe = Join-Path $constructionOutput 'ConstructionGraphics.exe'
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo /optimize+ /target:exe /platform:x64 /langversion:5 /main:MegaMappingExpansion.ConstructionGraphics /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.Linq.dll "/out:$constructionExe" $constructionReferences $constructionResources $constructionSources (Join-Path $PSScriptRoot 'sdk/ConstructionGraphics.cs')
if ($LASTEXITCODE -ne 0) { throw 'Construction graphics compilation failed' }
& $constructionExe $constructionOutput $GameDir (Join-Path $PSScriptRoot 'examples/minimal')
if ($LASTEXITCODE -ne 0) { throw 'Construction graphics checks failed' }
Write-Host "[OK] Construction graphics: $constructionOutput"
