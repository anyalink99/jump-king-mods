param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King', [switch]$Graphics)
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskInternal = Join-Path $taskRepo 'build/metroidvania/_INTERNAL'
$taskUpload = Join-Path $taskRepo 'build/metroidvania/UPLOAD_TO_WORKSHOP'
$taskRuntime = Join-Path $taskRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$taskCompiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskDependencies = @("$GameDir/JumpKing.exe", "$GameDir/MonoGame.Framework.dll", "$GameDir/LanguageJK.dll", "$GameDir/Steamworks.NET.dll", $taskRuntime)
foreach ($taskFile in @($taskCompiler) + $taskDependencies) { if (-not (Test-Path -LiteralPath $taskFile)) { throw "Missing build input: $taskFile" } }
New-Item -ItemType Directory -Force $taskInternal,$taskUpload | Out-Null
$taskRefs = @($taskDependencies | ForEach-Object { "/reference:$_" })
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File | ForEach-Object FullName)
$taskAssembly = Join-Path $taskInternal 'Metroidvania.Module.dll'
$taskContentRoot = Join-Path $PSScriptRoot 'assets/imported'
$taskCatalogueFile = Join-Path $taskContentRoot 'catalog.xml'
if (-not (Test-Path -LiteralPath $taskCatalogueFile)) { throw 'Compile the import manifest first: build-support/import-jkq.ps1' }
[xml]$taskCatalogue = Get-Content -LiteralPath $taskCatalogueFile -Raw
. (Join-Path $PSScriptRoot 'build-support/content-inputs.ps1')
if ($taskCatalogue.Content.RecipeFileHash -ne (Get-QuestInputHash (Join-Path $PSScriptRoot 'content/jkq-import.json'))) { throw 'Import manifest changed; rerun build-support/import-jkq.ps1 before building' }
if ($taskCatalogue.Content.ToolchainHash -ne (Get-QuestToolchainHash (Join-Path $PSScriptRoot 'build-support'))) { throw 'Import tools changed; rerun build-support/import-jkq.ps1 before building' }
$taskArt = @('/resource:' + $taskCatalogueFile + ',Metroidvania.Content')
foreach ($taskAsset in $taskCatalogue.Content.Assets.Asset) {
    $taskAssetPath = [IO.Path]::GetFullPath((Join-Path $taskContentRoot $taskAsset.Path))
    if (-not $taskAssetPath.StartsWith([IO.Path]::GetFullPath($taskContentRoot)+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Compiled asset escaped its content pack' }
    if ((Get-FileHash -LiteralPath $taskAssetPath -Algorithm SHA256).Hash -ne $taskAsset.SHA256) { throw "Compiled asset was changed after validation: $($taskAsset.Path)" }
    $taskArt += '/resource:' + $taskAssetPath + ',' + $taskAsset.Resource
}
& $taskCompiler /nologo /optimize+ /target:library /langversion:5 /nowarn:1685 "/out:$taskAssembly" $taskRefs $taskArt $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Metroidvania compilation failed' }
Copy-Item -LiteralPath $taskDependencies -Destination $taskInternal -Force
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $taskInternal -Force
$taskTests = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' -File | ForEach-Object FullName)
$taskExe = Join-Path $taskInternal 'MetroidvaniaTests.exe'
& $taskCompiler /nologo /optimize+ /target:exe /platform:x64 /langversion:5 /nowarn:1685 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$taskExe" $taskRefs $taskArt $taskSources $taskTests
if ($LASTEXITCODE -ne 0) { throw 'Metroidvania test compilation failed' }
$taskScratch = Join-Path $taskRepo 'build/_work/metroidvania/tests'
New-Item -ItemType Directory -Force $taskScratch | Out-Null
. (Join-Path $taskRepo 'mods/jk-runtime/build-support/build-retention.ps1')
Remove-BuildArtifactHistory -Directory $taskScratch -NamePattern '^run-[0-9a-f]{32}$' -Keep 2 -CompletionMarker '.build-complete' | Out-Null
$taskRun = Join-Path $taskScratch ('run-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $taskRun | Out-Null
& (Join-Path $PSScriptRoot 'tests/ContentInputs.Tests.ps1') -Scratch $taskRun
& $taskExe $taskRun
if ($LASTEXITCODE -ne 0) { throw 'Metroidvania regressions failed' }
if ($Graphics) { foreach ($taskTab in 0..4) { & $taskExe $taskRun --graphics $GameDir $taskTab; if ($LASTEXITCODE -ne 0) { throw 'Metroidvania graphics checks failed' } } }
if ($Graphics) { & $taskExe $taskRun --audio; if ($LASTEXITCODE -ne 0) { throw 'Metroidvania native audio checks failed' } }
& (Join-Path $taskRepo 'mods/jk-runtime/sdk/package.ps1') -Implementation $taskAssembly -Output (Join-Path $taskUpload 'Metroidvania.dll') -GameDir $GameDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'CHANGELOG.md'),(Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') -Destination $taskUpload -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $taskUpload -Recurse -Force
$taskPackageTest = Join-Path $taskInternal 'PackageTests.exe'
& $taskCompiler /nologo /target:exe /langversion:5 "/out:$taskPackageTest" "/reference:$GameDir/JumpKing.exe" (Join-Path $taskRepo 'mods/jk-runtime/tests/PackageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Package test compilation failed' }
& $taskPackageTest $taskRuntime (Join-Path $taskUpload 'Metroidvania.dll')
if ($LASTEXITCODE -ne 0) { throw 'Metroidvania native discovery failed' }
& (Join-Path $PSScriptRoot 'tests/Install.Tests.ps1') -Scratch $taskRun
if (-not $?) { throw 'Metroidvania installer checks failed' }
[IO.File]::WriteAllText((Join-Path $taskRun '.build-complete'), [DateTime]::UtcNow.ToString('o'))
Write-Host "[OK] Metroidvania 0.4.0: $taskUpload"
