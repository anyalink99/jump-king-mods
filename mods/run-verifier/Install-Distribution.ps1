param([string]$GameDir='C:\Program Files (x86)\Steam\steamapps\common\Jump King',[switch]$IncludeReplays)
$ErrorActionPreference='Stop'
if(Get-Process -Name JumpKing -ErrorAction SilentlyContinue){throw 'Close Jump King before installing.'}
$taskGame=(Resolve-Path -LiteralPath $GameDir).Path
if(-not(Test-Path -LiteralPath (Join-Path $taskGame 'JumpKing.exe'))){throw 'Jump King executable not found.'}
$taskLocal=Join-Path $taskGame 'Content/JKMods'
$taskWorkshop=Join-Path (Split-Path (Split-Path $taskGame -Parent) -Parent) 'workshop/content/1061090'
$taskNames=@('JKRuntime.dll','RunVerifier.dll');if($IncludeReplays){$taskNames+='Replays.dll'}
$taskBackup=Join-Path $taskGame ('Content/RunVerifier/install-backups/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
$taskOperations=@()
foreach($taskName in $taskNames){
    $taskFound=@(@($taskLocal,$taskWorkshop)|Where-Object {Test-Path -LiteralPath $_}|ForEach-Object {Get-ChildItem -LiteralPath $_ -File -Recurse -Filter $taskName})
    if($taskFound.Count -gt 1){throw "Multiple active copies of $taskName found. Resolve duplicate installations first."}
    $taskTarget=if($taskFound.Count){$taskFound[0].FullName}else{Join-Path $taskLocal $taskName}
    $taskSource=Join-Path $PSScriptRoot $(if($taskName -eq 'Replays.dll'){'optional/Replays.dll'}else{'required/'+$taskName})
    if(-not(Test-Path -LiteralPath $taskSource)){throw "Missing package file: $taskName"}
    $taskOperations+=@{Source=$taskSource;Target=$taskTarget;Name=$taskName}
}
New-Item -ItemType Directory -Force -Path $taskBackup,$taskLocal|Out-Null
foreach($taskOp in $taskOperations){if(Test-Path -LiteralPath $taskOp.Target){Copy-Item -LiteralPath $taskOp.Target -Destination (Join-Path $taskBackup $taskOp.Name)};Copy-Item -LiteralPath $taskOp.Source -Destination $taskOp.Target -Force;Write-Host ('Installed '+$taskOp.Target)}
Write-Host ('Rollback backups: '+$taskBackup)
