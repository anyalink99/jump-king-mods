# Shared only by an explicit check session. A direct build still validates from
# source. Reuse requires identical input bytes and intact dependency artifacts.
function Get-ModContentKey([string[]]$Paths) {
    $files = @($Paths | ForEach-Object {
        if (Test-Path -LiteralPath $_ -PathType Container) { Get-ChildItem -LiteralPath $_ -File -Recurse }
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

# Only successful Fast stages are reusable across invocations. Integration and
# Full always execute: GPU, audio and Workshop state are not source inputs.
function Read-ModStageRecord([string]$Path, [string]$InputKey, [string[]]$Outputs) {
    try {
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf) -or $Outputs.Count -eq 0) { return $null }
        $record = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        if ($record.Schema -ne 1 -or $record.Input -ne $InputKey) { return $null }
        if ($record.Output -ne (Get-ModContentKey $Outputs)) { return $null }
        return $record
    } catch { return $null }
}
function Write-ModStageRecord([string]$Path, [string]$InputKey, [string[]]$Outputs, $RuntimeSession) {
    if ($Outputs.Count -eq 0) { throw 'Cannot record a stage without package artifacts' }
    $record = @{ Schema=1; Input=$InputKey; Output=(Get-ModContentKey $Outputs); RuntimeSession=$RuntimeSession }
    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $temporary = Join-Path $directory ([Guid]::NewGuid().ToString('N') + '.tmp')
    [IO.File]::WriteAllText($temporary, ($record | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporary -Destination $Path -Force
}
function Get-ModStageArtifacts([string]$Root, [string]$Id) {
    if ($Id -eq 'stereo-madness') { $Id = 'stereo-madness-mod' }
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
    Join-Path $Root "mods/$($Check.Mod)"
    Join-Path $Root 'mods/jk-runtime'
    Join-Path $Root 'scripts'
    Join-Path $Root 'docs/modding/block-registry/catalog.json'
    Join-Path $Root 'mods/subframe-charge/lib/0Harmony.dll'
    if ($Check.Mod -eq 'wardrobe-plus') {
        $compiler = Join-Path $Root 'build/wardrobe-plus/_INTERNAL/effect-tools/sdk/$PROGRAMFILES/MSBuild/MonoGame/v3.0/Tools'
        if (Test-Path -LiteralPath $compiler) { $compiler }
    }
    if ($Check.Mod -ne 'jk-runtime') { Join-Path $Root 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll' }
    if ($Check.Mod -eq 'stereo-madness') { Join-Path $Root 'build/mega-mapping-expansion/UPLOAD_TO_WORKSHOP/MegaMappingApi.dll' }
    foreach ($name in @('ChargeAssembly','ChargePackage','CameraAssembly','CasualPackage','BallAssembly','ItemsAssembly')) {
        if ($Check.Arguments[$name]) { $Check.Arguments[$name] }
    }
    $NativeInputs
}
function Get-ModImpact([string[]]$Paths, [string[]]$Maintained) {
    $affected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $Paths) {
        $normal = $path.Replace('\','/')
        if ($normal -match '^mods/([^/]+)/') {
            $id = $Matches[1]
            if ($id -eq 'jk-runtime' -or $normal -eq 'mods/subframe-charge/lib/0Harmony.dll') { foreach ($mod in $Maintained) { [void]$affected.Add($mod) } }
            elseif ($Maintained -contains $id) {
                [void]$affected.Add($id)
                if ($id -eq 'mega-mapping-expansion' -and $Maintained -contains 'stereo-madness') { [void]$affected.Add('stereo-madness') }
            }
        } elseif ($normal -match '^(scripts/|docs/testing\.md$|docs/modding/block-registry/)') {
            foreach ($mod in $Maintained) { [void]$affected.Add($mod) }
        }
    }
    @($Maintained | Where-Object { $affected.Contains($_) })
}
function Get-ModArgumentsKey($Arguments) {
    $ordered = [ordered]@{}
    foreach ($name in @($Arguments.Keys | Sort-Object)) { $ordered[$name] = $Arguments[$name] }
    $ordered | ConvertTo-Json -Compress
}
