$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../mod-build-cache.ps1')
function Assert-Cache([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('jk-mod-cache-' + [Guid]::NewGuid().ToString('N'))
$inputDirectory = Join-Path $testRoot 'source'
$outputDirectory = Join-Path $testRoot 'package'
New-Item -ItemType Directory -Path $inputDirectory,$outputDirectory | Out-Null
$source = Join-Path $inputDirectory 'module.cs'
$output = Join-Path $outputDirectory 'module.dll'
$recordPath = Join-Path $testRoot 'cache.json'
try {
    [IO.File]::WriteAllText($source, 'aaaa')
    [IO.File]::WriteAllText($output, 'binary')
    $key = Get-ModContentKey @($inputDirectory)
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
    $mods = @('jk-runtime','morph-ball','replays','mega-mapping-expansion','stereo-madness')
    Assert-Cache ((@(Get-ModImpact @('mods/mega-mapping-expansion/api/MappingApi.cs') $mods) -join ',') -eq 'mega-mapping-expansion,stereo-madness') 'Mapping API changes must invalidate Stereo'
    $stereoInputs = @(Get-ModStageInputs $testRoot ([pscustomobject]@{ Mod='stereo-madness'; Arguments=@{} }) @())
    Assert-Cache ($stereoInputs -contains (Join-Path $testRoot 'build/mega-mapping-expansion/UPLOAD_TO_WORKSHOP/MegaMappingApi.dll')) 'Stereo cache must fingerprint its Mapping API dependency'
    Assert-Cache (@(Get-ModImpact @('unrelated/map.json','editor/tests/test_map.py') $mods).Count -eq 0) 'Unrelated work must not trigger mod builds'
    Assert-Cache ((@(Get-ModImpact @('mods/replays/src/Recorder.cs','mods/replays/tests/New.cs') $mods) -join ',') -eq 'replays') 'Mod changes should affect only their owner'
    Assert-Cache (@(Get-ModImpact @('mods/jk-runtime/src/Runtime/Host.cs') $mods).Count -eq $mods.Count) 'Runtime changes must invalidate consumers'
    Assert-Cache (@(Get-ModImpact @('scripts/check-mods.ps1') $mods).Count -eq $mods.Count) 'Planner changes must revalidate all consumers'
} finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolved -Leaf) -notlike 'jk-mod-cache-*') { throw 'Unsafe cache fixture cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
Write-Host '[OK] Mod cache: content/addition/artifact invalidation, corrupt-record recovery, argument identity and change impact'
