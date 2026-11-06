param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King', [string]$HistoryDirectory)
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $HistoryDirectory) { $HistoryDirectory = Join-Path $taskRepo 'build/_work/metroidvania/install' }
. (Join-Path $taskRepo 'mods/jk-runtime/build-support/build-retention.ps1')
function Assert-GameClosed {
    $taskExpectedExe = [IO.Path]::GetFullPath((Join-Path $GameDir 'JumpKing.exe'))
    foreach ($taskProcess in Get-Process JumpKing -ErrorAction SilentlyContinue) {
        if (-not $taskProcess.Path -or [IO.Path]::GetFullPath($taskProcess.Path) -eq $taskExpectedExe) { throw 'Close Jump King before installing Metroidvania.' }
    }
}
Assert-GameClosed
$taskPackage = Join-Path $taskRepo 'build/metroidvania/UPLOAD_TO_WORKSHOP/Metroidvania.dll'
if (-not (Test-Path -LiteralPath $taskPackage)) { throw 'Build Metroidvania first.' }
$taskLocal = Join-Path $GameDir 'Content/JKMods'
$taskWorkshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop/content/1061090'
$taskRoots = @($taskLocal,$taskWorkshop) | Where-Object { Test-Path -LiteralPath $_ }
$taskInstalled = @($taskRoots | ForEach-Object { Get-ChildItem -LiteralPath $_ -Recurse -File -Filter 'Metroidvania.dll' })
if ($taskInstalled.Count -gt 1) { throw 'Resolve duplicate active Metroidvania copies before installing.' }
$taskRuntime = @($taskRoots | ForEach-Object { Get-ChildItem -LiteralPath $_ -Recurse -File -Filter 'JKRuntime.dll' })
if ($taskRuntime.Count -ne 1 -or [Version]$taskRuntime[0].VersionInfo.FileVersion -lt [Version]'2.4.0.0') { throw 'Install one JK Runtime 2.4.0 or newer first.' }
$taskDestination = if ($taskInstalled.Count) { $taskInstalled[0].DirectoryName } else { Join-Path $taskLocal 'Metroidvania' }
$taskDestination = [IO.Path]::GetFullPath($taskDestination)
if (-not @($taskLocal,$taskWorkshop | Where-Object { $taskDestination.StartsWith([IO.Path]::GetFullPath($_).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) })) { throw 'Installation escaped the game mod roots.' }
$taskHash = (Get-FileHash -LiteralPath $taskPackage -Algorithm SHA256).Hash
$taskTarget = Join-Path $taskDestination 'Metroidvania.dll'
if ((Test-Path -LiteralPath $taskTarget) -and (Get-FileHash -LiteralPath $taskTarget -Algorithm SHA256).Hash -eq $taskHash) { Write-Host '[OK] Metroidvania already matches the package.'; return }
$taskBackup = Join-Path $HistoryDirectory ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N'))
Start-BuildBackup $taskBackup
$taskHadTarget = Test-Path -LiteralPath $taskTarget
if (Test-Path -LiteralPath $taskDestination) {
    $taskBackupContent = Join-Path $taskBackup 'content'
    New-Item -ItemType Directory -Force $taskBackupContent | Out-Null
    if ([IO.Path]::GetFileName($taskDestination) -eq 'Metroidvania') { Get-ChildItem -LiteralPath $taskDestination -Force | Copy-Item -Destination $taskBackupContent -Recurse -Force }
    else { Get-ChildItem -LiteralPath $taskDestination -File -Filter 'Metroidvania*' | Copy-Item -Destination $taskBackupContent -Force }
    foreach ($taskFile in Get-ChildItem -LiteralPath $taskBackupContent -File -Recurse) {
        $taskOriginal = Join-Path $taskDestination $taskFile.FullName.Substring($taskBackupContent.Length+1)
        if ((Get-FileHash -LiteralPath $taskFile.FullName).Hash -ne (Get-FileHash -LiteralPath $taskOriginal).Hash) { throw 'Rollback backup verification failed.' }
    }
}
$taskIncoming = Join-Path $taskDestination ('Metroidvania.incoming-'+[Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Force $taskDestination | Out-Null
    Copy-Item -LiteralPath $taskPackage -Destination $taskIncoming
    if ((Get-FileHash -LiteralPath $taskIncoming -Algorithm SHA256).Hash -ne $taskHash) { throw 'Package staging hash mismatch.' }
    Assert-GameClosed
    Move-Item -LiteralPath $taskIncoming -Destination $taskTarget -Force
    if ((Get-FileHash -LiteralPath $taskTarget -Algorithm SHA256).Hash -ne $taskHash) { throw 'Installed package hash mismatch.' }
    @{Actor='Niotid';Target=$taskTarget;SHA256=$taskHash;Version='0.4.0';HadPrevious=$taskHadTarget} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskBackup 'receipt.json') -Encoding UTF8
    Complete-BuildBackup $taskBackup
} catch {
    if ($taskHadTarget) { Copy-Item -LiteralPath (Join-Path $taskBackup 'content/Metroidvania.dll') -Destination $taskTarget -Force }
    elseif (Test-Path -LiteralPath $taskTarget) { Remove-Item -LiteralPath $taskTarget }
    throw
} finally { if (Test-Path -LiteralPath $taskIncoming) { Remove-Item -LiteralPath $taskIncoming } }
Invoke-WorkspaceMaintenance -Component Metroidvania -BackupDirectory $HistoryDirectory | Out-Null
Write-Host "[OK] Installed Metroidvania 0.4.0: $taskTarget"
