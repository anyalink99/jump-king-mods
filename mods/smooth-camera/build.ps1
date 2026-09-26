param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [switch]$Graphics,
    [switch]$MappingCompatibility
)
$ErrorActionPreference = 'Stop'
$cameraRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$cameraInternal = Join-Path $cameraRepo 'build/smooth-camera/_INTERNAL'
$cameraUpload = Join-Path $cameraRepo 'build/smooth-camera/UPLOAD_TO_WORKSHOP'
$cameraCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$cameraRuntime = Join-Path $cameraRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$cameraHarmony = Join-Path $PSScriptRoot 'lib/0Harmony.dll'
$cameraDependencies = @("$GameDir/JumpKing.exe", "$GameDir/MonoGame.Framework.dll", "$GameDir/LanguageJK.dll", "$GameDir/Steamworks.NET.dll", $cameraRuntime, $cameraHarmony)
foreach ($cameraFile in @($cameraCompiler) + $cameraDependencies) {
    if (-not (Test-Path -LiteralPath $cameraFile -PathType Leaf)) { throw "Missing build input: $cameraFile. Build JK Runtime first." }
}
New-Item -ItemType Directory -Force -Path $cameraInternal,$cameraUpload | Out-Null
$cameraReferences = @($cameraDependencies | ForEach-Object { "/reference:$_" })
$cameraSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
$cameraAssembly = Join-Path $cameraInternal 'SmoothCamera.Module.dll'
& $cameraCompiler /nologo /optimize+ /target:library /langversion:5 /nowarn:1685 "/out:$cameraAssembly" $cameraReferences $cameraSources
if ($LASTEXITCODE -ne 0) { throw 'Smooth Camera compilation failed' }
Copy-Item -LiteralPath $cameraDependencies -Destination $cameraInternal -Force
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $cameraInternal -Force
$cameraTests = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
$cameraTestExe = Join-Path $cameraInternal 'SmoothCameraTests.exe'
& $cameraCompiler /nologo /optimize+ /target:exe /platform:x64 /langversion:5 /nowarn:1685 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$cameraTestExe" $cameraReferences $cameraSources $cameraTests
if ($LASTEXITCODE -ne 0) { throw 'Smooth Camera test compilation failed' }
if ($MappingCompatibility) {
    $cameraMapping = Join-Path $cameraRepo 'build/mega-mapping-expansion/_INTERNAL/MegaMappingExpansion.Module.dll'
    if (-not (Test-Path -LiteralPath $cameraMapping)) { throw 'Build Mega Mapping Expansion before its camera compatibility check' }
    & $cameraTestExe --graphics $GameDir --mapping $cameraMapping
} elseif ($Graphics) { & $cameraTestExe --graphics $GameDir } else { & $cameraTestExe }
if ($LASTEXITCODE -ne 0) { throw 'Smooth Camera checks failed' }
& (Join-Path $cameraRepo 'mods/jk-runtime/sdk/package.ps1') -Implementation $cameraAssembly -Output (Join-Path $cameraUpload 'SmoothCamera.dll') -GameDir $GameDir -References @($cameraHarmony)
Copy-Item -LiteralPath $cameraHarmony,(Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'WORKSHOP.md'),(Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') -Destination $cameraUpload -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $cameraUpload -Recurse -Force
$cameraLegacyGuide = Join-Path $cameraUpload 'VALIDATION.md'
if (Test-Path -LiteralPath $cameraLegacyGuide -PathType Leaf) { Remove-Item -LiteralPath $cameraLegacyGuide }
$cameraPackageTest = Join-Path $cameraInternal 'PackageTests.exe'
& $cameraCompiler /nologo /target:exe /langversion:5 "/out:$cameraPackageTest" "/reference:$GameDir/JumpKing.exe" (Join-Path $cameraRepo 'mods/jk-runtime/tests/PackageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Package test compilation failed' }
& $cameraPackageTest (Join-Path $cameraInternal 'JKRuntime.dll') (Join-Path $cameraUpload 'SmoothCamera.dll')
if ($LASTEXITCODE -ne 0) { throw 'Smooth Camera package discovery failed' }
$cameraDlls = @(Get-ChildItem -LiteralPath $cameraUpload -Filter '*.dll' -File -Recurse)
if ($cameraDlls.Count -ne 2 -or @($cameraDlls | Where-Object Name -notin @('SmoothCamera.dll','0Harmony.dll')).Count -ne 0) { throw 'Unexpected release dependencies' }
Write-Host "[OK] Smooth Camera package: $cameraUpload"
