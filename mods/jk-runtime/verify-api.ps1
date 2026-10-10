param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Runtime build/tests failed' }
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$taskAssembly = Join-Path $taskRepo 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
$taskIdentity = [Reflection.AssemblyName]::GetAssemblyName($taskAssembly)
if ($taskIdentity.Name -ne 'JKRuntime' -or $taskIdentity.Version.Major -ne 1) { throw 'Wrong runtime SDK identity' }
Write-Host '[OK] JKRuntime API 2.0 with preserved assembly identity; typed SDK examples and runtime/UI tests passed.'
