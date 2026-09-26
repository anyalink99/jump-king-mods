param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [switch]$Graphics,
    [string]$CameraAssembly,
    [string]$ReplayAssembly
)
$ErrorActionPreference = 'Stop'
$overlayRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$overlayInternal = Join-Path $overlayRepo 'build/overlay-plus/_INTERNAL'
$overlayUpload = Join-Path $overlayRepo 'build/overlay-plus/UPLOAD_TO_WORKSHOP'
$overlayCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$overlayRuntime = Join-Path $overlayRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$overlayHarmony = Join-Path $overlayRepo 'mods/smooth-camera/lib/0Harmony.dll'
$overlayDependencies = @("$GameDir/JumpKing.exe", "$GameDir/MonoGame.Framework.dll", "$GameDir/LanguageJK.dll", "$GameDir/Steamworks.NET.dll", $overlayRuntime, $overlayHarmony)
foreach ($overlayFile in @($overlayCompiler) + $overlayDependencies) { if (-not (Test-Path -LiteralPath $overlayFile -PathType Leaf)) { throw "Missing input: $overlayFile. Build JK Runtime first." } }
New-Item -ItemType Directory -Force -Path $overlayInternal,$overlayUpload | Out-Null
$overlayApi = Join-Path $overlayInternal 'OverlayPlusApi.dll'
& $overlayCompiler /nologo /optimize+ /target:library /langversion:5 "/out:$overlayApi" "/reference:$GameDir/MonoGame.Framework.dll" (Join-Path $PSScriptRoot 'api/OverlayApi.cs')
if ($LASTEXITCODE -ne 0) { throw 'Overlay API compilation failed' }
$overlayReferences = @($overlayDependencies + $overlayApi | ForEach-Object { "/reference:$_" }) + @('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll')
$overlaySources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
$overlayAssembly = Join-Path $overlayInternal 'OverlayPlus.Module.dll'
& $overlayCompiler /nologo /optimize+ /target:library /langversion:5 /nowarn:1685 "/out:$overlayAssembly" $overlayReferences $overlaySources
if ($LASTEXITCODE -ne 0) { throw 'Overlay+ compilation failed' }
Copy-Item -LiteralPath $overlayDependencies -Destination $overlayInternal -Force
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $overlayInternal -Force
$overlayTests = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
$overlayTestExe = Join-Path $overlayInternal 'OverlayPlusTests.exe'
& $overlayCompiler /nologo /optimize+ /target:exe /platform:x64 /langversion:5 /nowarn:1685 "/out:$overlayTestExe" $overlayReferences $overlaySources $overlayTests
if ($LASTEXITCODE -ne 0) { throw 'Overlay+ tests compilation failed' }
$overlayTestArguments = @($GameDir)
if ($Graphics) { $overlayTestArguments += '--graphics' }
foreach ($peer in @(@('--camera',$CameraAssembly),@('--replays',$ReplayAssembly))) {
    if ($peer[1]) {
        if (-not (Test-Path -LiteralPath $peer[1] -PathType Leaf)) { throw "Missing selected compatibility input: $($peer[1])" }
        $overlayTestArguments += @($peer[0], [IO.Path]::GetFullPath($peer[1]))
    }
}
& $overlayTestExe @overlayTestArguments
if ($LASTEXITCODE -ne 0) { throw 'Overlay+ checks failed' }
& (Join-Path $overlayRepo 'mods/jk-runtime/sdk/package.ps1') -Implementation $overlayAssembly -Output (Join-Path $overlayUpload 'OverlayPlus.dll') -GameDir $GameDir -References @($overlayHarmony,$overlayApi)
Copy-Item -LiteralPath $overlayHarmony,$overlayApi,(Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'WORKSHOP.md'),(Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') -Destination $overlayUpload -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $overlayUpload -Recurse -Force
$overlayLegacyGuide = Join-Path $overlayUpload 'VALIDATION.md'
if (Test-Path -LiteralPath $overlayLegacyGuide -PathType Leaf) { Remove-Item -LiteralPath $overlayLegacyGuide }
$overlayPackageTest = Join-Path $overlayInternal 'PackageTests.exe'
& $overlayCompiler /nologo /target:exe /langversion:5 "/out:$overlayPackageTest" "/reference:$GameDir/JumpKing.exe" (Join-Path $overlayRepo 'mods/jk-runtime/tests/PackageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Package test compilation failed' }
& $overlayPackageTest (Join-Path $overlayInternal 'JKRuntime.dll') (Join-Path $overlayUpload 'OverlayPlus.dll')
if ($LASTEXITCODE -ne 0) { throw 'Overlay+ package discovery failed' }
$overlayDlls = @(Get-ChildItem -LiteralPath $overlayUpload -Filter '*.dll' -File -Recurse)
if ($overlayDlls.Count -ne 3 -or @($overlayDlls | Where-Object Name -notin @('OverlayPlus.dll','OverlayPlusApi.dll','0Harmony.dll')).Count -ne 0) { throw 'Unexpected release dependencies' }
& (Join-Path $PSScriptRoot 'tests/InstallerTests.ps1') -GameDir $GameDir
if (-not $?) { throw 'Overlay installer tests failed' }
Write-Host "[OK] Overlay+ package: $overlayUpload"
