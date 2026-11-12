$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../mod-build-cache.ps1')
function Assert-Cache([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
$testRoot = Join-Path (Join-Path $PSScriptRoot '../../build/_work') ('jk-mod-cache-' + [Guid]::NewGuid().ToString('N'))
$inputDirectory = Join-Path $testRoot 'source'
$outputDirectory = Join-Path $testRoot 'package'
New-Item -ItemType Directory -Path $inputDirectory,$outputDirectory | Out-Null
$source = Join-Path $inputDirectory 'module.cs'
$output = Join-Path $outputDirectory 'module.dll'
$recordPath = Join-Path $testRoot 'cache.json'
try {
    $catalogPath = Join-Path $testRoot 'components.json'
    $savedCatalogPath = $ModCatalogPath
    try {
        $ModCatalogPath = $catalogPath
        [IO.File]::WriteAllText($catalogPath,'{"schema":1,"components":[{"id":"consumer","dependencies":["base"]},{"id":"base","dependencies":[]}]}')
        Assert-Cache ((@(Get-ModComponentCatalog).id -join ',') -eq 'base,consumer') 'Dependency order must come from edges rather than declaration order'
        foreach ($invalid in @(
            '{"schema":1,"components":[{"id":"consumer","dependencies":["missing"]}]}',
            '{"schema":1,"components":[{"id":"consumer","dependencies":["base"]},{"id":"base","dependencies":["consumer"]}]}',
            '{"schema":1,"components":[{"id":"same","dependencies":[]},{"id":"same","dependencies":[]}]}'
        )) {
            [IO.File]::WriteAllText($catalogPath,$invalid)
            $rejected=$false;try { Get-ModComponentCatalog | Out-Null } catch { $rejected=$true }
            Assert-Cache $rejected 'Invalid component graph was accepted'
        }
    } finally { $ModCatalogPath=$savedCatalogPath }
    [IO.File]::WriteAllText($source, 'aaaa')
    [IO.File]::WriteAllText($output, 'binary')
    $key = Get-ModContentKey @($inputDirectory)
    $pythonCache = Join-Path $inputDirectory '__pycache__'
    New-Item -ItemType Directory -Path $pythonCache | Out-Null
    [IO.File]::WriteAllText((Join-Path $pythonCache 'module.cpython-310.pyc'),'generated cache')
    Assert-Cache ((Get-ModContentKey @($inputDirectory)) -eq $key) 'First-run interpreter caches must not invalidate verified source'
    Assert-Cache ($null -eq (Read-ModStageRecord $recordPath $key @($outputDirectory))) 'Missing record must execute'
    Write-ModStageRecord $recordPath $key @($outputDirectory) @{ Input='sdk-input'; Output='sdk-output' }
    $record = Read-ModStageRecord $recordPath $key @($outputDirectory)
    Assert-Cache ($record -and $record.RuntimeSession.Input -eq 'sdk-input') 'Successful stage must preserve dependency validation'
    $timestamp = (Get-Item -LiteralPath $source).LastWriteTimeUtc
    [IO.File]::WriteAllText($source, 'bbbb')
    (Get-Item -LiteralPath $source).LastWriteTimeUtc = $timestamp
    $changed = Get-ModContentKey @($inputDirectory)
    Assert-Cache ($changed -ne $key -and $null -eq (Read-ModStageRecord $recordPath $changed @($outputDirectory))) 'Same-length/same-time edit must invalidate'
    [IO.File]::WriteAllText((Join-Path $inputDirectory 'new.cs'), 'new')
    Assert-Cache ((Get-ModContentKey @($inputDirectory)) -ne $changed) 'New source files must invalidate'
    [IO.File]::WriteAllText($output, 'damage')
    Assert-Cache ($null -eq (Read-ModStageRecord $recordPath $key @($outputDirectory))) 'Damaged output must invalidate'
    [IO.File]::Delete($output)
    Assert-Cache ($null -eq (Read-ModStageRecord $recordPath $key @($outputDirectory))) 'Missing output must invalidate'
    [IO.File]::WriteAllText($recordPath, '{broken')
    Assert-Cache ($null -eq (Read-ModStageRecord $recordPath $key @($outputDirectory))) 'Corrupt record must execute'
    Assert-Cache ((Get-ModArgumentsKey @{ Z=2; A=1 }) -eq (Get-ModArgumentsKey @{ A=1; Z=2 })) 'Argument order must not change cache identity'
    # compile, visual and gameplay are independent receipts, even with identical bytes
    [IO.File]::WriteAllText($output, 'binary')
    $executed = [Collections.Generic.List[string]]::new()
    foreach ($pass in 1..2) {
        foreach ($stage in @('fixture/build','fixture/visuals','fixture/gameplay')) {
            $stageKey = Get-ModStageKey $stage @($inputDirectory) 'test' @{}
            $stagePath = Join-Path $testRoot ($stage.Replace('/','--') + '.json')
            if (-not (Read-ModStageRecord $stagePath $stageKey @($outputDirectory))) {
                $executed.Add($stage)
                Write-ModStageRecord $stagePath $stageKey @($outputDirectory) $null
            }
        }
    }
    Assert-Cache ($executed.Count -eq 3 -and @($executed | Select-Object -Unique).Count -eq 3) 'Every stage executes once, then reuses only itself'
    foreach ($status in @('Skipped','Fail','Unsupported')) {
        [IO.File]::WriteAllText($recordPath, (@{ Schema=2; Status=$status; Input=$key; Output=(Get-ModContentKey @($outputDirectory)) } | ConvertTo-Json))
        Assert-Cache ($null -eq (Read-ModStageRecord $recordPath $key @($outputDirectory))) 'Only a passing receipt can be reused'
    }
    Assert-Cache (@(Get-ModImpact @('worldsmith-extension/src/Editor.cs') @('worldsmith-extension')).Count -eq 1) 'Editor source must select its checks'
    Assert-Cache (@(Get-ModImpact @('mods/mega-mapping-expansion/api/SceneApi.cs') @('worldsmith-extension')).Count -eq 1) 'Mapping changes must select the editor consumer'
    $mods = @('jk-runtime','morph-ball','replays','mega-mapping-expansion','stereo-madness')
    Assert-Cache ((@(Get-ModImpact @('mods/mega-mapping-expansion/api/MappingApi.cs') $mods) -join ',') -eq 'mega-mapping-expansion,stereo-madness') 'Mapping API changes must invalidate Stereo'
    $legacyInputs = @(Get-ModStageInputs $testRoot ([pscustomobject]@{ Mod='palace-clock'; Arguments=@{} }) @())
    Assert-Cache ($legacyInputs -notcontains (Join-Path $testRoot 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll')) 'A native companion must not acquire an undeclared Runtime binary prerequisite'
    $stereoInputs = @(Get-ModStageInputs $testRoot ([pscustomobject]@{ Mod='stereo-madness'; Arguments=@{} }) @())
    Assert-Cache ($stereoInputs -contains (Join-Path $testRoot 'build/mega-mapping-expansion/UPLOAD_TO_WORKSHOP/MegaMappingApi.dll')) 'Stereo cache must fingerprint its Mapping API dependency'
    $extraInputScript = Join-Path $testRoot 'mods/fixture/build-support/check-inputs.ps1'
    New-Item -ItemType Directory -Path (Split-Path -Parent $extraInputScript) -Force | Out-Null
    [IO.File]::WriteAllText($extraInputScript, 'param([string]$RepoRoot) Join-Path $RepoRoot "source"')
    $fixtureInputs = @(Get-ModStageInputs $testRoot ([pscustomobject]@{ Mod='fixture'; Arguments=@{} }) @())
    Assert-Cache ($fixtureInputs -contains $inputDirectory) 'Mod-owned cache inputs must be included'
    Assert-Cache (@(Get-ModImpact @('unrelated/map.json','editor/tests/test_map.py') $mods).Count -eq 0) 'Unrelated work must not trigger mod builds'
    Assert-Cache ((@(Get-ModImpact @('mods/replays/src/Recorder.cs','mods/replays/tests/New.cs') $mods) -join ',') -eq 'replays') 'Mod changes should affect only their owner'
    Assert-Cache (@(Get-ModImpact @('mods/jk-runtime/src/Runtime/Host.cs') $mods).Count -eq $mods.Count) 'Runtime changes must invalidate consumers'
    Assert-Cache (@(Get-ModImpact @('scripts/check-mods.ps1') $mods).Count -eq $mods.Count) 'Planner changes must revalidate all consumers'
} finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $parent = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../build/_work')).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolved -Leaf) -notlike 'jk-mod-cache-*') { throw 'Unsafe cache fixture cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
Write-Host '[OK] Mod cache: content/addition/artifact invalidation, corrupt-record recovery, argument identity and change impact'
