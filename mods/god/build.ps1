param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [switch]$Graphics
)
$ErrorActionPreference = 'Stop'
$godRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$godCompiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$godInternal = Join-Path $godRepo 'build/god/_INTERNAL'
$godPayload = Join-Path $godRepo 'build/god/UPLOAD_TO_WORKSHOP'
$godRuntime = Join-Path $godRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$godHarmony = Join-Path $godRepo 'mods/smooth-camera/lib/0Harmony.dll'
$godDependencies = @("$GameDir/JumpKing.exe", "$GameDir/MonoGame.Framework.dll", "$GameDir/LanguageJK.dll", "$GameDir/Steamworks.NET.dll", $godRuntime, $godHarmony)
foreach ($godFile in @($godCompiler) + $godDependencies) {
    if (-not (Test-Path -LiteralPath $godFile -PathType Leaf)) { throw "Missing input: $godFile. Build JK Runtime first." }
}
New-Item -ItemType Directory -Force -Path $godInternal,$godPayload | Out-Null
$godReferences = @($godDependencies | ForEach-Object { "/reference:$_" }) + @('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.IO.Compression.dll')
$godSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
$godImplementation = Join-Path $godInternal 'God.Module.dll'
& $godCompiler /nologo /optimize+ /target:library /langversion:5 /nowarn:1685 "/out:$godImplementation" $godReferences $godSources
if ($LASTEXITCODE -ne 0) { throw 'God compilation failed' }
$godTests = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
$godTestExe = Join-Path $godInternal 'GodTests.exe'
& $godCompiler /nologo /debug:pdbonly /optimize+ /target:exe /platform:x64 /langversion:5 /nowarn:1685 "/out:$godTestExe" $godReferences $godSources $godTests
if ($LASTEXITCODE -ne 0) { throw 'God tests compilation failed' }
Copy-Item -LiteralPath $godDependencies -Destination $godInternal -Force
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $godInternal -Force
$godArguments = @($GameDir, (Join-Path $godRepo 'build/_work/god/validation'))
if ($Graphics) { $godArguments += '--graphics' }
& $godTestExe @godArguments
if ($LASTEXITCODE -ne 0) { throw 'God checks failed' }
& (Join-Path $godRepo 'mods/jk-runtime/sdk/package.ps1') -Implementation $godImplementation -Output (Join-Path $godPayload 'God.dll') -GameDir $GameDir -References @($godHarmony)
if (-not $?) { throw 'God packaging failed' }
Copy-Item -LiteralPath $godHarmony,(Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'CHANGELOG.md'),(Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') -Destination $godPayload -Force
$godPackageCheck = Join-Path $godInternal 'PackageTests.exe'
& $godCompiler /nologo /target:exe /langversion:5 "/out:$godPackageCheck" "/reference:$GameDir/JumpKing.exe" (Join-Path $godRepo 'mods/jk-runtime/tests/PackageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'God package check compilation failed' }
& $godPackageCheck $godRuntime (Join-Path $godPayload 'God.dll')
if ($LASTEXITCODE -ne 0) { throw 'God package discovery failed' }
$godDlls = @(Get-ChildItem -LiteralPath $godPayload -Filter '*.dll' -Recurse -File)
if ($godDlls.Count -ne 2 -or @($godDlls | Where-Object Name -notin @('God.dll','0Harmony.dll')).Count) { throw 'Unexpected God package dependencies' }
& (Join-Path $PSScriptRoot 'tests/InstallerTests.ps1') -GameDir $GameDir
if (-not $?) { throw 'God installer checks failed' }
Write-Host "[OK] God package: $godPayload"
