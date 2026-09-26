param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$ModRoot = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $ModRoot '..\..')).Path
$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$BuildRoot = Join-Path $RepoRoot 'build\replays'
$UploadDir = Join-Path $BuildRoot 'UPLOAD_TO_WORKSHOP'
$InternalDir = Join-Path $BuildRoot '_INTERNAL'
$Assembly = Join-Path $InternalDir 'Replays.Module.dll'
$Runtime = Join-Path $RepoRoot 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
$Dependencies = @((Join-Path $GameDir 'JumpKing.exe'), (Join-Path $GameDir 'MonoGame.Framework.dll'), (Join-Path $GameDir 'LanguageJK.dll'), $Runtime)
New-Item -ItemType Directory -Force -Path $UploadDir,$InternalDir | Out-Null
foreach ($dependency in $Dependencies) { if (-not (Test-Path -LiteralPath $dependency)) { throw "Missing dependency: $dependency" } }
$References = @($Dependencies | ForEach-Object { "/reference:$_" }) + '/reference:System.IO.Compression.dll'
$Sources = @(Get-ChildItem (Join-Path $ModRoot 'src') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
& $Compiler /nologo /optimize+ /target:library /langversion:5 "/out:$Assembly" $References $Sources
if ($LASTEXITCODE -ne 0) { throw 'Replays compilation failed' }
Copy-Item -LiteralPath $Dependencies -Destination $InternalDir -Force
Copy-Item -LiteralPath (Join-Path $GameDir 'Steamworks.NET.dll') -Destination $InternalDir -Force
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $InternalDir -Force
$TestExecutable = Join-Path $InternalDir 'ReplaysTests.exe'
& $Compiler /nologo /optimize+ /target:exe /langversion:5 "/out:$TestExecutable" $References $Sources (Join-Path $ModRoot 'tests\ReplaysTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Replays test compilation failed' }
& $TestExecutable
if ($LASTEXITCODE -ne 0) { throw 'Replays tests failed' }
& (Join-Path $RepoRoot 'mods\jk-runtime\sdk\package.ps1') -Implementation $Assembly -Output (Join-Path $UploadDir 'Replays.dll') -GameDir $GameDir
