param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King', [switch]$Graphics, [switch]$Compatibility, [string]$EffectCompiler = '', [switch]$ExperimentalRefraction)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'verify-preview.ps1')
$wardrobeRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$wardrobeBuild = Join-Path $wardrobeRepo 'build\wardrobe-plus\_INTERNAL'
$wardrobeUpload = Join-Path $wardrobeRepo 'build\wardrobe-plus\UPLOAD_TO_WORKSHOP'
$wardrobeCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wardrobeRuntime = Join-Path $wardrobeRepo 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
$wardrobeDependencies = @("$GameDir\JumpKing.exe", "$GameDir\MonoGame.Framework.dll", "$GameDir\LanguageJK.dll", "$GameDir\Steamworks.NET.dll", $wardrobeRuntime)
New-Item -ItemType Directory -Force -Path $wardrobeBuild,$wardrobeUpload | Out-Null
$wardrobeReferences = @($wardrobeDependencies | ForEach-Object { "/reference:$_" })
$wardrobeSources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
$wardrobeAssembly = Join-Path $wardrobeBuild 'WardrobePlus.Module.dll'
& (Join-Path $PSScriptRoot 'build-effect.ps1') -Compiler $EffectCompiler -Effect Cosmic
$wardrobeEffectResource = @("/resource:$wardrobeBuild\Cosmic.mgfxo,WardrobePlus.Cosmic.mgfxo",
    "/resource:$PSScriptRoot\assets\cosmic-nebula.png,WardrobePlus.cosmic-nebula.png",
    "/resource:$PSScriptRoot\assets\cosmic-stars.png,WardrobePlus.cosmic-stars.png")
if ($ExperimentalRefraction) {
    & (Join-Path $PSScriptRoot 'build-effect.ps1') -Compiler $EffectCompiler
    $wardrobeEffectResource += @("/resource:$wardrobeBuild\Crystal.mgfxo,WardrobePlus.Crystal.mgfxo", '/define:WARDROBE_REFRACTION')
}
& $wardrobeCompiler /nologo /optimize+ /target:library /langversion:5 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$wardrobeAssembly" $wardrobeEffectResource $wardrobeReferences $wardrobeSources
if ($LASTEXITCODE -ne 0) { throw 'Wardrobe+ compilation failed' }
Copy-Item -LiteralPath $wardrobeDependencies -Destination $wardrobeBuild -Force
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $wardrobeBuild -Force
$wardrobeTests = @(Get-ChildItem (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
if ($wardrobeTests.Count -gt 0) {
    & $wardrobeCompiler /nologo /optimize+ /target:exe /platform:x64 /langversion:5 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$wardrobeBuild\WardrobeTests.exe" $wardrobeEffectResource $wardrobeReferences $wardrobeSources $wardrobeTests
    if ($LASTEXITCODE -ne 0) { throw 'Wardrobe+ test compilation failed' }
    if ($Compatibility) { & (Join-Path $wardrobeBuild 'WardrobeTests.exe') --graphics $GameDir $wardrobeRepo }
    elseif ($Graphics) { & (Join-Path $wardrobeBuild 'WardrobeTests.exe') --graphics $GameDir }
    else { & (Join-Path $wardrobeBuild 'WardrobeTests.exe') }
    if ($LASTEXITCODE -ne 0) { throw 'Wardrobe+ tests failed' }
}
& (Join-Path $wardrobeRepo 'mods\jk-runtime\sdk\package.ps1') -Implementation $wardrobeAssembly -Output (Join-Path $wardrobeUpload 'WardrobePlus.dll') -GameDir $GameDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'CHANGELOG.md'),(Join-Path $PSScriptRoot 'WORKSHOP.md') -Destination $wardrobeUpload -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $wardrobeUpload -Recurse -Force
$wardrobeLegacyGuide = Join-Path $wardrobeUpload 'VALIDATION.md'
if (Test-Path -LiteralPath $wardrobeLegacyGuide -PathType Leaf) { Remove-Item -LiteralPath $wardrobeLegacyGuide }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'workshop-preview-256.png') -Destination (Join-Path $wardrobeUpload 'workshop-preview.png') -Force
& (Join-Path $PSScriptRoot 'verify-preview.ps1') -PreviewPath (Join-Path $wardrobeUpload 'workshop-preview.png')
$wardrobeExtraDlls = @(Get-ChildItem -LiteralPath $wardrobeUpload -Filter '*.dll' -File -Recurse | Where-Object { $_.FullName -ne (Join-Path $wardrobeUpload 'WardrobePlus.dll') })
if ($wardrobeExtraDlls.Count -gt 0) { throw "Unexpected dependency DLLs in release package: $($wardrobeExtraDlls.Name -join ', ')" }
$wardrobePackageTest = Join-Path $wardrobeBuild 'PackageTests.exe'
& $wardrobeCompiler /nologo /target:exe /langversion:5 "/out:$wardrobePackageTest" "/reference:$GameDir\JumpKing.exe" (Join-Path $wardrobeRepo 'mods\jk-runtime\tests\PackageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Wardrobe+ package check compilation failed' }
& $wardrobePackageTest (Join-Path $wardrobeBuild 'JKRuntime.dll') (Join-Path $wardrobeUpload 'WardrobePlus.dll')
if ($LASTEXITCODE -ne 0) { throw 'Wardrobe+ package discovery failed' }
Write-Host "[OK] Wardrobe+ package: $wardrobeUpload"
