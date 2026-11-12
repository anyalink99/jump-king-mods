param([Parameter(Mandatory=$true)][string]$Implementation,
      [Parameter(Mandatory=$true)][string]$Output,
      [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
      [string[]]$References = @(),
      [string[]]$SharedReferences = @())
$ErrorActionPreference = 'Stop'
$taskRepository = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$taskBuilder = Join-Path $taskRepository 'build\jk-runtime\_INTERNAL\PackageBuilder.exe'
$sharedArguments = @($SharedReferences | ForEach-Object { '--shared-api=' + $_ })
& $taskBuilder $Implementation $Output $GameDir @References @sharedArguments
if ($LASTEXITCODE -ne 0) { throw 'Runtime package compilation failed' }
