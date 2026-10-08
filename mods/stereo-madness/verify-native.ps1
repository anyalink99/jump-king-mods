param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$Internal = Join-Path $RepoRoot 'build/stereo-madness-mod/_INTERNAL'
$Runtime = Join-Path $RepoRoot 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$MappingApi = Join-Path $RepoRoot 'build/mega-mapping-expansion/UPLOAD_TO_WORKSHOP/MegaMappingApi.dll'
$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$Sources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$Native = @('JumpKing.exe','MonoGame.Framework.dll','LanguageJK.dll','Steamworks.NET.dll') | ForEach-Object { Join-Path $GameDir $_ }
$References = @($Native + $Runtime | ForEach-Object { "/reference:$_" })
Copy-Item -LiteralPath $MappingApi -Destination $Internal -Force
$References += "/reference:$MappingApi"
$References += "/reference:$RepoRoot/mods/subframe-charge/lib/0Harmony.dll"
Copy-Item $Native -Destination $Internal -Force
Copy-Item $Runtime -Destination $Internal -Force
Get-ChildItem $GameDir -Filter 'SharpDX*.dll' | Copy-Item -Destination $Internal -Force
Copy-Item (Join-Path $RepoRoot 'mods/subframe-charge/lib/0Harmony.dll') $Internal -Force
$Test = Join-Path $Internal 'NativeTests.exe'
& $Compiler /nologo /optimize+ /target:exe /platform:x64 /langversion:5 /reference:System.Windows.Forms.dll /reference:System.Xml.Linq.dll "/out:$Test" $References $Sources (Join-Path $PSScriptRoot 'tests/NativeTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Native fixture compilation failed' }
$Arguments = @($GameDir, (Join-Path $RepoRoot 'build/stereo-madness/UPLOAD_TO_WORKSHOP'))
$Camera = Join-Path $RepoRoot 'build/smooth-camera/_INTERNAL/SmoothCamera.Module.dll'
if (Test-Path -LiteralPath $Camera) { $Arguments += $Camera }
& $Test @Arguments
if ($LASTEXITCODE -ne 0) { throw 'Native fixture failed' }
