param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$LessonRoot,
    [switch]$ExportImages
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $LessonRoot) { $LessonRoot = Join-Path $PSScriptRoot 'examples/lessons' }
$lessons = (Resolve-Path -LiteralPath $LessonRoot).Path
$internal = Join-Path $repo 'build/mega-mapping-expansion/_INTERNAL'
$output = Join-Path $repo ('build/_work/mme-docs/authoring/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $output | Out-Null
$dependencies = @(
    "$GameDir/JumpKing.exe", "$GameDir/MonoGame.Framework.dll", "$GameDir/LanguageJK.dll", "$GameDir/Steamworks.NET.dll",
    "$repo/build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll", "$PSScriptRoot/lib/0Harmony.dll", "$internal/MegaMappingApi.dll", "$internal/MegaMappingMediaApi.dll"
)
foreach ($dependency in $dependencies) {
    if (-not (Test-Path -LiteralPath $dependency -PathType Leaf)) { throw "Build MME first; missing $dependency" }
    Copy-Item -LiteralPath $dependency -Destination $output
}
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $output
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Recurse -Filter '*.cs' -File | ForEach-Object FullName)
$sources += Join-Path $repo 'mods/jk-runtime/sdk/BoundedTextLog.cs'
$sources += Join-Path $PSScriptRoot 'sdk/AuthoringLessons.cs'
$references = @($dependencies | ForEach-Object { "/reference:$_" })
$resources = @("/resource:$internal/compiler-identity.txt,MegaMapping.CompilerIdentity")
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'recipes') -Filter '*.xml' -File | ForEach-Object {
    $resources += "/resource:$($_.FullName),MegaMapping.Recipes.$($_.Name)"
}
$exe = Join-Path $output 'AuthoringLessons.exe'
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo /optimize+ /target:exe /platform:x64 /langversion:5 /main:MegaMappingExpansion.AuthoringLessons /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.Linq.dll "/out:$exe" $references $resources $sources
if ($LASTEXITCODE -ne 0) { throw 'Authoring lesson checker compilation failed' }
& $exe $output $GameDir $lessons
if ($LASTEXITCODE -ne 0) { throw 'Authoring lesson checks failed' }
if ($ExportImages) {
    $images = Join-Path $PSScriptRoot 'docs/images'
    New-Item -ItemType Directory -Force -Path $images | Out-Null
    Get-ChildItem -LiteralPath $output -Filter 'lesson-*.png' -File | Copy-Item -Destination $images -Force
}
Write-Host "[OK] Authoring lessons and renderer proofs: $output"
