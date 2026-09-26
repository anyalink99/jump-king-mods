param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [ValidateSet('jk-runtime','casual-jumping','subframe-charge','more-items','morph-ball','replays','run-verifier','wardrobe-plus','mega-mapping-expansion','mega-gameplay-expansion','screen-solver','hammer-king','smooth-camera','overlay-plus','stereo-madness')]
    [string[]]$Mod,
    [switch]$Integration,
    [switch]$Full,
    [switch]$List,
    [string[]]$ExcludeMod,
    [string]$ChangedSince,
    [switch]$NoCache
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'mod-build-cache.ps1')
if ($Integration -and $Full) { throw 'Choose either -Integration or -Full.' }
$Tier = if ($Full) { 'Full' } elseif ($Integration) { 'Integration' } else { 'Fast' }
$Modules = [ordered]@{
    'jk-runtime' = @('mods\jk-runtime\verify-api.ps1','mods\jk-runtime\verify-harmony.ps1','mods\jk-runtime\verify-text-compat.ps1','mods\jk-runtime\verify-conveyor-compat.ps1','mods\jk-runtime\verify-manager-compat.ps1')
    'casual-jumping' = @('mods\casual-jumping\build.ps1')
    'mega-mapping-expansion' = @('mods\mega-mapping-expansion\build.ps1')
    'stereo-madness' = @('mods\stereo-madness\build.ps1')
    'smooth-camera' = @('mods\smooth-camera\build.ps1')
    'subframe-charge' = @('mods\subframe-charge\build.ps1')
    'more-items' = @('mods\more-items\verify-api.ps1')
    'morph-ball' = @('mods\morph-ball\build.ps1')
    'replays' = @('mods\replays\build.ps1')
    'run-verifier' = @('mods\run-verifier\build.ps1')
    'overlay-plus' = @('mods\overlay-plus\build.ps1')
    'wardrobe-plus' = @('mods\wardrobe-plus\build.ps1')
    'mega-gameplay-expansion' = @('mods\mega-gameplay-expansion\build.ps1')
    'screen-solver' = @('mods\screen-solver\build.ps1')
}
$Requested = if ($Mod) { @($Mod | ForEach-Object { if ($_ -eq 'hammer-king') { 'more-items' } else { $_ } }) } else { @($Modules.Keys | Where-Object { $_ -ne 'screen-solver' -or $Full }) }
foreach ($excluded in $ExcludeMod) { if (-not $Modules.Contains($excluded)) { throw "Unknown excluded mod: $excluded" } }
if ($ChangedSince) {
    $ref = @(& git -C $RepoRoot rev-parse --verify ($ChangedSince + '^{commit}'))
    if ($LASTEXITCODE -ne 0) { throw "Unknown change baseline: $ChangedSince" }
    $changed = @(& git -C $RepoRoot -c core.quotepath=false diff --name-only $ref[0] --)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect changed tracked files' }
    $changed += @(& git -C $RepoRoot -c core.quotepath=false ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect untracked inputs' }
    $impact = @(Get-ModImpact $changed @($Modules.Keys | Where-Object { $_ -ne 'screen-solver' -or $Full }))
    $Requested = @($Requested | Where-Object { $impact -contains $_ })
}
$Requested = @($Requested | Where-Object { $ExcludeMod -notcontains $_ })
if ($Requested.Count -eq 0) {
    if (-not $List) { Write-Host '[OK] No mod inputs changed in the selected scope.' }
    return
}
if ($Requested -contains 'screen-solver' -and -not $Full) { throw 'Retained Screen Solver conformance is slow; request it explicitly with -Full.' }
$Selected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($id in $Requested) { [void]$Selected.Add($(if ($id -eq 'hammer-king') { 'more-items' } else { $id })) }
# Every package needs a fresh Runtime SDK. Solver additionally pins these exact
# first-party build bytes; never validate it against stale unrelated packages.
[void]$Selected.Add('jk-runtime')
if ($Selected.Contains('stereo-madness')) { [void]$Selected.Add('mega-mapping-expansion') }
if ($Selected.Contains('screen-solver')) {
    foreach ($id in @('subframe-charge','more-items','morph-ball','replays','mega-gameplay-expansion')) { [void]$Selected.Add($id) }
}
foreach ($excluded in $ExcludeMod) {
    if ($Selected.Contains($excluded)) { throw "Excluded mod is required by the selected checks: $excluded. Narrow the selected consumers instead." }
}
$Checks = @(
    foreach ($id in $Modules.Keys) {
        if (-not $Selected.Contains($id)) { continue }
        $scripts = @(if ($id -eq 'jk-runtime' -and $Tier -eq 'Fast') { $Modules[$id][0] } else { $Modules[$id] })
        if ($id -eq 'run-verifier' -and $Tier -ne 'Fast') { $scripts += @('mods\run-verifier\verify-graphics.ps1') }
        if ($id -eq 'mega-mapping-expansion' -and $Tier -ne 'Fast') { $scripts += @('mods\mega-mapping-expansion\verify-scene-api.ps1', 'mods\mega-mapping-expansion\verify-behavior-graphics.ps1', 'mods\mega-mapping-expansion\verify-ending-compatibility.ps1') }
        foreach ($script in $scripts) {
            $arguments = @{ GameDir = $GameDir }
            if ($script -eq 'mods\mega-mapping-expansion\verify-ending-compatibility.ps1') { $arguments.Optional = $true }
            if ($id -in @('mega-gameplay-expansion','subframe-charge','morph-ball')) { $arguments.Tier = $Tier }
            if ($id -eq 'subframe-charge' -and $Selected.Contains('smooth-camera')) { $arguments.CameraAssembly = Join-Path $RepoRoot 'build/smooth-camera/_INTERNAL/SmoothCamera.Module.dll' }
            if ($id -eq 'morph-ball' -and $Selected.Contains('subframe-charge')) { $arguments.ChargeAssembly = Join-Path $RepoRoot 'build/subframe-charge/_INTERNAL/SubframeCharge.Module.dll' }
            if ($id -eq 'mega-gameplay-expansion' -and $Tier -ne 'Fast' -and $Selected.Contains('morph-ball') -and $Selected.Contains('more-items')) {
                $arguments.BallAssembly = Join-Path $RepoRoot 'build/morph-ball/_INTERNAL/MorphBall.Module.dll'
                $arguments.ItemsAssembly = Join-Path $RepoRoot 'build/more-items/_INTERNAL/MoreItems.Module.dll'
            }
            if ($id -eq 'wardrobe-plus' -and $Tier -ne 'Fast') {
                $arguments.Graphics = $true
                if ($Selected.Contains('morph-ball') -and $Selected.Contains('replays') -and $Selected.Contains('mega-mapping-expansion')) { $arguments.Compatibility = $true }
            }
            if ($id -eq 'more-items' -and $Tier -ne 'Fast') { $arguments.Graphics = $true }
            if ($id -eq 'more-items' -and $Selected.Contains('subframe-charge')) {
                $arguments.ChargePackage = Join-Path $RepoRoot 'build/subframe-charge/UPLOAD_TO_WORKSHOP/SubframeCharge.dll'
                if ($Selected.Contains('casual-jumping')) { $arguments.CasualPackage = Join-Path $RepoRoot 'build/casual-jumping/UPLOAD_TO_WORKSHOP/CasualJumping.dll' }
            }
            if ($id -eq 'smooth-camera' -and $Tier -ne 'Fast') {
                $arguments.Graphics = $true
                if ($Selected.Contains('mega-mapping-expansion')) { $arguments.MappingCompatibility = $true }
            }
            if ($id -eq 'overlay-plus' -and $Tier -ne 'Fast') { $arguments.Graphics = $true }
            if ($id -eq 'overlay-plus' -and $Selected.Contains('smooth-camera')) { $arguments.CameraAssembly = Join-Path $RepoRoot 'build/smooth-camera/_INTERNAL/SmoothCamera.Module.dll' }
            if ($id -eq 'overlay-plus' -and $Selected.Contains('replays')) { $arguments.ReplayAssembly = Join-Path $RepoRoot 'build/replays/_INTERNAL/Replays.Module.dll' }
            [pscustomobject]@{ Mod = $id; Script = $script; Tier = $Tier; Arguments = $arguments }
        }
    }
    if (-not $Mod -and -not $ChangedSince -and -not $ExcludeMod -and ($Integration -or $Full)) {
        foreach ($script in @('mods\jk-runtime\verify-packages.ps1','mods\jk-runtime\tests\InstallerTests.ps1')) {
            [pscustomobject]@{ Mod = 'package-integration'; Script = $script; Tier = $Tier; Arguments = @{ GameDir = $GameDir } }
        }
    }
)
# Listing is read-only: no builds, fixture extraction, directories or installs.
if ($List) { $Checks; return }

Push-Location $RepoRoot
$PreviousBuildSession = Get-Variable -Name JKModBuildSession -Scope Global -ErrorAction SilentlyContinue
$SavedBuildSession = if ($PreviousBuildSession) { $PreviousBuildSession.Value } else { $null }
$global:JKModBuildSession = @{}
try {
    $Total = [Diagnostics.Stopwatch]::StartNew()
    $documentationArguments = @($Selected | Sort-Object | ForEach-Object { '--mod'; $_ })
    & python (Join-Path $PSScriptRoot 'check-mod-docs.py') @documentationArguments
    if ($LASTEXITCODE -ne 0) { throw 'Mod documentation validation failed' }
    & python (Join-Path $PSScriptRoot 'tests/test_mod_docs.py')
    if ($LASTEXITCODE -ne 0) { throw 'Mod documentation checker regression failed' }
    & (Join-Path $PSScriptRoot 'tests\check-mods.Tests.ps1')
    if (-not $?) { throw 'Mod check planner regression failed' }
    & (Join-Path $PSScriptRoot 'tests/mod-build-cache.Tests.ps1')
    if (-not $?) { throw 'Mod cache regression failed' }
    $nativeInputs = @('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe') + @(Get-ChildItem -LiteralPath $GameDir -File | Where-Object { $_.Extension -in @('.dll','.exe') } | ForEach-Object FullName)
    $pythonCommand = Get-Command python -ErrorAction Stop
    if ($pythonCommand.Source) { $nativeInputs += $pythonCommand.Source }
    $environmentKey = $PSVersionTable.PSVersion.ToString() + '|' + [Environment]::OSVersion.VersionString + '|' + [IntPtr]::Size + '|' + (& python --version) + '|' + (@(Get-ChildItem Env: | Where-Object Name -Match '^(JK_|JKRUNTIME|SUBFRAME)' | Sort-Object Name | ForEach-Object { $_.Name+'='+$_.Value }) -join '|')
    foreach ($Check in $Checks) {
        Write-Host "[MOD $Tier] $($Check.Mod): $($Check.Script)"
        $Elapsed = [Diagnostics.Stopwatch]::StartNew()
        $Arguments = $Check.Arguments
        $cacheable = $Tier -eq 'Fast' -and -not $NoCache -and $Check.Mod -notin @('overlay-plus','screen-solver')
        $cachePath = Join-Path $RepoRoot ("build/mod-check-cache/" + $Check.Mod + '.json')
        $inputKey = $null
        if ($cacheable) {
            $inputPaths = @(Get-ModStageInputs $RepoRoot $Check $nativeInputs)
            $inputKey = (Get-ModContentKey $inputPaths) + '|' + $environmentKey + '|' + (Get-ModArgumentsKey $Arguments)
            $outputs = @(Get-ModStageArtifacts $RepoRoot $Check.Mod)
            $record = Read-ModStageRecord $cachePath $inputKey $outputs
            if ($record) {
                if ($record.RuntimeSession) { $global:JKModBuildSession['jk-runtime'] = @{ Input=$record.RuntimeSession.Input; Output=$record.RuntimeSession.Output } }
                Write-Host ('[REUSE] {0}: successful Fast check matches all inputs and artifacts ({1:F2}s verification).' -f $Check.Mod, $Elapsed.Elapsed.TotalSeconds)
                continue
            }
        }
        if (Test-Path -LiteralPath $cachePath -PathType Leaf) { [IO.File]::Delete($cachePath) }
        & (Join-Path $RepoRoot $Check.Script) @Arguments
        if (-not $?) {
            throw "$($Check.Mod) verification failed: $($Check.Script)"
        }
        if ($cacheable) {
            $after = (Get-ModContentKey $inputPaths) + '|' + $environmentKey + '|' + (Get-ModArgumentsKey $Arguments)
            if ($after -eq $inputKey) { Write-ModStageRecord $cachePath $inputKey @(Get-ModStageArtifacts $RepoRoot $Check.Mod) $global:JKModBuildSession['jk-runtime'] }
            else { Write-Host '[INFO] Inputs changed during execution; this result will not be reused.' }
        }
        Write-Host ('[TIME] {0}: {1:F2}s' -f $Check.Script, $Elapsed.Elapsed.TotalSeconds)
    }
    Write-Host ('[OK] {0} mod checks: {1} stages in {2:F2}s' -f $Tier, $Checks.Count, $Total.Elapsed.TotalSeconds)
}
finally {
    if ($PreviousBuildSession) { $global:JKModBuildSession = $SavedBuildSession }
    else { Remove-Variable -Name JKModBuildSession -Scope Global -ErrorAction SilentlyContinue }
    Pop-Location
}
