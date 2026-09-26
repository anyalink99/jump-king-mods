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
& (Join-Path $repoRoot 'mods\jk-runtime\build.ps1') -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { throw 'JK Runtime dependency build failed' }
foreach ($required in @($compiler,"$GameDir\JumpKing.exe","$GameDir\MonoGame.Framework.dll","$GameDir\LanguageJK.dll",$runtime,$harmony)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required build input is missing: $required" }
}
New-Item -ItemType Directory -Force -Path $internal,$upload | Out-Null
$api = Join-Path $internal 'MegaMappingApi.dll'
& $compiler /nologo /target:library /langversion:5 /nowarn:1591 "/out:$api" "/doc:$(Join-Path $internal 'MegaMappingApi.xml')" (Join-Path $modRoot 'api/MappingApi.cs')
if ($LASTEXITCODE -ne 0) { throw 'Mapping API contract compilation failed' }
$consumer = Join-Path $internal 'SceneExample.dll'
& $compiler /nologo /target:library /langversion:5 "/out:$consumer" "/reference:$api" (Join-Path $modRoot 'api/SceneExample.cs')
if ($LASTEXITCODE -ne 0) { throw 'External Mapping API consumer compilation failed' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $modRoot 'src') -Recurse -Filter '*.cs' -File | Sort-Object FullName | ForEach-Object FullName)
$identityFile = Join-Path $internal 'compiler-identity.txt'
# The source compiler and runtime embed the same semantic compiler identity.
$identity = (@($sources | Where-Object { $_ -match '[\\/]Authoring[\\/]' } | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }) -join '')
[IO.File]::WriteAllText($identityFile,$identity)
$resources = @("/resource:$identityFile,MegaMapping.CompilerIdentity")
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
& $tests $internal $sample
if ($LASTEXITCODE -ne 0) { throw 'Mega Mapping focused checks failed' }
if ($SceneSmoke) {
    if (-not $SceneRoot) { $SceneRoot = Join-Path $repoRoot 'build\mega-mapping-showcase\UPLOAD_TO_WORKSHOP' }
    & $tests $internal $SceneRoot scene
    if ($LASTEXITCODE -ne 0) { throw 'Night Garden scene smoke failed; build the map first' }
}
$sceneCompiler = Join-Path $internal 'SceneCacheCompiler.exe'
& $compiler /nologo /optimize+ /target:exe /langversion:5 /main:MegaMappingExpansion.SceneCacheCompiler "/out:$sceneCompiler" $refs $resources $sources (Join-Path $modRoot 'tools\SceneCacheCompiler.cs')
if ($LASTEXITCODE -ne 0) { throw 'Scene compiler compilation failed' }
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
Copy-Item -LiteralPath (Join-Path $sample 'props') -Destination $kit -Recurse -Force
$kitDocs = Join-Path $kit 'docs'
New-Item -ItemType Directory -Path $kitDocs | Out-Null
# Keep the source handbook layout in the portable kit; provenance stays private.
Get-ChildItem -LiteralPath (Join-Path $modRoot 'docs') -Filter '*.md' -File |
    Where-Object Name -ne 'art-sources.md' | Copy-Item -Destination $kitDocs
Copy-Item -LiteralPath (Join-Path $modRoot 'THIRD_PARTY_NOTICES.md') -Destination $kit
# The source entry is in sdk/; the exported entry sits at the kit root.
$kitReadme = (Get-Content -LiteralPath (Join-Path $modRoot 'sdk/README.md') -Raw).Replace('](../', '](')
[IO.File]::WriteAllText((Join-Path $kit 'README.md'), $kitReadme, [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath $reference -Destination $kit -Recurse
Copy-Item -LiteralPath (Join-Path $modRoot 'examples/narrative') -Destination $kit -Recurse
& $sceneCompiler (Join-Path $kit 'narrative') (Join-Path $kit 'narrative/props/mega-mapping-expansion/scene.mmgfx')
if ($LASTEXITCODE -ne 0) { throw 'Narrative kit compilation failed' }
Copy-Item -LiteralPath (Join-Path $modRoot 'tools\preview.ps1') -Destination $kit -Force
$kitSdk = Join-Path $kit 'sdk'
New-Item -ItemType Directory -Path $kitSdk | Out-Null
Copy-Item -LiteralPath $api,(Join-Path $internal 'MegaMappingApi.xml'),(Join-Path $modRoot 'api/SceneExample.cs'),(Join-Path $modRoot 'api/ModuleExample.cs') -Destination $kitSdk
Copy-Item -LiteralPath (Join-Path $modRoot 'examples/behaviors') -Destination $kit -Recurse
& $sceneCompiler (Join-Path $kit 'behaviors') (Join-Path $kit 'behaviors/props/mega-mapping-expansion/scene.mmgfx')
if ($LASTEXITCODE -ne 0) { throw 'Behavior kit compilation failed' }
Copy-Item -LiteralPath (Join-Path $modRoot 'examples/behavior-trees') -Destination $kit -Recurse
& $sceneCompiler (Join-Path $kit 'behavior-trees') (Join-Path $kit 'behavior-trees/props/mega-mapping-expansion/scene.mmgfx')
if ($LASTEXITCODE -ne 0) { throw 'Behavior tree / native ending kit compilation failed' }
& $sceneCompiler --schema (Join-Path $kit 'scene-expanded.xsd')
if ($LASTEXITCODE -ne 0) { throw 'Scene schema generation failed' }
& $sceneCompiler $kit (Join-Path $kit 'props\mega-mapping-expansion\scene.mmgfx')
if ($LASTEXITCODE -ne 0) { throw 'Minimal kit compilation failed' }
# Publish only complete outputs; retain recoverable previous versions locally.
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
Write-Host '[OK] Mod, compiler, focused checks and minimal kit. No map generation or installation.'
