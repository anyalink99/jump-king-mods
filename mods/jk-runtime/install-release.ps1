param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [ValidateSet('jk-runtime','casual-jumping','subframe-charge','morph-ball','more-items','replays','run-verifier','wardrobe-plus','mega-gameplay-expansion','mega-mapping-expansion','smooth-camera','overlay-plus')][string[]]$Include = @(),
    [switch]$OnlyRequested,
    [switch]$RemoveLocalScreenSolver
)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name JumpKing -ErrorAction SilentlyContinue) { throw 'Close Jump King before installing the coordinated runtime release.' }
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ($OnlyRequested) {
    if ($Include.Count -eq 0) { throw 'OnlyRequested requires at least one mod in Include.' }
    $taskCheckScript = (Join-Path $taskRepo 'scripts\check-mods.ps1').Replace("'", "''")
    $taskCheckGame = $GameDir.Replace("'", "''")
    $taskSelection = ($Include | ForEach-Object { "'" + $_.Replace("'", "''") + "'" }) -join ','
    $taskCheckCommand = "& '$taskCheckScript' -GameDir '$taskCheckGame' -Integration -Mod @($taskSelection); if (-not `$?) { exit 1 }"
    & powershell.exe -NoProfile -OutputFormat Text -ExecutionPolicy Bypass -EncodedCommand ([Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($taskCheckCommand)))
    if ($LASTEXITCODE -ne 0) { throw 'Focused release verification failed; installation was not started.' }
} else {
    & powershell.exe -NoProfile -OutputFormat Text -ExecutionPolicy Bypass -File (Join-Path $taskRepo 'scripts\check-mods.ps1') -GameDir $GameDir -Integration
    if ($LASTEXITCODE -ne 0) { throw 'Release verification failed; installation was not started.' }
}
if (Get-Process -Name JumpKing -ErrorAction SilentlyContinue) { throw 'Jump King was started during verification; close it before installation.' }
$taskLocal = Join-Path $GameDir 'Content\JKMods'
$taskWorkshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop\content\1061090'
$taskRoots = @($taskLocal,$taskWorkshop) | Where-Object { Test-Path -LiteralPath $_ -PathType Container }
$taskFiles = @($taskRoots | ForEach-Object { Get-ChildItem -LiteralPath $_ -File -Recurse -Filter '*.dll' })
$taskNames = [ordered]@{'jk-runtime'='JKRuntime.dll'; 'casual-jumping'='CasualJumping.dll'; 'subframe-charge'='SubframeCharge.dll'; 'morph-ball'='MorphBall.dll'; 'more-items'='MoreItems.dll'; 'replays'='Replays.dll'; 'wardrobe-plus'='WardrobePlus.dll'; 'mega-gameplay-expansion'='MegaGameplayExpansion.dll'; 'mega-mapping-expansion'='MegaMappingExpansion.dll'; 'overlay-plus'='OverlayPlus.dll'; 'smooth-camera'='SmoothCamera.dll'}
$taskOperations = @()
$taskNames['run-verifier'] = 'RunVerifier.dll'
foreach ($taskId in $taskNames.Keys) {
    if ($OnlyRequested -and $taskId -ne 'jk-runtime' -and $Include -notcontains $taskId) { continue }
    $taskName = $taskNames[$taskId]
    $taskMatches = @($taskFiles | Where-Object { $_.Name -eq $taskName -or ($taskId -eq 'jk-runtime' -and $_.Name -eq 'UIApiPlus.dll') })
    $taskDirectories = @($taskMatches.DirectoryName | Select-Object -Unique)
    if ($taskDirectories.Count -gt 1) { throw "Duplicate $taskId installations: $($taskDirectories -join ', '). Resolve duplicates before installing." }
    if ($taskDirectories.Count -eq 0 -and $taskId -ne 'jk-runtime' -and $Include -notcontains $taskId) { continue }
    $taskDestination = if ($taskDirectories.Count -eq 1) { $taskDirectories[0] } else { $taskLocal }
    $taskTarget = [IO.Path]::GetFullPath((Join-Path $taskDestination $taskName))
    $taskAllowed = @($taskLocal,$taskWorkshop) | Where-Object { $taskTarget.StartsWith([IO.Path]::GetFullPath($_).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) }
    if ($taskAllowed.Count -eq 0) { throw "Installation path escaped the explicit mod roots: $taskTarget" }
    $taskSource = Join-Path $taskRepo ('build\'+$taskId+'\UPLOAD_TO_WORKSHOP\'+$taskName)
    if (-not (Test-Path -LiteralPath $taskSource -PathType Leaf)) { throw "Missing verified payload: $taskSource" }
    $taskOperations += [pscustomobject]@{ Id=$taskId; Source=$taskSource; Target=$taskTarget; Existed=(Test-Path -LiteralPath $taskTarget); Backup=$null }
    if ($taskId -in @('mega-mapping-expansion','overlay-plus','subframe-charge','smooth-camera')) {
        $taskDependencyFiles = if ($taskId -eq 'overlay-plus') { @('0Harmony.dll','OverlayPlusApi.dll','THIRD_PARTY_NOTICES.md') } elseif ($taskId -in @('subframe-charge','smooth-camera')) { @('0Harmony.dll','THIRD_PARTY_NOTICES.md') } else { @('0Harmony.dll','MegaMappingApi.dll','THIRD_PARTY_NOTICES.md') }
        foreach ($taskDependency in $taskDependencyFiles) {
            $taskDependencySource = Join-Path (Split-Path $taskSource) $taskDependency
            $taskDependencyName = if ($taskDependency -eq 'THIRD_PARTY_NOTICES.md') { [IO.Path]::GetFileNameWithoutExtension($taskName)+'.THIRD_PARTY_NOTICES.md' } else { $taskDependency }
            $taskDependencyTarget = Join-Path $taskDestination $taskDependencyName
            if (-not (Test-Path -LiteralPath $taskDependencySource -PathType Leaf)) { throw "Missing $taskId dependency: $taskDependencySource" }
            if (Test-Path -LiteralPath $taskDependencyTarget -PathType Leaf) {
                if ((Get-FileHash -LiteralPath $taskDependencySource).Hash -eq (Get-FileHash -LiteralPath $taskDependencyTarget).Hash) { continue }
                if ($taskDependency -eq '0Harmony.dll') { throw "$taskId needs a different Harmony DLL; refusing to overwrite a shared dependency: $taskDependencyTarget" }
            }
            $taskOperations += [pscustomobject]@{ Id=($taskId+'.'+$taskDependency); Source=$taskDependencySource; Target=$taskDependencyTarget; Existed=(Test-Path -LiteralPath $taskDependencyTarget); Backup=$null }
        }
    }
}
$taskBackup = Join-Path $taskRepo ('build\jk-runtime\_INTERNAL\release-backup-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskBackup | Out-Null
$taskChanged = @()
$taskOldUi = @($taskFiles | Where-Object { $_.Name -eq 'UIApiPlus.dll' -or ($_.Name -in @('Jetpack.dll','HammerKing.dll') -and @($taskOperations.Id) -contains 'more-items') })
if ($RemoveLocalScreenSolver) {
    $taskSolver = Join-Path $taskLocal 'ScreenSolver.dll'
    if (Test-Path -LiteralPath $taskSolver -PathType Leaf) { $taskOldUi += Get-Item -LiteralPath $taskSolver }
}
$taskRuntimeOperation = $taskOperations | Where-Object Id -eq 'jk-runtime'
$taskRuntimeDirectory = Split-Path $taskRuntimeOperation.Target
$taskOldSettings = Join-Path $taskRuntimeDirectory 'UIApiPlus.Settings.xml'
try {
    foreach ($taskOperation in $taskOperations) {
        New-Item -ItemType Directory -Force -Path (Split-Path $taskOperation.Target) | Out-Null
        if ($taskOperation.Existed) {
            $taskOperation.Backup = Join-Path $taskBackup ($taskOperation.Id+'.dll')
            Copy-Item -LiteralPath $taskOperation.Target -Destination $taskOperation.Backup
        }
        $taskChanged += $taskOperation
        Copy-Item -LiteralPath $taskOperation.Source -Destination $taskOperation.Target -Force
        if ((Get-FileHash -LiteralPath $taskOperation.Source).Hash -ne (Get-FileHash -LiteralPath $taskOperation.Target).Hash) { throw "Installed hash mismatch: $($taskOperation.Id)" }
    }
    # The shared runtime performs the one-time, schema-aware migration on first
    # startup (including Steam updates without this installer). Keep an extra backup.
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
    throw "Install failed and DLL changes were rolled back: $taskFailure. Backups: $taskBackup"
}
Write-Host "[OK] Coordinated runtime release installed: $($taskOperations.Id -join ', ')"
Write-Host "[OK] Old DLLs recoverable from: $taskBackup"
Write-Host '[OK] Saves, inventory, replays and per-mod settings were preserved. UI settings migrate once on first startup.'
