$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../build-support/build-retention.ps1')
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$fixture = Join-Path $repo ('build/_work/retention-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
function Assert-Retention([bool]$Value, [string]$Message) { if (-not $Value) { throw $Message } }
try {
    foreach ($i in 1..6) {
        $path = Join-Path $fixture "run-$i"
        New-Item -ItemType Directory -Path $path | Out-Null
        [IO.File]::WriteAllText((Join-Path $path 'marker'), "run $i")
        (Get-Item -LiteralPath $path).LastWriteTimeUtc = [datetime]::UtcNow.AddDays(-7).AddMinutes($i)
    }
    New-Item -ItemType Directory -Path "$fixture/backup", "$fixture/run-7" | Out-Null
    $preview = @(Remove-BuildArtifactHistory -Directory $fixture -NamePattern '^run-\d+$' -Keep 3 -Preview)
    Assert-Retention ($preview.Count -eq 4 -and (Test-Path -LiteralPath "$fixture/run-1")) 'Preview must not delete files'
    Remove-BuildArtifactHistory -Directory $fixture -NamePattern '^run-\d+$' -Keep 3 -Preserve "$fixture/run-1" | Out-Null
    Assert-Retention ((Test-Path "$fixture/run-1") -and -not (Test-Path "$fixture/run-2") -and (Test-Path "$fixture/run-5") -and (Test-Path "$fixture/backup")) 'Keep newest, preserve current, ignore unrelated names'
    Remove-BuildArtifactHistory -Directory $fixture -NamePattern '^run-\d+$' -Keep 0 | Out-Null
    Assert-Retention (Test-Path "$fixture/run-7") 'Recent work must survive the grace period'
    $rejected = $false
    try { Remove-BuildArtifactHistory -Directory (Join-Path $repo 'mods') -NamePattern '^.+$' -Keep 0 -Preview }
    catch { $rejected = $true }
    Assert-Retention $rejected 'Source directories must be rejected'
    [IO.File]::WriteAllText("$fixture/backup/marker", 'keep outside linked subtree')
    New-Item -ItemType Junction -Path "$fixture/run-link" -Target "$fixture/backup" | Out-Null
    $rejected = $false
    try { Remove-BuildArtifactHistory -Directory $fixture -NamePattern '^run-link$' -Keep 0 -MinimumAgeMinutes 0 }
    catch { $rejected = $true }
    Assert-Retention ($rejected -and (Test-Path "$fixture/backup/marker")) 'Linked trees must never be followed or deleted'
    [IO.Directory]::Delete("$fixture/run-link")
    foreach ($i in 1..5) {
        $path = Join-Path $fixture "finished-$i"
        New-Item -ItemType Directory -Path $path | Out-Null
        [IO.File]::WriteAllText((Join-Path $path '.build-complete'), '')
        (Get-Item -LiteralPath $path).LastWriteTimeUtc = [datetime]::UtcNow.AddSeconds(-10 + $i)
    }
    $busy = [pscustomobject]@{ ExecutablePath=(Join-Path $fixture 'finished-1/tool.exe'); CommandLine='' }
    Remove-BuildArtifactHistory -Directory $fixture -NamePattern '^finished-\d+$' -Keep 1 -CompletionMarker '.build-complete' -Preserve "$fixture/finished-2/child" -RunningProcesses @($busy) | Out-Null
    Assert-Retention ((Test-Path "$fixture/finished-1") -and (Test-Path "$fixture/finished-2") -and -not (Test-Path "$fixture/finished-3") -and -not (Test-Path "$fixture/finished-4") -and (Test-Path "$fixture/finished-5")) 'Completed rapid builds must be bounded without deleting running or explicitly preserved parents'
    [IO.File]::WriteAllText("$fixture/busy.exe", 'running fixture')
    (Get-Item "$fixture/busy.exe").LastWriteTimeUtc = [datetime]::UtcNow.AddDays(-7)
    $busy = [pscustomobject]@{ ExecutablePath="$fixture\busy.exe"; CommandLine='' }
    Remove-BuildArtifactHistory -Directory $fixture -NamePattern '^busy\.exe$' -Files -Keep 0 -RunningProcesses @($busy) | Out-Null
    Assert-Retention (Test-Path "$fixture/busy.exe") 'Running executable files must survive file retention'

    $sandbox = Join-Path $fixture 'workspace-build'
    $families = @(
        @('jk-runtime/_INTERNAL/validation',''),
        @('mega-mapping-expansion/_INTERNAL','behavior-graphics-'),
        @('_work/mme-construction/tests',''),
        @('_work/worldsmith-extension','native-tests-'),
        @('_work/multiplayer-expansion/contracts',''),
        @('_work/exports','export-')
    )
    foreach ($family in $families) {
        foreach ($i in 1..5) {
            $path = Join-Path (Join-Path $sandbox $family[0]) ($family[1] + [Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $path -Force | Out-Null
            [IO.File]::WriteAllText((Join-Path $path 'asset.xnb'), "fixture $i")
            (Get-Item -LiteralPath $path).LastWriteTimeUtc = [datetime]::UtcNow.AddDays(-7).AddMinutes($i)
        }
    }
    $valuable = @('_work/backups/only-copy', '_work/worldsmith-extension/manual-fixtures/source', 'mega-mapping-expansion/UPLOAD_TO_WORKSHOP/MegaMappingExpansion.dll')
    foreach ($relative in $valuable) {
        $path = Join-Path $sandbox $relative
        New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
        [IO.File]::WriteAllText($path, 'keep user data and release packages')
    }
    $preview = @(Remove-WorkspaceBuildHistory -BuildRoot $sandbox -Preview)
    Assert-Retention ($preview.Count -eq 12) 'Preview must include every generated family without touching packages or manual data'
    Remove-WorkspaceBuildHistory -BuildRoot $sandbox | Out-Null
    foreach ($family in $families) {
        $left = @(Get-ChildItem -LiteralPath (Join-Path $sandbox $family[0]) -Directory | Where-Object { $_.Name -like ($family[1] + '*') })
        if ($family[1] -eq 'native-tests-') { $left = @($left | Where-Object { $_.Name -like 'native-tests-*' }) }
        Assert-Retention ($left.Count -eq 3) "Generated family didn't retain three runs: $($family[0])"
    }
    foreach ($relative in $valuable) { Assert-Retention (Test-Path -LiteralPath (Join-Path $sandbox $relative)) "User data was removed: $relative" }
    Write-Host '[OK] Build retention: completed rapid runs, active processes, nested preservation, component coverage, preview, age, source boundary and junction safety'
} finally {
    if (Test-Path -LiteralPath "$fixture/run-link") { [IO.Directory]::Delete("$fixture/run-link") }
    Remove-BuildArtifactHistory -Directory (Split-Path $fixture) -NamePattern ('^' + [regex]::Escape((Split-Path $fixture -Leaf)) + '$') -Keep 0 -MinimumAgeMinutes 0 | Out-Null
}
