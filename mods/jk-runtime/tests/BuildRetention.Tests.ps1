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
    Remove-Item -LiteralPath "$fixture/run-link"
    Write-Host '[OK] Build retention: preview, age, count, explicit preservation, source boundary and junction safety'
} finally {
    if (Test-Path -LiteralPath "$fixture/run-link") { Remove-Item -LiteralPath "$fixture/run-link" }
    Remove-BuildArtifactHistory -Directory (Split-Path $fixture) -NamePattern ('^' + [regex]::Escape((Split-Path $fixture -Leaf)) + '$') -Keep 0 -MinimumAgeMinutes 0 | Out-Null
}
