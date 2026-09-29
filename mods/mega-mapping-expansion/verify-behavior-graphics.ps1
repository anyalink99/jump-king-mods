param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$build = Join-Path $repo 'build/mega-mapping-expansion/_INTERNAL'
$output = Join-Path $build ('behavior-graphics-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null
$dependencies = @("$GameDir/JumpKing.exe", "$GameDir/MonoGame.Framework.dll", "$GameDir/LanguageJK.dll", "$GameDir/Steamworks.NET.dll", "$repo/build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll", "$PSScriptRoot/lib/0Harmony.dll", "$build/MegaMappingApi.dll")
foreach ($dependency in $dependencies) { Copy-Item -LiteralPath $dependency -Destination $output -Force }
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $output -Force
$references = @($dependencies | ForEach-Object { "/reference:$_" })
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Recurse -Filter '*.cs' | ForEach-Object FullName)
$exe = Join-Path $output 'BehaviorGraphics.exe'
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo /optimize+ /target:exe /platform:x64 /langversion:5 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.Linq.dll "/resource:$build/compiler-identity.txt,MegaMapping.CompilerIdentity" "/out:$exe" $references $sources (Join-Path $PSScriptRoot 'tools/BehaviorGraphics.cs')
if ($LASTEXITCODE -ne 0) { throw 'Behavior graphics compilation failed' }
& $exe $GameDir (Join-Path $PSScriptRoot 'examples/behaviors') $output -debug
if ($LASTEXITCODE -ne 0) { throw 'Behavior graphics checks failed' }
