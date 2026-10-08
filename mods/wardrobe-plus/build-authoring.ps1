param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = Join-Path $repo 'build/wardrobe-plus/_INTERNAL/authoring'
$module = Join-Path $repo 'build/wardrobe-plus/_INTERNAL/WardrobePlus.Module.dll'
if (-not (Test-Path -LiteralPath $module)) { & (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$dependencies = @($module, (Join-Path $GameDir 'JumpKing.exe'), (Join-Path $GameDir 'MonoGame.Framework.dll'), (Join-Path $GameDir 'Steamworks.NET.dll'), (Join-Path $GameDir 'LanguageJK.dll'), (Join-Path $repo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'))
Copy-Item -LiteralPath $dependencies -Destination $output -Force
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $output -Force
$references = @($dependencies | ForEach-Object { "/reference:$_" })
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /platform:x64 /langversion:5 /reference:System.Drawing.dll /reference:System.Xml.Linq.dll /reference:System.Web.Extensions.dll "/out:$output/SkinTool.exe" $references (Join-Path $PSScriptRoot 'tools/SkinTool.cs')
if ($LASTEXITCODE -ne 0) { throw 'Skin authoring tool compilation failed' }
Write-Host "[OK] Authoring tool: $output/SkinTool.exe"
