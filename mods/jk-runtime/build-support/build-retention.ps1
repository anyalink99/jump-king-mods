function Remove-BuildArtifactHistory {
    param(
        [Parameter(Mandatory=$true)][string]$Directory,
        [Parameter(Mandatory=$true)][string]$NamePattern,
        [ValidateRange(0,100)][int]$Keep = 3,
        [ValidateRange(0,10080)][int]$MinimumAgeMinutes = 60,
        [string[]]$Preserve = @(),
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
    $running = @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object { $_.ProcessId -ne $PID })
    foreach ($item in @($candidates | Select-Object -Skip $Keep)) {
        if ($protected -contains $item.FullName -or $item.LastWriteTimeUtc -gt $cutoff) { continue }
        $resolved = [IO.Path]::GetFullPath($item.FullName)
        if ([IO.Path]::GetDirectoryName($resolved) -ne $root) { throw "Unexpected retention target: $resolved" }
        if (@($running | Where-Object {
            ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($resolved + '\', [StringComparison]::OrdinalIgnoreCase)) -or
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
