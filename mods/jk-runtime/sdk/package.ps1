param([Parameter(Mandatory=$true)][string]$Implementation,
      [Parameter(Mandatory=$true)][string]$Output,
      [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
      [string[]]$References = @())
$ErrorActionPreference = 'Stop'
$taskRepository = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$taskBuilder = Join-Path $taskRepository 'build\jk-runtime\_INTERNAL\PackageBuilder.exe'
& $taskBuilder $Implementation $Output $GameDir @References
if ($LASTEXITCODE -ne 0) { throw 'Runtime package compilation failed' }
