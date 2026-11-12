$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../release-candidate.ps1')
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture = Join-Path $repo ('build/_work/candidate-tests/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $fixture | Out-Null
function Reject-Candidate([scriptblock]$Action) { $failed=$false; try { & $Action | Out-Null } catch { $failed=$true }; if (-not $failed) { throw 'Invalid candidate was accepted' } }
try {
    $scope = @{ Components=@('jk-runtime','mega-mapping-expansion'); ReleaseComponents=@('mega-mapping-expansion') }
    Assert-ModReleaseComponent $scope 'mega-mapping-expansion'
    Reject-Candidate { Assert-ModReleaseComponent $scope 'jk-runtime' }
    Reject-Candidate { Assert-ModReleaseComponent $scope 'missing' }
    $scope.ReleaseComponents=@('jk-runtime','mega-mapping-expansion')
    Assert-ModReleaseComponent $scope 'jk-runtime'
    $payload = Join-Path $fixture 'payload.dll'; [IO.File]::WriteAllText($payload,'verified')
    $startupCache = Join-Path $fixture 'JKRuntime.StartupCache'
    New-Item -ItemType Directory -Path $startupCache | Out-Null
    Reject-Candidate { Get-CandidateFiles $fixture }
    [IO.Directory]::Delete($startupCache)
    $record = @{ Schema=1; ReleaseReady=$true; Checks=@{ Success=$true; Release=$true; Stages=@(@{ Status='Pass' }) }; Files=@(Get-CandidateFiles $fixture) }
    $path = Join-Path $fixture 'candidate.json'
    [IO.File]::WriteAllText($path,($record | ConvertTo-Json -Depth 8))
    Test-ModCandidate $fixture | Out-Null
    [IO.File]::WriteAllText($payload,'changed')
    Reject-Candidate { Test-ModCandidate $fixture }
    [IO.File]::WriteAllText($payload,'verified')
    [IO.File]::WriteAllText((Join-Path $fixture 'extra.dll'),'unexpected')
    Reject-Candidate { Test-ModCandidate $fixture }
    [IO.File]::Delete((Join-Path $fixture 'extra.dll'))
    $record.Checks.Stages[0].Status='Skipped'
    [IO.File]::WriteAllText($path,($record | ConvertTo-Json -Depth 8))
    Reject-Candidate { Test-ModCandidate $fixture }
    $record.Checks.Stages[0].Status='Pass'; $record.Checks.Release=$false
    [IO.File]::WriteAllText($path,($record | ConvertTo-Json -Depth 8))
    Reject-Candidate { Test-ModCandidate $fixture }
    Reject-Candidate { New-ModCandidate -RepoRoot $repo -CheckReceipt $path -Components jk-runtime }
    $plannerRoot = Join-Path $fixture 'scripts'; New-Item -ItemType Directory -Path $plannerRoot | Out-Null
    [IO.File]::WriteAllText((Join-Path $plannerRoot 'check-mods.ps1'),'param($Mod,$Release,$List,$GameDir) [pscustomobject]@{ Mod="probe"; Stage="probe/contracts"; Tier="Integration"; Arguments=@{ GameDir=$GameDir; Optional=$false } }')
    $providerRoot = Join-Path $fixture 'providers'; New-Item -ItemType Directory -Path $providerRoot | Out-Null
    $checks = @{ Schema=1; Success=$true; Release=$true; Tier='Integration'; ProviderRoots=@($providerRoot); Providers=@(); Stages=@(@{ Stage='probe/contracts'; Tier='Integration'; Status='Pass'; Inputs=@($payload); InputKey=(Get-ModContentKey @($payload)); Arguments=@{ GameDir='fixture'; Optional=$true } }) }
    function Reject-Reason([string]$Pattern) {
        [IO.File]::WriteAllText($path,($checks | ConvertTo-Json -Depth 10))
        $reason=''; try { New-ModCandidate -RepoRoot $fixture -CheckReceipt $path -Components probe | Out-Null } catch { $reason=$_.Exception.Message }
        if ($reason -notlike $Pattern) { throw "Wrong candidate refusal: $reason" }
    }
    Reject-Reason 'Required stage arguments differ:*'
    $checks.Stages[0].Tier='Fast'
    Reject-Reason 'Required stage tier differs:*'
    $checks.Stages[0].Tier='Integration'
    $checks.Stages[0].Arguments.Optional=$false; $checks.Stages[0].Stage='probe/other'
    Reject-Reason 'Missing required stage:*'
    $checks.Stages[0].Stage='probe/contracts'; [IO.File]::WriteAllText($payload,'stale')
    Reject-Reason 'Stale check inputs:*'
    [IO.File]::WriteAllText((Join-Path $providerRoot 'new.dll'),'new provider')
    Reject-Reason 'Installed provider set changed*'

} finally {
    $parent = [IO.Path]::GetFullPath((Join-Path $repo 'build/_work/candidate-tests')) + [IO.Path]::DirectorySeparatorChar
    if (-not $fixture.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe candidate fixture cleanup' }
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
Write-Host '[OK] Candidate bytes, exact file set, required evidence and unresolved skip refusal'
