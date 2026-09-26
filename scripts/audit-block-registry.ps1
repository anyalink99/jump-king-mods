param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskBuild = Join-Path $taskRepo 'build\block-registry'
$taskWorkshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop\content\1061090'
New-Item -ItemType Directory -Force -Path $taskBuild | Out-Null
$taskCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $taskCompiler /nologo /optimize+ /target:exe /langversion:5 "/out:$taskBuild\BlockRegistryAudit.exe" "/reference:$GameDir\JumpKing.exe" "/reference:$GameDir\MonoGame.Framework.dll" /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'BlockRegistryAudit.cs')
if ($LASTEXITCODE -ne 0) { throw 'Registry tool compilation failed' }
Copy-Item -LiteralPath (Join-Path $GameDir 'JumpKing.exe'),(Join-Path $GameDir 'MonoGame.Framework.dll') -Destination $taskBuild -Force
& "$taskBuild\BlockRegistryAudit.exe" $GameDir $taskWorkshop "$taskBuild\installed.json"
if ($LASTEXITCODE -ne 0) { throw 'Incomplete registry: inspect errors before reserving colours' }
