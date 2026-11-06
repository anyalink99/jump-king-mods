param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King', [switch]$Graphics)
$ErrorActionPreference = 'Stop'
$prismRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$prismInternal = Join-Path $prismRepo 'build/prism/_INTERNAL'
$prismUpload = Join-Path $prismRepo 'build/prism/UPLOAD_TO_WORKSHOP'
$prismCompiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$prismRuntime = Join-Path $prismRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$prismHarmony = Join-Path $prismRepo 'mods/subframe-charge/lib/0Harmony.dll'
$prismDeps = @("$GameDir/JumpKing.exe", "$GameDir/MonoGame.Framework.dll", "$GameDir/LanguageJK.dll", "$GameDir/Steamworks.NET.dll", "$GameDir/SharpDX.dll", "$GameDir/SharpDX.XAudio2.dll", $prismRuntime, $prismHarmony)
foreach ($file in @($prismCompiler) + $prismDeps) { if (-not (Test-Path -LiteralPath $file)) { throw "Missing build input: $file" } }
New-Item -ItemType Directory -Force $prismInternal,$prismUpload | Out-Null
$prismReferences = @($prismDeps | ForEach-Object { "/reference:$_" })
$prismSources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$prismAssembly = Join-Path $prismInternal 'Prism.Module.dll'
& $prismCompiler /nologo /optimize+ /target:library /langversion:5 /nowarn:1685 "/out:$prismAssembly" $prismReferences $prismSources
if ($LASTEXITCODE -ne 0) { throw 'Prism compilation failed' }
Copy-Item -LiteralPath $prismDeps -Destination $prismInternal -Force
Get-ChildItem $GameDir -Filter 'SharpDX*.dll' | Copy-Item -Destination $prismInternal -Force
$prismTestExe = Join-Path $prismInternal 'PrismTests.exe'
$prismTests = @(Get-ChildItem (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | ForEach-Object FullName)
& $prismCompiler /nologo /optimize+ /target:exe /platform:x64 /langversion:5 /nowarn:1685 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$prismTestExe" $prismReferences $prismSources $prismTests
if ($LASTEXITCODE -ne 0) { throw 'Prism test compilation failed' }
if ($Graphics) { & $prismTestExe --graphics $GameDir } else { & $prismTestExe }
if ($LASTEXITCODE -ne 0) { throw 'Prism checks failed' }
& (Join-Path $prismRepo 'mods/jk-runtime/sdk/package.ps1') -Implementation $prismAssembly -Output (Join-Path $prismUpload 'Prism.dll') -GameDir $GameDir -References @($prismHarmony, "$GameDir/SharpDX.dll", "$GameDir/SharpDX.XAudio2.dll")
Copy-Item -LiteralPath $prismHarmony,(Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'CHANGELOG.md'),(Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') -Destination $prismUpload -Force
$prismPackageTest = Join-Path $prismInternal 'PackageTests.exe'
& $prismCompiler /nologo /target:exe /langversion:5 "/out:$prismPackageTest" "/reference:$GameDir/JumpKing.exe" (Join-Path $prismRepo 'mods/jk-runtime/tests/PackageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Prism package test compilation failed' }
& $prismPackageTest (Join-Path $prismInternal 'JKRuntime.dll') (Join-Path $prismUpload 'Prism.dll')
if ($LASTEXITCODE -ne 0) { throw 'Prism native package discovery failed' }
if (Test-Path (Join-Path $prismUpload 'themes')) { throw 'Keep personal music outside UPLOAD_TO_WORKSHOP' }
Write-Host "[OK] Prism package: $prismUpload"
