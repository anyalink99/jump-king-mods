param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [ValidateSet('jk-runtime','free-scaling','casual-jumping','subframe-charge','morph-ball','more-items','replays','run-verifier','wardrobe-plus','no-walk-off','powers','mega-mapping-expansion','smooth-camera','overlay-plus','god','prism','multiplayer-expansion')][string[]]$Include = @(),
    [switch]$OnlyRequested,
    [switch]$RemoveLocalScreenSolver,
    [string]$BackupRoot,
    [string]$Candidate
)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name JumpKing -ErrorAction SilentlyContinue) { throw 'Close Jump King before installing the coordinated runtime release.' }
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $PSScriptRoot 'build-support/build-retention.ps1')
if (-not $BackupRoot) { $BackupRoot = Join-Path $taskRepo 'build/jk-runtime/_INTERNAL' }
$BackupRoot = Assert-BuildBackupPath $BackupRoot
$taskHistoryBuildRoot = if ($PSBoundParameters.ContainsKey('BackupRoot')) { Split-Path -Parent $BackupRoot } else { Join-Path $taskRepo 'build' }
if ($Candidate) {
    . (Join-Path $taskRepo 'scripts/release-candidate.ps1')
    $taskCandidate = Test-ModCandidate $Candidate
    if (-not $OnlyRequested) { throw 'A fixed candidate requires OnlyRequested and an explicit Include list' }
    foreach ($taskId in @('jk-runtime') + $Include) { if ($taskCandidate.Components -notcontains $taskId) { throw "Component is absent from candidate: $taskId" } }
} else {
$taskCheckScript = (Join-Path $taskRepo 'scripts\check-mods.ps1').Replace("'", "''")
$taskCheckGame = $GameDir.Replace("'", "''")
if ($OnlyRequested) {
    if ($Include.Count -eq 0) { throw 'OnlyRequested requires at least one mod in Include.' }
    $taskSelection = ($Include | ForEach-Object { "'" + $_.Replace("'", "''") + "'" }) -join ','
    $taskCheckCommand = "& '$taskCheckScript' -GameDir '$taskCheckGame' -Integration -Mod @($taskSelection); if (-not `$?) { exit 1 }"
} else {
    $taskCheckCommand = "& '$taskCheckScript' -GameDir '$taskCheckGame' -Integration; if (-not `$?) { exit 1 }"
}
# use files so a nested shell can't mistake native stderr for CLIXML
$taskCheckBase = if ($PSBoundParameters.ContainsKey('BackupRoot')) { Join-Path $BackupRoot 'verification' } else { Join-Path $taskRepo 'build/_work/runtime-install' }
$taskCheckRoot = Join-Path $taskCheckBase ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskCheckRoot -Force | Out-Null
$taskCheckOutput = Join-Path $taskCheckRoot 'stdout.log'
$taskCheckError = Join-Path $taskCheckRoot 'stderr.log'
$taskEngineName = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh.exe' } else { 'powershell.exe' }
$taskCheckArguments = @('-NoProfile','-OutputFormat','Text','-ExecutionPolicy','Bypass','-EncodedCommand',([Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($taskCheckCommand))))
Write-Host "Verifying release; logs: $taskCheckRoot"
$taskCheckProcess = Start-Process -FilePath (Join-Path $PSHOME $taskEngineName) -ArgumentList $taskCheckArguments -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $taskCheckOutput -RedirectStandardError $taskCheckError
foreach ($taskCheckLog in @($taskCheckOutput,$taskCheckError)) {
    if (Test-Path -LiteralPath $taskCheckLog) { Get-Content -LiteralPath $taskCheckLog | ForEach-Object { Write-Host $_ } }
}
if ($taskCheckProcess.ExitCode -ne 0) { throw "Release verification failed (exit $($taskCheckProcess.ExitCode)); installation was not started. Logs: $taskCheckRoot" }
if (Get-Process -Name JumpKing -ErrorAction SilentlyContinue) { throw 'Jump King was started during verification; close it before installation.' }
}
$taskLocal = Join-Path $GameDir 'Content\JKMods'
$taskWorkshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop\content\1061090'
$taskRoots = @($taskLocal,$taskWorkshop) | Where-Object { Test-Path -LiteralPath $_ -PathType Container }
$taskFiles = @($taskRoots | ForEach-Object { Get-ChildItem -LiteralPath $_ -File -Recurse -Filter '*.dll' })
$taskNames = [ordered]@{'jk-runtime'='JKRuntime.dll'; 'casual-jumping'='CasualJumping.dll'; 'subframe-charge'='SubframeCharge.dll'; 'morph-ball'='MorphBall.dll'; 'more-items'='MoreItems.dll'; 'replays'='Replays.dll'; 'wardrobe-plus'='WardrobePlus.dll'; 'no-walk-off'='NoWalkOff.dll'; 'powers'='Powers.dll'; 'mega-mapping-expansion'='MegaMappingExpansion.dll'; 'overlay-plus'='OverlayPlus.dll'; 'smooth-camera'='SmoothCamera.dll'}
$taskOperations = @()
$taskNames['run-verifier'] = 'RunVerifier.dll'
$taskNames['god'] = 'God.dll'
$taskNames['prism'] = 'Prism.dll'
$taskNames['free-scaling'] = 'FreeScaling.dll'
$taskNames['multiplayer-expansion'] = 'MultiplayerExpansion.dll'
foreach ($taskId in $taskNames.Keys) {
    if ($OnlyRequested -and $taskId -ne 'jk-runtime' -and $Include -notcontains $taskId) { continue }
    $taskName = $taskNames[$taskId]
    $taskMatches = @($taskFiles | Where-Object { $_.Name -eq $taskName -or ($taskId -eq 'jk-runtime' -and $_.Name -eq 'UIApiPlus.dll') -or ($taskId -eq 'no-walk-off' -and $_.Name -eq 'MegaGameplayExpansion.dll') })
    $taskDirectories = @($taskMatches.DirectoryName | Select-Object -Unique)
    if ($taskDirectories.Count -gt 1) { throw "Duplicate $taskId installations: $($taskDirectories -join ', '). Resolve duplicates before installing." }
    if ($taskDirectories.Count -eq 0 -and $taskId -ne 'jk-runtime' -and $Include -notcontains $taskId) { continue }
    $taskDestination = if ($taskDirectories.Count -eq 1) { $taskDirectories[0] } else { $taskLocal }
    $taskTarget = [IO.Path]::GetFullPath((Join-Path $taskDestination $taskName))
    $taskAllowed = @($taskLocal,$taskWorkshop) | Where-Object { $taskTarget.StartsWith([IO.Path]::GetFullPath($_).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) }
    if ($taskAllowed.Count -eq 0) { throw "Installation path escaped the explicit mod roots: $taskTarget" }
    $taskSource = if ($Candidate) { Join-Path $Candidate ($taskId+'/'+$taskName) } else { Join-Path $taskRepo ('build\'+$taskId+'\UPLOAD_TO_WORKSHOP\'+$taskName) }
    if (-not (Test-Path -LiteralPath $taskSource -PathType Leaf)) { throw "Missing verified payload: $taskSource" }
    $taskOperations += [pscustomobject]@{ Id=$taskId; Source=$taskSource; Target=$taskTarget; Existed=(Test-Path -LiteralPath $taskTarget); Backup=$null }
    if ($taskId -in @('mega-mapping-expansion','overlay-plus','subframe-charge','smooth-camera','god','prism','free-scaling','multiplayer-expansion')) {
        $taskDependencyFiles = if ($taskId -eq 'overlay-plus') { @('0Harmony.dll','OverlayPlusApi.dll','THIRD_PARTY_NOTICES.md') } elseif ($taskId -in @('subframe-charge','smooth-camera','god','prism','free-scaling')) { @('0Harmony.dll','THIRD_PARTY_NOTICES.md') } else { @('0Harmony.dll','MegaMappingApi.dll','THIRD_PARTY_NOTICES.md') }
        if ($taskId -eq 'mega-mapping-expansion') { $taskDependencyFiles += @('assets/platforms/default.png','MegaMappingMediaApi.dll') }
        if ($taskId -eq 'multiplayer-expansion') {
            $taskPackageRoot = Split-Path $taskSource
            $taskDependencyFiles = @(Get-ChildItem -LiteralPath $taskPackageRoot -File -Recurse | Where-Object Name -ne 'MultiplayerExpansion.dll' | ForEach-Object { $_.FullName.Substring($taskPackageRoot.Length + 1) })
        }
        foreach ($taskDependency in $taskDependencyFiles) {
            $taskDependencySource = Join-Path (Split-Path $taskSource) $taskDependency
            $taskDependencyName = if ($taskDependency -eq 'THIRD_PARTY_NOTICES.md') { [IO.Path]::GetFileNameWithoutExtension($taskName)+'.THIRD_PARTY_NOTICES.md' } else { $taskDependency }
            $taskDependencyTarget = Join-Path $taskDestination $taskDependencyName
            if (-not (Test-Path -LiteralPath $taskDependencySource -PathType Leaf)) { throw "Missing $taskId dependency: $taskDependencySource" }
            if (Test-Path -LiteralPath $taskDependencyTarget -PathType Leaf) {
                if ((Get-FileHash -LiteralPath $taskDependencySource).Hash -eq (Get-FileHash -LiteralPath $taskDependencyTarget).Hash) { continue }
                if ($taskDependency -eq '0Harmony.dll') { throw "$taskId needs a different Harmony DLL; refusing to overwrite a shared dependency: $taskDependencyTarget" }
            }
            $taskOperations += [pscustomobject]@{ Id=($taskId+'.'+$taskDependency.Replace('/','_').Replace('\','_')); Source=$taskDependencySource; Target=$taskDependencyTarget; Existed=(Test-Path -LiteralPath $taskDependencyTarget); Backup=$null }
        }
    }
}
if ($Candidate) { Test-ModCandidate $Candidate | Out-Null }
# freeze the exact verified operation bytes before replacing any installed file
$taskFixedRoot = Join-Path $taskRepo ('build/_work/install-candidates/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $taskFixedRoot | Out-Null
$taskFixedFiles = @()
foreach ($taskOperation in $taskOperations) {
    $taskHash = (Get-FileHash -LiteralPath $taskOperation.Source).Hash
    $taskFixedFile = Join-Path $taskFixedRoot ($taskOperation.Id + '.payload')
    Copy-Item -LiteralPath $taskOperation.Source -Destination $taskFixedFile
    if ((Get-FileHash -LiteralPath $taskFixedFile).Hash -ne $taskHash) { throw "Install source changed during staging: $($taskOperation.Id)" }
    $taskOperation.Source = $taskFixedFile
    $taskFixedFiles += @{ Id=$taskOperation.Id; Target=$taskOperation.Target; SHA256=$taskHash }
}
[IO.File]::WriteAllText((Join-Path $taskFixedRoot 'receipt.json'),($taskFixedFiles | ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
$taskBackup = Join-Path $BackupRoot ('release-backup-'+[Guid]::NewGuid().ToString('N'))
Start-BuildBackup $taskBackup
$taskChanged = @()
$taskOldUi = @($taskFiles | Where-Object { $_.Name -eq 'UIApiPlus.dll' -or ($_.Name -in @('Jetpack.dll','HammerKing.dll') -and @($taskOperations.Id) -contains 'more-items') })
if (@($taskOperations.Id) -contains 'multiplayer-expansion') { $taskOldUi += @($taskFiles | Where-Object Name -eq 'MultiplayerExpansion.World.dll') }
if (@($taskOperations.Id) -contains 'no-walk-off') {
    $taskOldUi += @($taskFiles | Where-Object Name -eq 'MegaGameplayExpansion.dll')
}
if ($RemoveLocalScreenSolver) {
    $taskSolver = Join-Path $taskLocal 'ScreenSolver.dll'
    if (Test-Path -LiteralPath $taskSolver -PathType Leaf) { $taskOldUi += Get-Item -LiteralPath $taskSolver }
}
$taskRuntimeOperation = $taskOperations | Where-Object Id -eq 'jk-runtime'
$taskRuntimeDirectory = Split-Path $taskRuntimeOperation.Target
$taskOldSettings = Join-Path $taskRuntimeDirectory 'UIApiPlus.Settings.xml'
try {
    foreach ($taskOperation in $taskOperations) {
        $taskExpected = @($taskFixedFiles | Where-Object Id -eq $taskOperation.Id)[0].SHA256
        if ((Get-FileHash -LiteralPath $taskOperation.Source).Hash -ne $taskExpected) { throw "Fixed install payload changed: $($taskOperation.Id)" }
        New-Item -ItemType Directory -Force -Path (Split-Path $taskOperation.Target) | Out-Null
        if ($taskOperation.Existed) {
            $taskOperation.Backup = Join-Path $taskBackup ($taskOperation.Id+'.dll')
            Copy-Item -LiteralPath $taskOperation.Target -Destination $taskOperation.Backup
        }
        $taskChanged += $taskOperation
        Copy-Item -LiteralPath $taskOperation.Source -Destination $taskOperation.Target -Force
        if ((Get-FileHash -LiteralPath $taskOperation.Source).Hash -ne (Get-FileHash -LiteralPath $taskOperation.Target).Hash) { throw "Installed hash mismatch: $($taskOperation.Id)" }
    }
    # runtime handles the one-time schema migration on first startup
    # that also covers Steam updates that skip this installer, keep an extra backup here
    if (Test-Path -LiteralPath $taskOldSettings) {
        Copy-Item -LiteralPath $taskOldSettings -Destination (Join-Path $taskBackup 'UIApiPlus.Settings.xml')
    }
    foreach ($taskOld in $taskOldUi) {
        $taskOldBackup = Join-Path $taskBackup (($taskOldUi.IndexOf($taskOld)).ToString() + '-' + $taskOld.Name)
        Copy-Item -LiteralPath $taskOld.FullName -Destination $taskOldBackup
        Remove-Item -LiteralPath $taskOld.FullName
    }
    $taskOperations | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskBackup 'manifest.json') -Encoding UTF8
}
catch {
    $taskFailure = $_
    $taskRollbackErrors = @()
    for ($taskIndex = $taskChanged.Count - 1; $taskIndex -ge 0; $taskIndex--) {
        $taskOperation = $taskChanged[$taskIndex]
        try {
            if ($taskOperation.Existed) { Copy-Item -LiteralPath $taskOperation.Backup -Destination $taskOperation.Target -Force }
            elseif (Test-Path -LiteralPath $taskOperation.Target) { Remove-Item -LiteralPath $taskOperation.Target }
        } catch { $taskRollbackErrors += $_.ToString() }
    }
    foreach ($taskOld in $taskOldUi) {
        try {
            $taskOldBackup = Join-Path $taskBackup (($taskOldUi.IndexOf($taskOld)).ToString() + '-' + $taskOld.Name)
            if (Test-Path -LiteralPath $taskOldBackup) { Copy-Item -LiteralPath $taskOldBackup -Destination $taskOld.FullName -Force }
        } catch { $taskRollbackErrors += $_.ToString() }
    }
    if ($taskRollbackErrors.Count) { throw "Install failed: $taskFailure. ROLLBACK INCOMPLETE: $($taskRollbackErrors -join '; '). Backups: $taskBackup" }
    Complete-BuildBackup $taskBackup
    throw "Install failed and DLL changes were rolled back: $taskFailure. Backups: $taskBackup"
}
Complete-BuildBackup $taskBackup
Invoke-WorkspaceMaintenance -Component Runtime -BuildRoot $taskHistoryBuildRoot -BackupDirectory $BackupRoot | Out-Null
Write-Host "[OK] Coordinated runtime release installed: $($taskOperations.Id -join ', ')"
Write-Host "[OK] Old DLLs recoverable from: $taskBackup"
Write-Host '[OK] Saves, inventory, replays and per-mod settings were preserved. UI settings migrate once on first startup.'

[IO.File]::WriteAllText((Join-Path $taskFixedRoot '.build-complete'),'')
Remove-BuildArtifactHistory -Directory (Split-Path $taskFixedRoot) -NamePattern '^[0-9a-f]{32}$' -Keep 2 -CompletionMarker '.build-complete' -Preserve @($taskFixedRoot) | Out-Null
