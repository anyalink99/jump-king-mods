param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [ValidateSet('jk-runtime','free-scaling','casual-jumping','subframe-charge','more-items','morph-ball','replays','run-verifier','wardrobe-plus','mega-mapping-expansion','no-walk-off','powers','screen-solver','hammer-king','smooth-camera','overlay-plus','stereo-madness','total-eclipse','worldsmith-extension','prism','multiplayer-expansion','metroidvania','god','red-crystal','palace-clock')]
    [string[]]$Mod,
    [string]$WorldsmithDir,
    [switch]$Integration,
    [switch]$Full,
    [switch]$List,
    [string[]]$ExcludeMod,
    [string]$ChangedSince,
    [switch]$NoCache,
    [switch]$Release,
    [switch]$Tooling
)

$ErrorActionPreference = 'Stop'
$Total = [Diagnostics.Stopwatch]::StartNew()
$RepoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'mod-build-cache.ps1')
function Invoke-CheckToolTests {
    & python (Join-Path $PSScriptRoot 'tests/test_mod_docs.py')
    if ($LASTEXITCODE -ne 0) { throw 'Mod documentation checker regression failed' }
    foreach ($test in @('check-mods.Tests.ps1','mod-build-cache.Tests.ps1','private-component-inputs.Tests.ps1','build-stages.Tests.ps1','release-candidate.Tests.ps1')) {
        $path = Join-Path $PSScriptRoot "tests/$test"
        if ($test -eq 'private-component-inputs.Tests.ps1' -and -not (Test-Path -LiteralPath $path)) { continue }
        & $path
        if (-not $?) { throw "Check tooling regression failed: $test" }
    }
}
if ($Tooling) {
    if ($Mod -or $Integration -or $Full -or $List -or $Release -or $ChangedSince -or $ExcludeMod -or $NoCache -or $WorldsmithDir) { throw 'Run -Tooling separately from mod checks.' }
    Invoke-CheckToolTests
    return
}
if ($Release -and -not $Full) { $Integration = $true }
if ($Integration -and $Full) { throw 'Choose either -Integration or -Full.' }
$Tier = if ($Full) { 'Full' } elseif ($Integration) { 'Integration' } else { 'Fast' }
$Catalog = Get-ModComponentCatalog
$Modules = [ordered]@{}
foreach ($component in $Catalog) {
    if ($component.id -in @('red-crystal','palace-clock') -and -not (Test-Path -LiteralPath (Join-Path $RepoRoot $component.checks[0]))) { continue }
    $Modules[$component.id] = @($component.checks)
}
if ($Mod -contains 'red-crystal' -and -not $Modules.Contains('red-crystal')) { throw 'Red Crystal is available only in its source workspace.' }
if ($Mod -contains 'palace-clock' -and -not $Modules.Contains('palace-clock')) { throw 'Palace Clock is available only in its source workspace.' }
$HasEclipse = Test-Path -LiteralPath (Join-Path $RepoRoot 'mods/total-eclipse/build.ps1') -PathType Leaf
$HasMetroidvania = Test-Path -LiteralPath (Join-Path $RepoRoot 'mods/metroidvania/build.ps1') -PathType Leaf
if ($Mod -contains 'metroidvania' -and -not $HasMetroidvania) { throw 'Metroidvania is available only in its development workspace.' }
if ($Mod -contains 'total-eclipse' -and -not $HasEclipse) { throw 'Total Eclipse is a map-owned module available only in its source workspace.' }
$Requested = if ($Mod) { @($Mod | ForEach-Object { if ($_ -eq 'hammer-king') { 'more-items' } else { $_ } }) } else { @($Modules.Keys | Where-Object { ($_ -ne 'screen-solver' -or $Full) -and $_ -notin @('worldsmith-extension','multiplayer-expansion','red-crystal','palace-clock') -and ($_ -ne 'total-eclipse' -or $HasEclipse) -and ($_ -ne 'metroidvania' -or $HasMetroidvania) }) }
foreach ($excluded in $ExcludeMod) { if (-not $Modules.Contains($excluded)) { throw "Unknown excluded mod: $excluded" } }
if ($ChangedSince) {
    $ref = @(& git -C $RepoRoot rev-parse --verify ($ChangedSince + '^{commit}'))
    if ($LASTEXITCODE -ne 0) { throw "Unknown change baseline: $ChangedSince" }
    $changed = @(& git -C $RepoRoot -c core.quotepath=false diff --name-only $ref[0] --)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect changed tracked files' }
    $changed += @(& git -C $RepoRoot -c core.quotepath=false ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect untracked inputs' }
    $impact = @(Get-ModImpact $changed @($Modules.Keys | Where-Object { $_ -ne 'screen-solver' -or $Full }))
    if ($Mod) { $Requested = @($Requested | Where-Object { $impact -contains $_ }) }
    else { $Requested = @($impact | Where-Object { ($_ -ne 'total-eclipse' -or $HasEclipse) -and ($_ -ne 'metroidvania' -or $HasMetroidvania) }) }
}
$Requested = @($Requested | Where-Object { $ExcludeMod -notcontains $_ })
if ($Requested.Count -eq 0) {
    if (-not $List) { Write-Host '[OK] No mod inputs changed in the selected scope.' }
    return
}
if ($Requested -contains 'screen-solver' -and -not $Full) { throw 'Retained Screen Solver conformance is slow; request it explicitly with -Full.' }
$Selected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($id in $Requested) { [void]$Selected.Add($(if ($id -eq 'hammer-king') { 'more-items' } else { $id })) }
# use the same dependency edges for impact, inputs and execution order
$expanded = $true
while ($expanded) {
    $expanded = $false
    foreach ($component in $Catalog) {
        if (-not $Selected.Contains($component.id)) { continue }
        foreach ($dependency in $component.dependencies) { if ($Selected.Add($dependency)) { $expanded = $true } }
    }
}
foreach ($excluded in $ExcludeMod) {
    if ($Selected.Contains($excluded)) { throw "Excluded mod is required by the selected checks: $excluded. Narrow the selected consumers instead." }
}
$Checks = @(
    foreach ($id in $Modules.Keys) {
        if (-not $Selected.Contains($id)) { continue }
        # dependencies supply checked build artifacts, not an unrelated compatibility audit
        $stageTier = if ($Full -or $Requested -contains $id) { $Tier } else { 'Fast' }
        $scripts = @(if ($id -eq 'jk-runtime' -and $stageTier -ne 'Full') { $Modules[$id][0] } else { $Modules[$id] })
        if ($id -eq 'jk-runtime' -and $stageTier -eq 'Integration') { $scripts += @('mods\jk-runtime\verify-harmony.ps1') }
        if ($id -eq 'run-verifier' -and $stageTier -ne 'Fast') { $scripts += @('mods\run-verifier\verify-graphics.ps1') }
        if ($id -eq 'jk-runtime' -and $stageTier -ne 'Fast') { $scripts += @('mods\jk-runtime\verify-binding-modes.ps1') }
        if ($id -eq 'mega-mapping-expansion' -and $stageTier -ne 'Fast') { $scripts += @('mods\mega-mapping-expansion\verify-scene-api.ps1', 'mods\mega-mapping-expansion\verify-construction-graphics.ps1', 'mods\mega-mapping-expansion\verify-ending-compatibility.ps1', 'mods\mega-mapping-expansion\verify-warp-shapes.ps1', 'mods\mega-mapping-expansion\verify-material-compatibility.ps1') }
        foreach ($script in $scripts) {
            $arguments = @{ GameDir = $GameDir }
            if ($id -eq 'metroidvania' -and $stageTier -ne 'Fast') { $arguments.Graphics = $true }
            if ($id -eq 'worldsmith-extension' -and $WorldsmithDir) { $arguments.WorldsmithDir = $WorldsmithDir }
            if ($script -eq 'mods\mega-mapping-expansion\verify-ending-compatibility.ps1') { $arguments.Optional = -not $Release }
            if ($script -eq 'mods\mega-mapping-expansion\verify-warp-shapes.ps1') { $arguments.Optional = -not $Release }
            if ($script -eq 'mods\mega-mapping-expansion\verify-material-compatibility.ps1') { $arguments.Optional = -not $Release }
            if ($id -in @('no-walk-off','powers','subframe-charge','morph-ball')) { $arguments.Tier = $stageTier }
            if ($id -eq 'subframe-charge' -and $Selected.Contains('smooth-camera')) { $arguments.CameraAssembly = Join-Path $RepoRoot 'build/smooth-camera/_INTERNAL/SmoothCamera.Module.dll' }
            if ($id -eq 'morph-ball' -and $Selected.Contains('subframe-charge')) { $arguments.ChargeAssembly = Join-Path $RepoRoot 'build/subframe-charge/_INTERNAL/SubframeCharge.Module.dll' }
            if ($id -eq 'powers' -and $stageTier -ne 'Fast' -and $Selected.Contains('morph-ball') -and $Selected.Contains('more-items')) {
                $arguments.BallAssembly = Join-Path $RepoRoot 'build/morph-ball/_INTERNAL/MorphBall.Module.dll'
                $arguments.ItemsAssembly = Join-Path $RepoRoot 'build/more-items/_INTERNAL/MoreItems.Module.dll'
            }
            if ($id -eq 'wardrobe-plus' -and $stageTier -ne 'Fast') {
                $arguments.Graphics = $true
                if ($Selected.Contains('morph-ball') -and $Selected.Contains('replays') -and $Selected.Contains('mega-mapping-expansion')) { $arguments.Compatibility = $true }
            }
            if ($id -eq 'more-items' -and $stageTier -ne 'Fast') { $arguments.Graphics = $true }
            if ($id -eq 'more-items' -and $Selected.Contains('subframe-charge')) {
                $arguments.ChargePackage = Join-Path $RepoRoot 'build/subframe-charge/UPLOAD_TO_WORKSHOP/SubframeCharge.dll'
                if ($Selected.Contains('casual-jumping')) { $arguments.CasualPackage = Join-Path $RepoRoot 'build/casual-jumping/UPLOAD_TO_WORKSHOP/CasualJumping.dll' }
            }
            if ($id -eq 'smooth-camera' -and $stageTier -ne 'Fast') {
                $arguments.Graphics = $true
                if ($Selected.Contains('mega-mapping-expansion')) { $arguments.MappingCompatibility = $true }
            }
            if ($id -eq 'overlay-plus' -and $stageTier -ne 'Fast') { $arguments.Graphics = $true }
            if ($id -eq 'prism' -and $stageTier -ne 'Fast') { $arguments.Graphics = $true }
            if ($id -eq 'god' -and $stageTier -ne 'Fast') { $arguments.Graphics = $true }
            if ($id -eq 'free-scaling' -and $stageTier -ne 'Fast') { $arguments.Graphics = $true }
            if ($id -eq 'overlay-plus' -and $Selected.Contains('smooth-camera')) { $arguments.CameraAssembly = Join-Path $RepoRoot 'build/smooth-camera/_INTERNAL/SmoothCamera.Module.dll' }
            if ($id -eq 'overlay-plus' -and $Selected.Contains('replays')) { $arguments.ReplayAssembly = Join-Path $RepoRoot 'build/replays/_INTERNAL/Replays.Module.dll' }
            [pscustomobject]@{ Mod = $id; Stage = ($id + '/' + [IO.Path]::GetFileNameWithoutExtension($script)); Script = $script; Tier = $stageTier; Arguments = $arguments }
        }
    }
    if (-not $Mod -and -not $ChangedSince -and -not $ExcludeMod -and ($Integration -or $Full)) {
        foreach ($script in @('mods\jk-runtime\verify-packages.ps1','mods\jk-runtime\tests\InstallerTests.ps1')) {
            [pscustomobject]@{ Mod = 'package-integration'; Stage = ('package-integration/' + [IO.Path]::GetFileNameWithoutExtension($script)); Script = $script; Tier = $Tier; Arguments = @{ GameDir = $GameDir } }
        }
    }
)
# Listing is read-only: no builds, fixture extraction, directories or installs
if ($List) { $Checks; return }

Push-Location $RepoRoot
$PreviousBuildSession = Get-Variable -Name JKModBuildSession -Scope Global -ErrorAction SilentlyContinue
$SavedBuildSession = if ($PreviousBuildSession) { $PreviousBuildSession.Value } else { $null }
$global:JKModBuildSession = @{}
$StageResults = [Collections.Generic.List[object]]::new()
$ReceiptPath = Join-Path $RepoRoot 'build/_work/check-receipts/latest.json'
. (Join-Path $RepoRoot 'mods/jk-runtime/build-support/build-retention.ps1')
try {
    $documentationArguments = @($Selected | Where-Object { $_ -ne 'worldsmith-extension' } | Sort-Object | ForEach-Object { '--mod'; $_ })
    & python (Join-Path $PSScriptRoot 'check-mod-docs.py') @documentationArguments
    if ($LASTEXITCODE -ne 0) { throw 'Mod documentation validation failed' }
    if ($Full) { Invoke-CheckToolTests }
    $nativeInputs = @('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe') + @(Get-ChildItem -LiteralPath $GameDir -File | Where-Object { $_.Extension -in @('.dll','.exe') } | ForEach-Object FullName)
    $providerRoots = @((Join-Path $GameDir 'Content/JKMods'), (Join-Path (Split-Path (Split-Path $GameDir)) 'workshop/content/1061090'))
    $providerFiles = @($providerRoots | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-ChildItem -LiteralPath $_ -File -Recurse -Filter '*.dll' } | Sort-Object FullName -Unique)
    $nativeInputs += @($providerFiles | ForEach-Object FullName)
    $providerEvidence = @($providerFiles | ForEach-Object { @{ Path=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash; FileVersion=$_.VersionInfo.FileVersion } })
    $pythonCommand = Get-Command python -ErrorAction Stop
    if ($pythonCommand.Source) { $nativeInputs += $pythonCommand.Source }
    $environmentKey = $PSVersionTable.PSVersion.ToString() + '|' + [Environment]::OSVersion.VersionString + '|' + [IntPtr]::Size + '|' + (& python --version) + '|' + (@(Get-ChildItem Env: | Where-Object Name -Match '^(JK_|JKRUNTIME|SUBFRAME)' | Sort-Object Name | ForEach-Object { $_.Name+'='+$_.Value }) -join '|')
    foreach ($Check in $Checks) {
        Write-Host "[MOD $($Check.Tier)] $($Check.Mod): $($Check.Script)"
        $Elapsed = [Diagnostics.Stopwatch]::StartNew()
        $Arguments = $Check.Arguments
        $cacheable = $Check.Tier -eq 'Fast' -and -not $NoCache -and $Check.Mod -notin @('overlay-plus','screen-solver','worldsmith-extension','multiplayer-expansion')
        $cachePath = Join-Path $RepoRoot ("build/_work/cache/mod-checks/" + $Check.Stage.Replace('/','--') + '.json')
        $inputPaths = @(Get-ModStageInputs $RepoRoot $Check $nativeInputs)
        if ($Check.Mod -eq 'package-integration') { $inputPaths = @((Join-Path $RepoRoot 'mods/jk-runtime'),(Join-Path $RepoRoot 'scripts')) + $nativeInputs }
        $sourceKey = Get-ModContentKey $inputPaths
        $inputKey = Get-ModStageKey $Check.Stage $inputPaths $environmentKey $Arguments
        $stageResult = [ordered]@{ Stage=$Check.Stage; Component=$Check.Mod; Script=$Check.Script; Tier=$Check.Tier; Arguments=$Arguments; Status='Fail'; Reused=$false; Inputs=$inputPaths; InputKey=$sourceKey; Seconds=0 }
        $StageResults.Add($stageResult)
        if ($cacheable) {
            $outputs = @(Get-ModStageArtifacts $RepoRoot $Check.Mod)
            $record = Read-ModStageRecord $cachePath $inputKey $outputs
            if ($record) {
                if ($record.RuntimeSession) { $global:JKModBuildSession['jk-runtime'] = @{ Input=$record.RuntimeSession.Input; Output=$record.RuntimeSession.Output } }
                Write-Host ('[REUSE] {0}: successful Fast check matches all inputs and artifacts ({1:F2}s verification).' -f $Check.Mod, $Elapsed.Elapsed.TotalSeconds)
                $stageResult.Status='Pass'; $stageResult.Reused=$true; $stageResult.Seconds=$Elapsed.Elapsed.TotalSeconds
                continue
            }
        }
        if (Test-Path -LiteralPath $cachePath -PathType Leaf) { [IO.File]::Delete($cachePath) }
        $skipped = $false
        & (Join-Path $RepoRoot $Check.Script) @Arguments 6>&1 | ForEach-Object {
            if ("$_" -match '^\[SKIP\]') { $skipped = $true }
            Write-Host $_
        }
        if (-not $?) {
            throw "$($Check.Mod) verification failed: $($Check.Script)"
        }
        $stageResult.Status = if ($skipped) { 'Skipped' } else { 'Pass' }
        $stageResult.Seconds = $Elapsed.Elapsed.TotalSeconds
        if ($Release -and $skipped) { throw "Required release fixture was skipped: $($Check.Stage)" }
        if ($sourceKey -ne (Get-ModContentKey $inputPaths)) { $stageResult.Status='InputsChanged'; throw "Inputs changed during $($Check.Stage); rerun validation" }
        if ($cacheable -and -not $skipped) {
            $after = Get-ModStageKey $Check.Stage $inputPaths $environmentKey $Arguments
            if ($after -eq $inputKey) { Write-ModStageRecord $cachePath $inputKey @(Get-ModStageArtifacts $RepoRoot $Check.Mod) $global:JKModBuildSession['jk-runtime'] }
            else { Write-Host '[INFO] Inputs changed during execution; this result will not be reused.' }
        }
        Write-Host ('[TIME] {0}: {1:F2}s' -f $Check.Script, $Elapsed.Elapsed.TotalSeconds)
    }
    $Succeeded = $true
}
finally {
    try {
    $maintenanceStamp = Join-Path $RepoRoot 'build/_work/cache/maintenance.completed'
    if (-not (Test-Path -LiteralPath $maintenanceStamp) -or [IO.File]::GetLastWriteTimeUtc($maintenanceStamp) -lt [DateTime]::UtcNow.AddHours(-24)) {
        Remove-WorkspaceBuildHistory | Out-Null
        Invoke-WorkspaceMaintenance -Logs | Out-Null
        New-Item -ItemType Directory -Force -Path (Split-Path $maintenanceStamp) | Out-Null
        [IO.File]::WriteAllText($maintenanceStamp,[DateTime]::UtcNow.ToString('o'))
    }
    $componentResults = @(foreach ($id in @($Selected)) {
        $artifacts = @(Get-ModStageArtifacts $RepoRoot $id)
        if ($artifacts.Count -gt 0) { @{ Id=$id; Artifacts=$artifacts; ArtifactKey=(Get-ModContentKey $artifacts) } }
    })
    $receipt = @{ Schema=1; Actor='Niotid'; FinishedUtc=[DateTime]::UtcNow.ToString('o'); Success=[bool]$Succeeded; Release=[bool]$Release; Tier=$Tier; Stages=@($StageResults.ToArray()); ProviderRoots=$providerRoots; Providers=$providerEvidence; Environment=$environmentKey; Components=$componentResults; Seconds=$Total.Elapsed.TotalSeconds }
    New-Item -ItemType Directory -Force -Path (Split-Path $ReceiptPath) | Out-Null
    if (Test-Path -LiteralPath $ReceiptPath) { Copy-Item -LiteralPath $ReceiptPath -Destination (Join-Path (Split-Path $ReceiptPath) 'previous.json') -Force }
    [IO.File]::WriteAllText($ReceiptPath,($receipt | ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    } finally {
    if ($PreviousBuildSession) { $global:JKModBuildSession = $SavedBuildSession }
    else { Remove-Variable -Name JKModBuildSession -Scope Global -ErrorAction SilentlyContinue }
    Pop-Location
    Write-Host ('[TIME] Complete {0} command including maintenance: {1:F2}s; {2} stages; success={3}' -f $Tier, $Total.Elapsed.TotalSeconds, $Checks.Count, [bool]$Succeeded)
}
    }
