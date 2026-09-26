$ErrorActionPreference = 'Stop'
$taskCheck = Join-Path $PSScriptRoot '..\check-mods.ps1'
function Assert-Plan([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }

# Exercise the planner through its public read-only interface. These tests do
# not compile any mod, load the game, launch a solver or touch installed files.
$fast = @(& $taskCheck -Mod mega-gameplay-expansion -List)
Assert-Plan (@($fast | Where-Object { $_.Mod -notin @('jk-runtime','mega-gameplay-expansion') }).Count -eq 0) 'Targeted checks included unrelated mods'
Assert-Plan ($fast[0].Mod -eq 'jk-runtime') 'Runtime dependency must precede its consumer'
$mega = @($fast | Where-Object Mod -eq 'mega-gameplay-expansion')
Assert-Plan ($mega.Count -eq 1 -and $mega[0].Arguments.Tier -eq 'Fast') 'Targeted fast tier was not forwarded'
$dedup = @(& $taskCheck -Mod @('mega-gameplay-expansion','mega-gameplay-expansion','JK-RUNTIME') -List)
Assert-Plan (@($dedup | Where-Object Mod -eq 'mega-gameplay-expansion').Count -eq 1) 'Duplicate requests would rebuild the same mod'
Assert-Plan (@($dedup | Group-Object Script | Where-Object Count -gt 1).Count -eq 0) 'Dependencies were scheduled more than once'
$allFast = @(& $taskCheck -List)
$stereo = @(& $taskCheck -Mod stereo-madness -List)
Assert-Plan (($stereo.Mod -join ',') -eq 'jk-runtime,mega-mapping-expansion,stereo-madness') 'Stereo needs Runtime and Mapping built before its controller'
Assert-Plan (@($allFast | Where-Object { $_.Mod -in @('screen-solver','package-integration') }).Count -eq 0) 'Slow/global integration checks leaked into the default tier'
$integration = @(& $taskCheck -Integration -List)
Assert-Plan (@($integration | Where-Object Mod -eq 'package-integration').Count -gt 0) 'Integration must include package and installer verification'
Assert-Plan (@($integration | Where-Object Mod -eq 'screen-solver').Count -eq 0) 'Integration unexpectedly launched the retained solver'
$focusedIntegration = @(& $taskCheck -Mod mega-gameplay-expansion -Integration -List)
$mappingPlan = @(& $taskCheck -Mod mega-mapping-expansion -Integration -List)
$endingCompatibility = @($mappingPlan | Where-Object { $_.Script -like '*verify-ending-compatibility.ps1' })
Assert-Plan ($endingCompatibility.Count -eq 1 -and $endingCompatibility[0].Arguments.Optional) 'Mapping integration must check the installed ending provider without requiring its subscription'
Assert-Plan (@($allFast | Where-Object { $_.Script -like '*verify-ending-compatibility.ps1' }).Count -eq 0) 'Installed foreign-provider checks belong to integration'
$hammerPlan = @(& $taskCheck -Mod hammer-king -Integration -List)
$cameraPlan = @(& $taskCheck -Mod smooth-camera -Integration -List)
Assert-Plan (@($cameraPlan | Where-Object { $_.Mod -notin @('jk-runtime','smooth-camera') }).Count -eq 0) 'Camera checks included unrelated mods'
Assert-Plan (($cameraPlan | Where-Object Mod -eq 'smooth-camera').Arguments.Graphics -eq $true) 'Camera integration must include its native graphics fixture'
Assert-Plan (@($hammerPlan | Where-Object { $_.Mod -notin @('jk-runtime','more-items') }).Count -eq 0) 'Retired Hammer alias must select its More Items owner'
Assert-Plan (($hammerPlan | Where-Object Mod -eq 'more-items').Arguments.Graphics -eq $true) 'Integrated Hammer checks must include its native graphics fixture'
$itemsAlias = @(& $taskCheck -Mod @('hammer-king','more-items') -List)
Assert-Plan (@($itemsAlias | Where-Object Mod -eq 'more-items').Count -eq 1) 'Alias and owner must not build More Items twice'
foreach ($stage in $mappingPlan) {
    Assert-Plan (Test-Path -LiteralPath (Join-Path (Join-Path $PSScriptRoot '../..') $stage.Script) -PathType Leaf) 'Integration stages must remain separate callable script paths'
}
Assert-Plan (@($focusedIntegration | Where-Object Mod -eq 'package-integration').Count -eq 0) 'Focused checks must not consume stale unrelated packages'
Assert-Plan (($focusedIntegration | Where-Object Mod -eq 'mega-gameplay-expansion').Arguments.Tier -eq 'Integration') 'Integration tier was not forwarded'
$full = @(& $taskCheck -Full -List)
Assert-Plan (@($full | Where-Object Mod -eq 'screen-solver').Count -gt 0) 'Full no longer contains retained conformance coverage'
$solver = @(& $taskCheck -Mod screen-solver -Full -List)
$solverIndex = [Array]::FindIndex([object[]]$solver,[Predicate[object]]{ param($entry) $entry.Mod -eq 'screen-solver' })
foreach ($dependency in @('jk-runtime','subframe-charge','more-items','morph-ball','replays','mega-gameplay-expansion')) {
    $index = [Array]::FindIndex([object[]]$solver,[Predicate[object]]{ param($entry) $entry.Mod -eq $dependency })
    Assert-Plan ($index -ge 0 -and $index -lt $solverIndex) "Solver dependency missing or late: $dependency"
}
foreach ($invalid in @(@{ Mod='missing-mod' },@{ Mod='screen-solver' },@{ Integration=$true; Full=$true },
    @{ Mod='more-items'; ExcludeMod='jk-runtime' }, @{ Mod='screen-solver'; Full=$true; ExcludeMod='more-items' },
    @{ Mod='stereo-madness'; ExcludeMod='mega-mapping-expansion' })) {
    $rejected=$false
    try { & $taskCheck @invalid -List | Out-Null } catch { $rejected=$true }
    Assert-Plan $rejected 'Invalid selection should fail before running anything'
}
$checkAll = Join-Path $PSScriptRoot '..\check-all.ps1'
if (Test-Path -LiteralPath $checkAll) {
$forwarded = [Collections.Generic.List[object]]::new()
$previousExitCode = $global:LASTEXITCODE
$hostResult = @{ ExitCode = 0 }
function Get-Process { [pscustomobject]@{ Path='Invoke-CheckStub' } }
function Invoke-CheckStub { $forwarded.Add(@($args)); $global:LASTEXITCODE=$hostResult.ExitCode }
try {
    foreach ($mode in @(@{},@{ Integration=$true },@{ Full=$true })) {
        $forwarded.Clear()
        & $checkAll @mode *> $null
        Assert-Plan ($forwarded.Count -eq 2) 'Workspace runner must dispatch both source and mod checks'
        foreach ($arguments in $forwarded) {
            Assert-Plan (($arguments -contains '-Full') -eq [bool]$mode.Full) 'Workspace Full choice was not forwarded'
            Assert-Plan (($arguments -contains '-Integration') -eq [bool]$mode.Integration) 'Workspace Integration choice was not forwarded'
        }
    }
    $forwarded.Clear(); $hostResult.ExitCode=1; $rejected=$false
    try { & $checkAll *> $null } catch { $rejected=$true }
    Assert-Plan ($rejected -and $forwarded.Count -eq 1) 'Failed source checks should stop the workspace runner'
} finally {
    $global:LASTEXITCODE = $previousExitCode
}
}
Write-Host '[OK] Mod check planner: scope, tier forwarding, dependency ordering/deduplication, explicit slow coverage, invalid requests and workspace failure propagation'

$composition = @(& $taskCheck -Mod @('morph-ball','subframe-charge','more-items','smooth-camera','mega-mapping-expansion') -Integration -List)
$overlayOnly = @(& $taskCheck -Mod overlay-plus -Integration -List)
$overlayStep = $overlayOnly | Where-Object Mod -eq 'overlay-plus'
Assert-Plan (-not $overlayStep.Arguments.CameraAssembly -and -not $overlayStep.Arguments.ReplayAssembly) 'Focused overlay checks must not silently load stale peer outputs'
$overlayPeers = @(& $taskCheck -Mod @('overlay-plus','smooth-camera','replays') -Integration -List)
$overlayStep = $overlayPeers | Where-Object Mod -eq 'overlay-plus'
Assert-Plan ($overlayStep.Arguments.CameraAssembly -and $overlayStep.Arguments.ReplayAssembly) 'Overlay compatibility must receive explicitly selected peers'
$overlayOrder = @($overlayPeers.Mod)
Assert-Plan ([Array]::IndexOf($overlayOrder,'replays') -lt [Array]::IndexOf($overlayOrder,'overlay-plus') -and [Array]::IndexOf($overlayOrder,'smooth-camera') -lt [Array]::IndexOf($overlayOrder,'overlay-plus')) 'Overlay peers must build before adapter checks'
$camera = $composition | Where-Object Mod -eq 'smooth-camera'
$charge = $composition | Where-Object Mod -eq 'subframe-charge'
$ball = $composition | Where-Object Mod -eq 'morph-ball'
Assert-Plan ($camera.Arguments.MappingCompatibility -and $charge.Arguments.CameraAssembly -and $ball.Arguments.ChargeAssembly) 'Requested composition must use explicit fresh peer outputs'
$names = @($composition.Mod)
Assert-Plan ([Array]::IndexOf($names,'mega-mapping-expansion') -lt [Array]::IndexOf($names,'smooth-camera') -and [Array]::IndexOf($names,'smooth-camera') -lt [Array]::IndexOf($names,'subframe-charge')) 'Composition inputs must precede consumers'
$excluded = @(& $taskCheck -ExcludeMod @('overlay-plus','screen-solver') -Integration -List)
Assert-Plan (@($excluded | Where-Object { $_.Mod -in @('overlay-plus','screen-solver','package-integration') }).Count -eq 0) 'Explicit exclusions must not trigger global stale-package checks'
Assert-Plan (($ball.Arguments.Tier -eq 'Integration') -and $charge.Arguments.Tier -eq 'Integration') 'Ball and SFC must receive integration coverage explicitly'
$allCompositions = @(& $taskCheck -ExcludeMod @('overlay-plus','screen-solver') -Integration -List)
$items = $allCompositions | Where-Object Mod -eq 'more-items'
$gameplay = $allCompositions | Where-Object Mod -eq 'mega-gameplay-expansion'
Assert-Plan ($items.Arguments.CasualPackage -and $items.Arguments.ChargePackage -and $gameplay.Arguments.BallAssembly -and $gameplay.Arguments.ItemsAssembly) 'Real controller compositions must receive explicit fresh peer packages'
