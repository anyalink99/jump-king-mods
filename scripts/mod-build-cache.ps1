$ModCatalogPath = Join-Path $PSScriptRoot 'components.json'
function Get-ModComponentCatalog {
    $catalog = Get-Content -LiteralPath $ModCatalogPath -Raw | ConvertFrom-Json
    if ($catalog.schema -ne 1) { throw 'Unsupported component catalog schema' }
    $byId = @{}
    foreach ($component in $catalog.components) {
        if ($component.id -notmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$' -or $byId.ContainsKey($component.id)) { throw 'Invalid or duplicate component identity' }
        $byId[$component.id] = $component
    }
    foreach ($component in $catalog.components) {
        foreach ($dependency in $component.dependencies) { if (-not $byId.ContainsKey($dependency)) { throw "Missing component dependency: $dependency" } }
    }
    $ordered = [Collections.Generic.List[object]]::new()
    $done = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    while ($ordered.Count -lt $byId.Count) {
        $progress = $false
        foreach ($component in $catalog.components) {
            if ($done.Contains($component.id) -or @($component.dependencies | Where-Object { -not $done.Contains($_) }).Count) { continue }
            $ordered.Add($component); [void]$done.Add($component.id); $progress = $true
        }
        if (-not $progress) { throw 'Component dependency cycle' }
    }
    $ordered.ToArray()
}
function Get-ModStageKey([string]$Stage, [string[]]$Inputs, [string]$Environment, $Arguments) {
    if ([string]::IsNullOrWhiteSpace($Stage)) { throw 'A stable stage identity is required' }
    'stage-v2|' + $Stage + '|' + (Get-ModContentKey $Inputs) + '|' + $Environment + '|' + (Get-ModArgumentsKey $Arguments)
}
# only share inside an explicit check session, direct builds still validate from source
# reuse needs identical input bytes and intact dependency artifacts
function Get-ModContentKey([string[]]$Paths) {
    $files = @($Paths | ForEach-Object {
        if (Test-Path -LiteralPath $_ -PathType Container) {
            # clean checkouts create interpreter caches during their first verification
            Get-ChildItem -LiteralPath $_ -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](__pycache__|\.pytest_cache|\.ruff_cache)[\\/]' }
        }
        elseif (Test-Path -LiteralPath $_ -PathType Leaf) { Get-Item -LiteralPath $_ }
        else { throw "Missing fingerprint input: $_" }
    } | Sort-Object FullName -Unique)
    $text = [Text.StringBuilder]::new()
    foreach ($file in $files) {
        [void]$text.Append($file.FullName).Append('|').Append((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash).Append("`n")
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text.ToString()))) }
    finally { $sha.Dispose() }
}

# reuse successful Fast stages only
# Integration and Full stages always run; a dependency's Fast stage may still be reused
function Read-ModStageRecord([string]$Path, [string]$InputKey, [string[]]$Outputs) {
    try {
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf) -or $Outputs.Count -eq 0) { return $null }
        $record = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        if ($record.Schema -ne 2 -or $record.Status -ne 'Pass' -or $record.Input -ne $InputKey) { return $null }
        if ($record.Output -ne (Get-ModContentKey $Outputs)) { return $null }
        return $record
    } catch { return $null }
}
function Write-ModStageRecord([string]$Path, [string]$InputKey, [string[]]$Outputs, $RuntimeSession) {
    if ($Outputs.Count -eq 0) { throw 'Cannot record a stage without package artifacts' }
    $record = @{ Schema=2; Status='Pass'; Input=$InputKey; Output=(Get-ModContentKey $Outputs); RuntimeSession=$RuntimeSession }
    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $temporary = Join-Path $directory ([Guid]::NewGuid().ToString('N') + '.tmp')
    [IO.File]::WriteAllText($temporary, ($record | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporary -Destination $Path -Force
}
function Get-ModStageArtifacts([string]$Root, [string]$Id) {
    if ($Id -eq 'stereo-madness') { $Id = 'stereo-madness-mod' }
    if ($Id -eq 'total-eclipse') { $Id = 'total-eclipse-mod' }
    $directory = Join-Path $Root "build/$Id"
    foreach ($name in @('UPLOAD_TO_WORKSHOP','SDK','AUTHORING_KIT')) {
        $path = Join-Path $directory $name
        if (Test-Path -LiteralPath $path -PathType Container) { $path }
    }
    $internal = Join-Path $directory '_INTERNAL'
    if (Test-Path -LiteralPath $internal) {
        Get-ChildItem -LiteralPath $internal -File | Where-Object { $_.Extension -in @('.dll','.exe') } | ForEach-Object FullName
    }
    if ($Id -eq 'more-items') { Join-Path $directory 'API_TEST' }
}
function Get-ModStageInputs([string]$Root, $Check, [string[]]$NativeInputs) {
    $component = Get-ModComponentCatalog | Where-Object id -eq $Check.Mod
    if ($component) { foreach ($inputPath in $component.inputs) { Join-Path $Root $inputPath } }
    else { Join-Path $Root "mods/$($Check.Mod)" }
    foreach ($dependency in $component.dependencies) {
        $owner = Get-ModComponentCatalog | Where-Object id -eq $dependency
        foreach ($inputPath in $owner.inputs) { Join-Path $Root $inputPath }
    }
    Join-Path $Root 'mods/jk-runtime'
    Join-Path $Root 'scripts'
    Join-Path $Root 'docs/modding/block-registry/catalog.json'
    Join-Path $Root 'mods/subframe-charge/lib/0Harmony.dll'
    if ($Check.Mod -eq 'wardrobe-plus') {
        $compiler = Join-Path $Root 'build/wardrobe-plus/_INTERNAL/effect-tools/sdk/$PROGRAMFILES/MSBuild/MonoGame/v3.0/Tools'
        if (Test-Path -LiteralPath $compiler) { $compiler }
    }
    if ($component.dependencies -contains 'jk-runtime') { Join-Path $Root 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll' }
    if ($Check.Mod -eq 'stereo-madness') { Join-Path $Root 'build/mega-mapping-expansion/UPLOAD_TO_WORKSHOP/MegaMappingApi.dll' }
    $extraInputs = Join-Path $Root "mods/$($Check.Mod)/build-support/check-inputs.ps1"
    if (Test-Path -LiteralPath $extraInputs -PathType Leaf) { & $extraInputs -RepoRoot $Root }
    foreach ($name in @('ChargeAssembly','ChargePackage','CameraAssembly','CasualPackage','BallAssembly','ItemsAssembly','ReplayAssembly')) {
        if ($Check.Arguments[$name]) { $Check.Arguments[$name] }
    }
    $NativeInputs
}
function Get-ModImpact([string[]]$Paths, [string[]]$Maintained) {
    $affected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $catalog = @(Get-ModComponentCatalog)
    foreach ($path in $Paths) {
        $normal = $path.Replace('\','/')
        if ($normal -match '^(scripts/|docs/testing\.md$|docs/modding/block-registry/)' -or $normal -eq 'mods/subframe-charge/lib/0Harmony.dll') {
            foreach ($mod in $Maintained) { [void]$affected.Add($mod) }
        }
        foreach ($component in $catalog) {
            foreach ($inputPath in $component.inputs) {
                if ($normal -eq $inputPath -or $normal.StartsWith($inputPath.TrimEnd('/') + '/', [StringComparison]::OrdinalIgnoreCase)) { [void]$affected.Add($component.id) }
            }
        }
    }
    $expanded = $true
    while ($expanded) {
        $expanded = $false
        foreach ($component in $catalog) {
            if (@($component.dependencies | Where-Object { $affected.Contains($_) }).Count -and $affected.Add($component.id)) { $expanded = $true }
        }
    }
    @($Maintained | Where-Object { $affected.Contains($_) })
}
function Get-ModArgumentsKey($Arguments) {
    $ordered = [ordered]@{}
    foreach ($name in @($Arguments.Keys | Sort-Object)) { $ordered[$name] = $Arguments[$name] }
    $ordered | ConvertTo-Json -Compress
}
