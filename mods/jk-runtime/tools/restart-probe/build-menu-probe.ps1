param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$menuRepo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$menuBuild = Join-Path $menuRepo 'build/jk-runtime/_INTERNAL/restart-probe'
New-Item -ItemType Directory -Force -Path $menuBuild | Out-Null
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo /nowarn:1685 /optimize+ /target:library /langversion:5 "/out:$menuBuild/JKMenuProbe.dll" "/reference:$GameDir/JumpKing.exe" "/reference:$GameDir/MonoGame.Framework.dll" "/reference:$GameDir/Content/JKMods/0Harmony.dll" (Join-Path $PSScriptRoot 'MenuProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Native menu probe compilation failed' }
