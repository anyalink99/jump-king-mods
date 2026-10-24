function Remove-BuildArtifactHistory {
    param(
        [Parameter(Mandatory=$true)][string]$Directory,
        [Parameter(Mandatory=$true)][string]$NamePattern,
        [ValidateRange(0,100)][int]$Keep = 3,
        [ValidateRange(0,10080)][int]$MinimumAgeMinutes = 60,
        [string[]]$Preserve = @(),
        [string]$CompletionMarker,
        [object[]]$RunningProcesses,
        [switch]$Files,
        [switch]$Preview
    )
    $boundary = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../build')).TrimEnd('\','/')
    $root = [IO.Path]::GetFullPath($Directory).TrimEnd('\','/')
    if (-not $root.StartsWith($boundary + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Retention must stay below the workspace build directory: $root"
    }
    if (-not (Test-Path -LiteralPath $root)) { return }
    # check ancestors too, a junction above the target still escapes the workspace
    for ($ancestor = Get-Item -LiteralPath $root; $null -ne $ancestor; $ancestor = $ancestor.Parent) {
        if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked retention path: $($ancestor.FullName)" }
    }
    $protected = @($Preserve | ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\','/') })
    $candidates = @(Get-ChildItem -LiteralPath $root -Force | Where-Object {
        $_.Name -cmatch $NamePattern -and ($_.PSIsContainer -ne [bool]$Files)
    } | Sort-Object LastWriteTimeUtc, Name -Descending)
    $cutoff = [datetime]::UtcNow.AddMinutes(-$MinimumAgeMinutes)
    if ($CompletionMarker -and [IO.Path]::GetFileName($CompletionMarker) -ne $CompletionMarker) { throw 'Completion marker must be a file name.' }
    $running = if ($PSBoundParameters.ContainsKey('RunningProcesses')) { @($RunningProcesses) } else { @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object { $_.ProcessId -ne $PID }) }
    $running = @($running | Where-Object {
        ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) -or
        ($_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($root, [StringComparison]::OrdinalIgnoreCase) -ge 0)
    })
    foreach ($item in @($candidates | Select-Object -Skip $Keep)) {
        $resolved = [IO.Path]::GetFullPath($item.FullName)
        if (@($protected | Where-Object { $_ -eq $resolved -or $_.StartsWith($resolved + '\', [StringComparison]::OrdinalIgnoreCase) }).Count) { continue }
        # finished fixtures don't need an hour of duplicate game assets
        $complete = $CompletionMarker -and $item.PSIsContainer -and (Test-Path -LiteralPath (Join-Path $resolved $CompletionMarker) -PathType Leaf)
        if (-not $complete -and $item.LastWriteTimeUtc -gt $cutoff) { continue }
        if ([IO.Path]::GetDirectoryName($resolved) -ne $root) { throw "Unexpected retention target: $resolved" }
        if (@($running | Where-Object {
            ($_.ExecutablePath -and ($_.ExecutablePath -eq $resolved -or $_.ExecutablePath.StartsWith($resolved + '\', [StringComparison]::OrdinalIgnoreCase))) -or
            ($_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($resolved, [StringComparison]::OrdinalIgnoreCase) -ge 0)
        }).Count) { continue }
        $pending = New-Object 'Collections.Generic.Stack[IO.FileSystemInfo]'
        $pending.Push($item)
        $bytes = 0L
        while ($pending.Count) {
            $entry = $pending.Pop()
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked retention contents: $($entry.FullName)" }
            if ($entry -is [IO.DirectoryInfo]) {
                foreach ($child in $entry.GetFileSystemInfos()) { $pending.Push($child) }
            } else { $bytes += $entry.Length }
        }
        if (-not $Preview) {
            # generated fixtures can contain hidden temp files and read-only test inputs
            Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction Stop
        }
        [pscustomobject]@{ Path=$resolved; Bytes=$bytes; Removed=(-not $Preview) }
    }
}

function Remove-WorkspaceBuildHistory {
    param(
        [ValidateSet('All','Runtime','Mapping','Worldsmith','Wardrobe','Multiplayer','StereoMadness','TotalEclipse','Prism','ScreenSolver','Exports')][string[]]$Component = @('All'),
        [string]$BuildRoot = (Join-Path $PSScriptRoot '../../../build'),
        [ValidateRange(0,10080)][int]$MinimumAgeMinutes = 60,
        [string[]]$Preserve = @(),
        [switch]$Preview
    )
    $guid = '[0-9a-f]{32}'
    $rules = @(
        @{ Component='Runtime'; Path='jk-runtime/_INTERNAL/validation'; Pattern="^$guid$"; Complete=$true },
        @{ Component='Runtime'; Path='jk-runtime/_INTERNAL'; Pattern="^example-$guid$" },
        @{ Component='Runtime'; Path='jk-runtime/_INTERNAL'; Pattern="^installer-test-$guid$" },
        @{ Component='Runtime'; Path='jk-runtime/_INTERNAL'; Pattern="^foreign-attribution-$guid$" },
        @{ Component='Mapping'; Path='mega-mapping-expansion/_INTERNAL'; Pattern="^package-$guid$" },
        @{ Component='Mapping'; Path='mega-mapping-expansion/_INTERNAL'; Pattern="^kit-$guid$" },
        @{ Component='Mapping'; Path='_work/mme-construction/tests'; Pattern="^$guid$"; Complete=$true },
        @{ Component='Mapping'; Path='_work/mme-construction/docs'; Pattern="^$guid$" },
        @{ Component='Worldsmith'; Path='_work/worldsmith-extension'; Pattern="^native-tests-$guid$"; Complete=$true },
        @{ Component='Worldsmith'; Path='_work/worldsmith-extension'; Pattern="^package-$guid$" },
        @{ Component='Worldsmith'; Path='_work/worldsmith-extension'; Pattern='^release-\d{8}-\d{6}$' },
        @{ Component='Wardrobe'; Path='wardrobe-plus/_INTERNAL/tests'; Pattern="^$guid$" },
        @{ Component='Multiplayer'; Path='_work/multiplayer-expansion/contracts'; Pattern="^$guid$" },
        @{ Component='Multiplayer'; Path='_work/multiplayer-expansion'; Pattern="^discovery-$guid$" },
        @{ Component='Exports'; Path='_work/exports'; Pattern='^.+$' }
    )
    # these are generated test families, not playtest or install backups
    foreach ($family in @('behavior-graphics','api-package-check','doc-checks','scene-save-test','structured-authoring','shape','migration-rollback','ending-coexistence','compiled-pixel-native','pixel-mbs')) {
        $rules += @{ Component='Mapping'; Path='mega-mapping-expansion/_INTERNAL'; Pattern="^$family-$guid$" }
    }
    try { $running = @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object { $_.ProcessId -ne $PID }) }
    catch { Write-Warning "Build history cleanup skipped: cannot inspect active processes: $_"; return }
    foreach ($rule in $rules) {
        if ($Component -notcontains 'All' -and $Component -notcontains $rule.Component) { continue }
        $arguments = @{
            Directory=(Join-Path $BuildRoot $rule.Path); NamePattern=$rule.Pattern
            MinimumAgeMinutes=$MinimumAgeMinutes; Preserve=$Preserve; Preview=$Preview
            RunningProcesses=$running
        }
        if ($rule.ContainsKey('Keep')) { $arguments.Keep = $rule.Keep }
        if ($rule.Complete) { $arguments.CompletionMarker = '.build-complete' }
        try { Remove-BuildArtifactHistory @arguments }
        catch { Write-Warning "Build history cleanup skipped for $($rule.Path) ($($rule.Pattern)): $_" }
    }
    $maintenance = @($Component | Where-Object { $_ -ne 'Exports' })
    if ($maintenance.Count) {
        Invoke-WorkspaceMaintenance -Component $maintenance -BuildRoot $BuildRoot -Preserve $Preserve -Preview:$Preview | Out-Null
    }
}

function Assert-BuildBackupPath([string]$Directory) {
    $boundary = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../build')).TrimEnd('\','/')
    $path = [IO.Path]::GetFullPath($Directory).TrimEnd('\','/')
    if (-not $path.StartsWith($boundary + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Backup path escaped build: $path" }
    for ($cursor = $path; $cursor; $cursor = Split-Path -Parent $cursor) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Linked backup path: $cursor"
        }
    }
    return $path
}

function Start-BuildBackup([string]$Directory) {
    $path = Assert-BuildBackupPath $Directory
    [IO.Directory]::CreateDirectory($path) | Out-Null
    [IO.File]::WriteAllText((Join-Path $path '.backup-incomplete'), [datetime]::UtcNow.ToString('o'))
}

function Complete-BuildBackup([string]$Directory) {
    $path = Assert-BuildBackupPath $Directory
    [IO.File]::WriteAllText((Join-Path $path '.backup-complete'), [datetime]::UtcNow.ToString('o'))
    [IO.File]::Delete((Join-Path $path '.backup-incomplete'))
}

function Invoke-WorkspaceMaintenance {
    param(
        [ValidateSet('All','Runtime','Mapping','Worldsmith','Wardrobe','Multiplayer','StereoMadness','TotalEclipse','Prism','ScreenSolver')][string[]]$Component = @('All'),
        [string]$BuildRoot = (Join-Path $PSScriptRoot '../../../build'),
        [string]$BackupDirectory,
        [string[]]$Preserve = @(),
        [double]$MinimumAgeHours = 1,
        [switch]$Logs,
        [switch]$Preview
    )
    try {
        $processes = @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object { $_.ProcessId -ne $PID })
        $state = @{ processes=@($processes | ForEach-Object { $_.ExecutablePath; $_.CommandLine } | Where-Object { $_ }) } | ConvertTo-Json -Compress
        foreach ($name in $Component) {
            $arguments = @((Join-Path $PSScriptRoot 'history-maintenance.py'), '--build-root', [IO.Path]::GetFullPath($BuildRoot), '--component', $name, '--minimum-age-hours', $MinimumAgeHours.ToString([Globalization.CultureInfo]::InvariantCulture))
            if (-not $Preview) { $arguments += '--apply' }
            if ($Logs) { $arguments += '--logs-only' }
            if ($BackupDirectory) { $arguments += @('--backup-directory', [IO.Path]::GetFullPath($BackupDirectory)) }
            foreach ($path in $Preserve) { $arguments += @('--preserve', $path) }
            $output = $state | & python @arguments
            if ($LASTEXITCODE -ne 0) { throw "History maintenance failed for $name" }
            $result = $output | ConvertFrom-Json
            if ($result.retired.Count -or $result.compressed_logs) {
                Write-Host ("[HISTORY] {0}: {1} old backups, {2} compressed logs{3}" -f $name, $result.retired.Count, $result.compressed_logs, $(if ($Preview) { ' (preview)' } else { '' }))
            }
            if ($result.skipped.Count) { Write-Warning "History maintenance skipped $($result.skipped.Count) active, linked or changed paths." }
            $result
        }
    } catch { Write-Warning "Backup and log cleanup skipped: $_" }
}
