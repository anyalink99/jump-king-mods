. (Join-Path $PSScriptRoot 'mod-build-cache.ps1')

function Get-CandidateFiles([string]$Root) {
    if (@(Get-ChildItem -LiteralPath $Root -Directory -Recurse -Force | Where-Object Name -like '*.StartupCache').Count) {
        throw 'Runtime startup caches are test or installed state, not release content'
    }
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    @(Get-ChildItem -LiteralPath $Root -File -Recurse | Where-Object { $_.FullName -ne (Join-Path ([IO.Path]::GetFullPath($Root)) 'candidate.json') } | Sort-Object FullName | ForEach-Object {
        if ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Candidate files cannot be links' }
        @{ Path=$_.FullName.Substring($prefix.Length).Replace('\','/'); SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash }
    })
}

function Test-ModCandidate([string]$Directory) {
    $record = Get-Content -LiteralPath (Join-Path $Directory 'candidate.json') -Raw | ConvertFrom-Json
    if ($record.Schema -ne 1 -or -not $record.ReleaseReady -or $record.Checks.Success -ne $true -or $record.Checks.Release -ne $true) { throw 'Candidate has no successful required release evidence' }
    if (@($record.Checks.Stages | Where-Object Status -ne 'Pass').Count) { throw 'Candidate contains unresolved checks' }
    if (@(Get-ChildItem -LiteralPath $Directory -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Candidate cannot contain linked directories or files' }
    $actual = @(Get-CandidateFiles $Directory)
    if ($actual.Count -ne $record.Files.Count -or $actual.Count -eq 0) { throw 'Candidate payload file set changed' }
    foreach ($file in $actual) {
        $expected = @($record.Files | Where-Object Path -ceq $file.Path)
        if ($expected.Count -ne 1 -or $expected[0].SHA256 -ne $file.SHA256) { throw "Candidate bytes changed: $($file.Path)" }
    }
    $record
}

function Assert-ModReleaseComponent($Candidate, [string]$Component) {
    if ($Candidate.Components -notcontains $Component) { throw 'Component is absent from the verified candidate' }
    if ($Candidate.ReleaseComponents -and $Candidate.ReleaseComponents -notcontains $Component) {
        throw 'Dependency was built but not selected for release. Create a candidate with that component explicitly selected.'
    }
}

function New-ModCandidate {
    param([string]$RepoRoot, [string]$CheckReceipt, [string[]]$Components)
    $record = Get-Content -LiteralPath $CheckReceipt -Raw | ConvertFrom-Json
    if ($record.Schema -ne 1 -or -not $record.Success -or -not $record.Release -or $record.Tier -notin @('Integration','Full')) { throw 'Run required Integration or Full release checks before creating a candidate' }
    if (@($record.Stages | Where-Object Status -ne 'Pass').Count) { throw 'Skipped, failed or changed-input stages cannot authorize a release' }
    if (-not $record.ProviderRoots) { throw 'Release receipt has no provider discovery evidence' }
    $providers = @($record.ProviderRoots | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-ChildItem -LiteralPath $_ -File -Recurse -Filter '*.dll' } | Sort-Object FullName -Unique)
    if ($providers.Count -ne $record.Providers.Count) { throw 'Installed provider set changed after verification' }
    foreach ($provider in $providers) {
        $observed = @($record.Providers | Where-Object Path -eq $provider.FullName)
        if ($observed.Count -ne 1 -or $observed[0].SHA256 -ne (Get-FileHash -LiteralPath $provider.FullName).Hash) { throw "Installed provider changed: $($provider.Name)" }
    }
    foreach ($stage in $record.Stages) {
        if ((Get-ModContentKey @($stage.Inputs)) -ne $stage.InputKey) { throw "Stale check inputs: $($stage.Stage)" }
    }
    $selected = @($Components | ForEach-Object { if ($_ -eq 'hammer-king') { 'more-items' } else { $_.ToLowerInvariant() } } | Sort-Object -Unique)
    if ($selected.Count -eq 0) { throw 'Select candidate components explicitly' }
    $game = $record.Stages[0].Arguments.GameDir
    $planArgs = @{ Mod=$selected; Release=$true; List=$true; GameDir=$game }
    if ($record.Tier -eq 'Full') { $planArgs.Full=$true }
    $required = @(& (Join-Path $RepoRoot 'scripts/check-mods.ps1') @planArgs)
    foreach ($stage in $required) {
        if (@($record.Stages | Where-Object { $_.Stage -eq $stage.Stage -and $_.Status -eq 'Pass' }).Count -ne 1) { throw "Missing required stage: $($stage.Stage)" }
    }
    foreach ($stage in $required) {
        $passed = @($record.Stages | Where-Object Stage -eq $stage.Stage)[0]
        if ($passed.Tier -ne $stage.Tier) { throw "Required stage tier differs: $($stage.Stage)" }
        foreach ($name in $stage.Arguments.Keys) {
            if ($passed.Arguments.$name -ne $stage.Arguments[$name]) { throw "Required stage arguments differ: $($stage.Stage) / $name" }
        }
    }
    $releaseComponents = $selected
    $selected = @($required.Mod | Sort-Object -Unique)
    foreach ($id in $selected) {
        $component = @($record.Components | Where-Object Id -eq $id)
        if ($component.Count -ne 1 -or (Get-ModContentKey @($component[0].Artifacts)) -ne $component[0].ArtifactKey) { throw "Verified artifacts changed: $id" }
    }
    $directory = Join-Path $RepoRoot ('build/_work/release-candidates/' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $directory | Out-Null
    foreach ($id in $selected) {
        $artifact = @($record.Components | Where-Object Id -eq $id)[0]
        $package = @($artifact.Artifacts | Where-Object { (Split-Path $_ -Leaf) -eq 'UPLOAD_TO_WORKSHOP' })
        if ($package.Count -ne 1) { throw "No distributable package for $id; use its dedicated release workflow" }
        Copy-Item -LiteralPath $package[0] -Destination (Join-Path $directory $id) -Recurse
        if ((Get-ModContentKey @($artifact.Artifacts)) -ne $artifact.ArtifactKey) { throw "Package changed while staging: $id" }
    }
    $sourceHead = @(& git -C $RepoRoot rev-parse HEAD)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify candidate source revision' }
    $sourceDirty = @(& git -C $RepoRoot status --porcelain).Count -gt 0
    $facts = @(foreach ($id in $selected) { Get-ModPackageFacts (Join-Path $directory $id) $id @($record.Providers) })
    $runtimeFact = @($facts | Where-Object Id -eq 'jk-runtime')
    foreach ($fact in $facts | Where-Object Id -ne 'jk-runtime') {
        if ($runtimeFact.Count -ne 1 -or $fact.ApiMajor -ne $runtimeFact[0].ApiMajor -or $fact.ApiMinor -gt $runtimeFact[0].ApiMinor) { throw "Candidate Runtime API is too old for $($fact.Id)" }
    }
    $candidate = @{ Schema=1; SourceRevision=$sourceHead[0]; SourceDirty=$sourceDirty; Packages=$facts; Actor='Niotid'; CreatedUtc=[DateTime]::UtcNow.ToString('o'); ReleaseReady=$true; ReleaseComponents=$releaseComponents; Components=$selected; Checks=$record; Files=@(Get-CandidateFiles $directory) }
    [IO.File]::WriteAllText((Join-Path $directory 'candidate.json'),($candidate | ConvertTo-Json -Depth 16),[Text.UTF8Encoding]::new($false))
    Test-ModCandidate $directory | Out-Null
    # incomplete candidates stay for diagnosis; complete payload histories are bounded
    [IO.File]::WriteAllText((Join-Path $directory '.build-complete'),'')
    $candidate.Files = @(Get-CandidateFiles $directory)
    [IO.File]::WriteAllText((Join-Path $directory 'candidate.json'),($candidate | ConvertTo-Json -Depth 16),[Text.UTF8Encoding]::new($false))
    . (Join-Path $RepoRoot 'mods/jk-runtime/build-support/build-retention.ps1')
    Remove-BuildArtifactHistory -Directory (Split-Path $directory) -NamePattern '^[0-9a-f]{32}$' -Keep 2 -MinimumAgeMinutes 60 -CompletionMarker '.build-complete' -Preserve @($directory) | Out-Null
    $directory
}

function Get-ModPackageFacts([string]$Directory,[string]$Id,[object[]]$Providers=@()) {
    $facts = @()
    foreach ($file in Get-ChildItem -LiteralPath $Directory -Filter '*.dll' -File) {
        $assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($file.FullName))
        if ($Id -eq 'jk-runtime' -and $file.Name -eq 'JKRuntime.dll') {
            $type = $assembly.GetType('JKRuntime.RuntimeApi',$true)
            $facts += @{ Id=$Id; Version=([Version]$file.VersionInfo.FileVersion).ToString(3); ApiMajor=$type.GetField('ApiMajor').GetRawConstantValue(); ApiMinor=$type.GetField('ApiMinor').GetRawConstantValue(); Dependencies=@() }
            continue
        }
        $stream = $assembly.GetManifestResourceStream('JKRuntime.Manifest')
        if (-not $stream) { continue }
        $reader = [IO.StreamReader]::new($stream)
        try { $manifest = ([xml]$reader.ReadToEnd()).module } finally { $reader.Dispose() }
        $dependencies = @(foreach ($reference in $manifest.assemblyReference) {
            $path = Join-Path $Directory $reference.file
            $source = 'package'
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                if ((Get-FileHash -LiteralPath $path).Hash -ne $reference.sha256) { throw "Candidate dependency differs from its manifest: $Id / $($reference.file)" }
            } else {
                $external = @($Providers | Where-Object { (Split-Path $_.Path -Leaf) -eq $reference.file -and $_.SHA256 -eq $reference.sha256 })
                if ($external.Count -ne 1) { throw "Required external dependency has no unique verified provider: $Id / $($reference.file)" }
                $source = 'installed-provider'
            }
            @{ Source=$source; File=[string]$reference.file; Identity=[string]$reference.fullName; Kind=[string]$reference.kind; SHA256=[string]$reference.sha256 }
        })
        $facts += @{ Id=$Id; Module=[string]$manifest.id; Version=([Version]$manifest.version).ToString(3); ApiMajor=[int]$manifest.apiMajor; ApiMinor=[int]$manifest.apiMinor; Dependencies=$dependencies }
    }
    if ($facts.Count -ne 1) { throw "Expected one distributable entry point: $Id" }
    $facts[0]
}
