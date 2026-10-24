param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King', [switch]$SceneSmoke, [string]$SceneRoot)
$ErrorActionPreference = 'Stop'
$modRoot = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $modRoot '..\..')).Path
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$buildRoot = Join-Path $repoRoot 'build\mega-mapping-expansion'
$internal = Join-Path $buildRoot '_INTERNAL'
$uploadFinal = Join-Path $buildRoot 'UPLOAD_TO_WORKSHOP'
$upload = Join-Path $internal ('package-' + [Guid]::NewGuid().ToString('N'))
$runtime = Join-Path $repoRoot 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
$harmony = Join-Path $modRoot 'lib\0Harmony.dll'
$sample = Join-Path $modRoot 'examples\minimal'
. (Join-Path $repoRoot 'mods/jk-runtime/build-support/build-retention.ps1')
Remove-WorkspaceBuildHistory -Component Mapping | Out-Null
$testWork = $null
$kit = $null
try {
& (Join-Path $repoRoot 'mods\jk-runtime\build.ps1') -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { throw 'JK Runtime dependency build failed' }
foreach ($required in @($compiler,"$GameDir\JumpKing.exe","$GameDir\MonoGame.Framework.dll","$GameDir\LanguageJK.dll",$runtime,$harmony)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required build input is missing: $required" }
}
New-Item -ItemType Directory -Force -Path $internal,$upload | Out-Null
$platformDefault = Join-Path $modRoot 'assets/platforms/default.png'
foreach ($assetRoot in @($internal,$upload)) {
    $assetTarget = Join-Path $assetRoot 'assets/platforms'
    New-Item -ItemType Directory -Force -Path $assetTarget | Out-Null
    Copy-Item -LiteralPath $platformDefault -Destination $assetTarget
}
$api = Join-Path $internal 'MegaMappingApi.dll'
& $compiler /nologo /target:library /langversion:5 /nowarn:1591 "/out:$api" "/doc:$(Join-Path $internal 'MegaMappingApi.xml')" (Join-Path $modRoot 'api/MappingApi.cs') (Join-Path $modRoot 'api/SceneValues.cs') (Join-Path $modRoot 'api/SceneApi.cs')
if ($LASTEXITCODE -ne 0) { throw 'Mapping API contract compilation failed' }
$consumer = Join-Path $internal 'SceneExample.dll'
& $compiler /nologo /target:library /langversion:5 "/out:$consumer" "/reference:$api" (Join-Path $modRoot 'api/SceneExample.cs')
if ($LASTEXITCODE -ne 0) { throw 'External Mapping API consumer compilation failed' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $modRoot 'src') -Recurse -Filter '*.cs' -File | Sort-Object FullName | ForEach-Object FullName)
$sources += Join-Path $repoRoot 'mods/jk-runtime/sdk/BoundedTextLog.cs'
$identityFile = Join-Path $internal 'compiler-identity.txt'
# the source compiler and runtime embed the same semantic compiler identity
$identityInputs = @($sources) + @(Get-ChildItem -LiteralPath (Join-Path $modRoot 'api'),(Join-Path $modRoot 'recipes') -File -Recurse | ForEach-Object FullName)
$identityHasher = [Security.Cryptography.SHA256]::Create()
try {
    # checkout line endings mustn't make matching runtime/compiler caches disagree
    $identity = (@($identityInputs | Sort-Object | ForEach-Object {
        $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($_)).Replace("`r`n", "`n")
        [BitConverter]::ToString($identityHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace('-', '')
    }) -join '')
}
finally { $identityHasher.Dispose() }
[IO.File]::WriteAllText($identityFile,$identity)
$resources = @("/resource:$identityFile,MegaMapping.CompilerIdentity")
Get-ChildItem -LiteralPath (Join-Path $modRoot 'recipes') -Filter '*.xml' -File | Sort-Object Name | ForEach-Object {
    $resources += "/resource:$($_.FullName),MegaMapping.Recipes.$($_.Name)"
}
$implementation = Join-Path $internal 'MegaMappingExpansion.Module.dll'
$refs = @("/reference:$GameDir\JumpKing.exe","/reference:$GameDir\MonoGame.Framework.dll","/reference:$GameDir\LanguageJK.dll","/reference:System.Drawing.dll","/reference:System.Xml.Linq.dll","/reference:$runtime","/reference:$harmony","/reference:$api")
& $compiler /nologo /optimize+ /target:library /langversion:5 "/out:$implementation" $refs $resources $sources
if ($LASTEXITCODE -ne 0) { throw 'Mega Mapping Expansion compilation failed' }
Copy-Item -LiteralPath "$GameDir\JumpKing.exe","$GameDir\MonoGame.Framework.dll","$GameDir\LanguageJK.dll","$GameDir\Steamworks.NET.dll",$runtime,$harmony -Destination $internal -Force
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $internal -Force
$tests = Join-Path $internal 'MegaMappingExpansionTests.exe'
$testSources = @(Get-ChildItem (Join-Path $modRoot 'tests') -Filter '*.cs' -Recurse | ForEach-Object FullName)
& $compiler /nologo /optimize+ /target:exe /langversion:5 /main:MegaMappingExpansion.Tests "/out:$tests" "/reference:$consumer" $refs $resources $sources $testSources
if ($LASTEXITCODE -ne 0) { throw 'Mega Mapping tests compilation failed' }
$testWork = Join-Path $repoRoot ('build/_work/mme-construction/tests/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $testWork | Out-Null
& $tests $testWork $sample
[IO.File]::WriteAllText((Join-Path $testWork '.build-complete'), '')
if ($LASTEXITCODE -ne 0) { throw 'Mega Mapping focused checks failed' }
$sceneCompiler = Join-Path $internal 'SceneCacheCompiler.exe'
& $compiler /nologo /optimize+ /target:exe /langversion:5 /main:MegaMappingExpansion.SceneCacheCompiler "/out:$sceneCompiler" $refs $resources $sources (Join-Path $modRoot 'sdk\SceneCacheCompiler.cs')
if ($LASTEXITCODE -ne 0) { throw 'Scene compiler compilation failed' }
$platformLab = Join-Path $internal 'PlatformLab.exe'
& $compiler /nologo /target:exe /langversion:5 /reference:System.Drawing.dll /reference:System.Xml.Linq.dll "/out:$platformLab" (Join-Path $modRoot 'sdk/PlatformLab.cs')
if ($LASTEXITCODE -ne 0) { throw 'Platform lab tool compilation failed' }
$reference = Join-Path $modRoot 'reference'
& $sceneCompiler --reference $reference
if ($LASTEXITCODE -ne 0) { throw 'Scene reference generation failed' }
& $sceneCompiler --schema (Join-Path $reference 'scene-expanded.xsd')
if ($LASTEXITCODE -ne 0) { throw 'Scene reference schema generation failed' }
& (Join-Path $repoRoot 'mods\jk-runtime\sdk\package.ps1') -Implementation $implementation -Output (Join-Path $upload 'MegaMappingExpansion.dll') -GameDir $GameDir -References @($harmony,$api)
if ($LASTEXITCODE -ne 0) { throw 'Mega Mapping package failed' }
Copy-Item -LiteralPath $harmony,$api,(Join-Path $modRoot 'THIRD_PARTY_NOTICES.md') -Destination $upload -Force
$kitFinal = Join-Path $buildRoot 'AUTHORING_KIT\minimal'
$kit = Join-Path $internal ('kit-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $kit | Out-Null
Get-ChildItem -LiteralPath $sample -Force | Copy-Item -Destination $kit -Recurse -Force
$kitDocs = Join-Path $kit 'docs'
New-Item -ItemType Directory -Path $kitDocs | Out-Null
# keep the same handbook paths in the portable kit
$sourceDocs = Join-Path $modRoot 'docs'
Get-ChildItem -LiteralPath $sourceDocs -File -Recurse | ForEach-Object {
        $kitDocTarget = Join-Path $kitDocs $_.FullName.Substring($sourceDocs.Length + 1)
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $kitDocTarget) | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $kitDocTarget
    }
Copy-Item -LiteralPath (Join-Path $modRoot 'THIRD_PARTY_NOTICES.md') -Destination $kit
# the source entry is in sdk/, the exported entry sits at the kit root
$kitReadme = (Get-Content -LiteralPath (Join-Path $modRoot 'sdk/README.md') -Raw).Replace('](../', '](')
[IO.File]::WriteAllText((Join-Path $kit 'README.md'), $kitReadme, [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath $reference -Destination $kit -Recurse
Copy-Item -LiteralPath (Join-Path $modRoot 'api') -Destination $kit -Recurse
$kitExamples = Join-Path $kit 'examples'
New-Item -ItemType Directory -Path $kitExamples | Out-Null
Get-ChildItem -LiteralPath (Join-Path $modRoot 'examples') -Force | Copy-Item -Destination $kitExamples -Recurse
Copy-Item -LiteralPath (Join-Path $modRoot 'recipes') -Destination $kit -Recurse
$kitSdk = Join-Path $kit 'sdk'
New-Item -ItemType Directory -Path $kitSdk | Out-Null
Copy-Item -LiteralPath $api,(Join-Path $internal 'MegaMappingApi.xml'),(Join-Path $modRoot 'api/SceneExample.cs'),(Join-Path $modRoot 'api/ModuleExample.cs') -Destination $kitSdk
Copy-Item -LiteralPath $platformLab,(Join-Path $modRoot 'sdk/PlatformLab.cs') -Destination $kitSdk
if ($SceneSmoke) {
    if (-not $SceneRoot) { throw 'SceneSmoke requires an explicit SceneRoot in the construction format' }
    & $sceneCompiler --validate $SceneRoot
    if ($LASTEXITCODE -ne 0) { throw 'Construction scene validation failed' }
}
& $sceneCompiler --schema (Join-Path $kit 'scene-expanded.xsd')
if ($LASTEXITCODE -ne 0) { throw 'Scene schema generation failed' }
& $sceneCompiler --validate $kit
if ($LASTEXITCODE -ne 0) { throw 'Minimal kit validation failed' }
& (Join-Path $modRoot 'check-docs.ps1') -DocumentationRoot $kit
if ($LASTEXITCODE -ne 0) { throw 'Authoring handbook and examples failed validation' }
# publish complete outputs only, keep the previous versions recoverable locally
$moved = @()
try {
    foreach ($pair in @(@($upload,$uploadFinal),@($kit,$kitFinal))) {
        $source = [IO.Path]::GetFullPath($pair[0]); $destination = [IO.Path]::GetFullPath($pair[1])
        foreach ($path in @($source,$destination)) {
            if (-not $path.StartsWith($buildRoot + '\',[StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe package path: $path" }
        }
        $previous = $null
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
        if (Test-Path -LiteralPath $destination) {
            $previous = Join-Path $internal ('previous-output-' + [Guid]::NewGuid().ToString('N'))
            Move-Item -LiteralPath $destination -Destination $previous
        }
        $moved += [pscustomobject]@{ Source=$source; Destination=$destination; Previous=$previous }
        Move-Item -LiteralPath $source -Destination $destination
    }
}
catch {
    [array]::Reverse($moved)
    foreach ($item in $moved) {
        if (Test-Path -LiteralPath $item.Destination) { Move-Item -LiteralPath $item.Destination -Destination $item.Source }
        if ($item.Previous) { Move-Item -LiteralPath $item.Previous -Destination $item.Destination }
    }
    throw
}
Write-Host '[OK] Mod, compiler, focused checks, authoring kit and handbook examples. No map generation or installation.'
} finally {
    $preserveHistory = @($upload,$kit,$testWork | Where-Object { $_ })
    Remove-WorkspaceBuildHistory -Component Mapping -Preserve $preserveHistory | Out-Null
}
