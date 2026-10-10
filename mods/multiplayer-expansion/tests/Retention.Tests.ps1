$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../stage-lab.ps1')
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$fixture = Join-Path $repo ('build/_work/multiplayer-expansion/tests/retention-' + [Guid]::NewGuid().ToString('N'))
$root = Join-Path $fixture 'sessions'
$paths = @()
try {
    foreach ($i in 1..5) {
        $path = Join-Path $root ('20260101-00000' + $i + '-abcdef12')
        $paths += $path
        New-Item -ItemType Directory -Path "$path/client2/Content/Saves", "$path/client2/backups" | Out-Null
        [IO.File]::WriteAllText("$path/mpex-session.txt", 'fixture')
        [IO.File]::WriteAllText("$path/client2/resource.xnb", 'rebuildable')
        [IO.File]::WriteAllText("$path/client2/Content/Saves/progress.sav", 'keep save')
        [IO.File]::WriteAllText("$path/client2/backups/old.dll", 'keep backup')
        [IO.File]::WriteAllText("$path/client2/settings.json", 'keep settings')
        [IO.File]::WriteAllText("$path/client2.performance.txt", 'keep log')
        (Get-Item -LiteralPath $path).CreationTimeUtc = [datetime]::UtcNow.AddDays(-4).AddMinutes($i)
    }
    Remove-OldLabAssets -Root $root -Current $paths[4]
    if ((Test-Path "$($paths[0])/client2/resource.xnb") -or -not (Test-Path "$($paths[3])/client2/resource.xnb")) { throw 'Keep only two full old client copies' }
    foreach ($path in $paths) {
        if (-not (Test-Path "$path/client2/Content/Saves/progress.sav") -or -not (Test-Path "$path/client2/settings.json") -or
            -not (Test-Path "$path/client2/backups/old.dll") -or -not (Test-Path "$path/client2.performance.txt")) { throw 'State or diagnostics were removed' }
    }
    [IO.File]::WriteAllText("$($paths[0])/client2/resource.xnb", 'current')
    Remove-OldLabAssets -Root $root -Current $paths[0]
    if (-not (Test-Path "$($paths[0])/client2/resource.xnb")) { throw 'Explicit current session must survive' }
    (Get-Item -LiteralPath $paths[0]).CreationTimeUtc = [datetime]::UtcNow
    Remove-OldLabAssets -Root $root -Current $paths[4]
    if (-not (Test-Path "$($paths[0])/client2/resource.xnb")) { throw 'Recent session must survive' }
    foreach ($index in 0..2) {
        (Get-Item -LiteralPath $paths[$index]).CreationTimeUtc = [datetime]::UtcNow.AddMinutes(-10+$index)
        [IO.File]::WriteAllText("$($paths[$index])/inputs.json", '{}')
    }
    Remove-OldLabAssets -Root $root -Current $paths[4]
    if (Test-Path "$($paths[0])/client2/resource.xnb") { throw 'Completed rapid restarts bypassed the two-copy limit' }
    (Get-Item -LiteralPath $paths[1]).CreationTimeUtc = [datetime]::UtcNow.AddDays(-4)
    New-Item -ItemType Junction -Path "$($paths[1])/client2/link" -Target "$($paths[4])/client2" | Out-Null
    $rejected = $false
    try { Remove-OldLabAssets -Root $root -Current $paths[4] } catch { $rejected = $true }
    if (-not $rejected -or -not (Test-Path "$($paths[4])/client2/resource.xnb")) { throw 'Linked client trees must be rejected' }
    Remove-Item -LiteralPath "$($paths[1])/client2/link"
    Write-Host '[OK] Lab history: two copies, recent/current protection, saves/settings/logs/backups and junction refusal'
} finally {
    if ($paths.Count -gt 1 -and (Test-Path -LiteralPath "$($paths[1])/client2/link")) { Remove-Item -LiteralPath "$($paths[1])/client2/link" }
    $safeRoot = [IO.Path]::GetFullPath((Join-Path $repo 'build/_work/multiplayer-expansion/tests')).TrimEnd('\','/') + '\'
    if (-not [IO.Path]::GetFullPath($fixture).StartsWith($safeRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture cleanup' }
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
