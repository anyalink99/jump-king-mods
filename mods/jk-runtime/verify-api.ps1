param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Runtime build/tests failed' }
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$taskAssembly = Join-Path $taskRepo 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
$taskIdentity = [Reflection.AssemblyName]::GetAssemblyName($taskAssembly)
if ($taskIdentity.Name -ne 'JKRuntime' -or $taskIdentity.Version.Major -ne 1) { throw 'Wrong runtime SDK identity' }
Write-Host '[OK] Clean-break JKRuntime API 1.x; typed SDK examples and runtime/UI tests passed.'
