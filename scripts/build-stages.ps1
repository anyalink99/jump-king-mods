# compilation reuse never stands in for a behavior test or a package check
function Enter-ModBuild([string]$Root, [string]$Component) {
    $hash = [Security.Cryptography.SHA256]::Create()
    try { $identity = [BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($Root).ToLowerInvariant() + '|' + $Component))) }
    finally { $hash.Dispose() }
    $mutex = [Threading.Mutex]::new($false, ('Local\JKMods.Build.' + $identity))
    try {
        try { $entered = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $entered = $true }
        if (-not $entered) { throw "Another build owns $Component outputs. Wait for it to finish before retrying." }
        $mutex
    } catch { $mutex.Dispose(); throw }
}
function Invoke-ModCompiler {
    param([string]$Compiler, [Parameter(ValueFromRemainingArguments=$true)][object[]]$CompilerArguments)
    $CompilerArguments = @($CompilerArguments | ForEach-Object { $_ } | ForEach-Object { [string]$_ })
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $output = $null
    $documentation = $null
    $symbols = $null
    $debug = $false
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('compiler-cache-v3')
    $lines.Add((Get-FileHash -LiteralPath $Compiler -Algorithm SHA256).Hash)
    $lines.Add([Environment]::OSVersion.VersionString)
    foreach ($argument in $CompilerArguments) {
        if ($argument -match '^/out:(.+)$') { $output = $Matches[1]; $lines.Add('/out:' + [IO.Path]::GetFileName($output)); continue }
        if ($argument -match '^/pdb:(.+)$') { $symbols = $Matches[1]; $lines.Add('/pdb:' + [IO.Path]::GetFileName($symbols)); continue }
        if ($argument -match '^/debug(:(full|pdbonly)|\+)?$') { $debug = $true }
        if ($argument -match '^/doc:(.+)$') { $documentation = $Matches[1]; $lines.Add('/doc:' + [IO.Path]::GetFileName($documentation)); continue }
        if ($argument -match '^/(reference|r|resource):(.+)$') {
            $kind = $Matches[1]; $parts = $Matches[2].Split(',',2); $path = $parts[0]
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { $path = Join-Path (Split-Path $Compiler) $path }
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                $lines.Add($kind + ':' + [IO.Path]::GetFileName($path) + '|' + (Get-FileHash -LiteralPath $path).Hash + '|' + ($parts | Select-Object -Skip 1))
                continue
            }
        }
        if (Test-Path -LiteralPath $argument -PathType Leaf) { $lines.Add([IO.Path]::GetFullPath($argument) + '|' + (Get-FileHash -LiteralPath $argument).Hash) }
        else { $lines.Add($argument) }
    }
    # implicit Framework references and compiler configuration affect emitted code too
    foreach ($name in @('mscorlib.dll','System.dll','System.Core.dll','csc.rsp','csc.exe.config')) {
        $path = Join-Path (Split-Path $Compiler) $name
        if (Test-Path -LiteralPath $path -PathType Leaf) { $lines.Add($name + '|' + (Get-FileHash -LiteralPath $path).Hash) }
    }
    if (-not $output) { throw 'Cached compiler requires an explicit output path' }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $key = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($lines -join "`n"))).Replace('-','') }
    finally { $sha.Dispose() }
    $root = Split-Path -Parent $PSScriptRoot
    $buildBoundary = [IO.Path]::GetFullPath((Join-Path $root 'build')).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    $fullOutput = [IO.Path]::GetFullPath($output)
    $owner = if ($fullOutput.StartsWith($buildBoundary,[StringComparison]::OrdinalIgnoreCase)) { $fullOutput.Substring($buildBoundary.Length).Split([IO.Path]::DirectorySeparatorChar)[0] } else { 'tools' }
    $stage = Join-Path $root ('build/_work/cache/compiler/' + $owner + '/' + [IO.Path]::GetFileName($output))
    $compilerLease = Enter-ModBuild $root ('compiler-' + $owner + '-' + [IO.Path]::GetFileName($output))
    try {
    $cache = Join-Path $stage $key
    $receipt = Join-Path $cache 'receipt.json'
    if ($debug -and -not $symbols) { $symbols = [IO.Path]::ChangeExtension($output,'.pdb') }
    $targets = @($output) + @(if ($documentation) { $documentation }) + @(if ($debug) { $symbols })
    $valid = $false
    if ($env:JK_COMPILER_REBUILD -ne '1' -and (Test-Path -LiteralPath $receipt)) {
        try {
            $record = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
            $valid = $record.Key -eq $key -and $record.Outputs.Count -eq $targets.Count
            foreach ($entry in $record.Outputs) {
                $file = Join-Path $cache $entry.File
                if (-not (Test-Path -LiteralPath $file -PathType Leaf) -or (Get-FileHash -LiteralPath $file).Hash -ne $entry.Hash) { $valid = $false }
            }
            if ($valid) { for ($i=0; $i -lt $targets.Count; $i++) { Copy-Item -LiteralPath (Join-Path $cache $record.Outputs[$i].File) -Destination $targets[$i] -Force } }
        } catch { $valid = $false }
    }
    if ($valid) { $global:LASTEXITCODE=0; Write-Host ('[COMPILE REUSE] {0}: {1:F3}s' -f [IO.Path]::GetFileName($output),$clock.Elapsed.TotalSeconds); return }
    # source lists can exceed CreateProcess's limit in an exported checkout
    $responseRoot = Join-Path $root 'build/_work/compiler-arguments'
    New-Item -ItemType Directory -Force -Path $responseRoot | Out-Null
    $response = Join-Path $responseRoot ([Guid]::NewGuid().ToString('N') + '.rsp')
    try {
        $quoted = @($CompilerArguments | ForEach-Object {
            '"' + [regex]::Replace([regex]::Replace($_, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
        })
        [IO.File]::WriteAllLines($response, $quoted, [Text.UTF8Encoding]::new($true))
        & $Compiler ('@' + $response)
        $compilerExit = $LASTEXITCODE
    } finally { if (Test-Path -LiteralPath $response) { [IO.File]::Delete($response) } }
    if ($compilerExit -ne 0) { throw "Compiler failed ($compilerExit): $output" }
    New-Item -ItemType Directory -Force -Path $cache | Out-Null
    $outputs = @()
    for ($i=0; $i -lt $targets.Count; $i++) {
        $file = "$i.bin"; Copy-Item -LiteralPath $targets[$i] -Destination (Join-Path $cache $file) -Force
        $outputs += @{ File=$file; Hash=(Get-FileHash -LiteralPath $targets[$i]).Hash }
    }
    [IO.File]::WriteAllText($receipt, (@{ Key=$key; Outputs=$outputs } | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    . (Join-Path $root 'mods/jk-runtime/build-support/build-retention.ps1')
    Remove-BuildArtifactHistory -Directory $stage -NamePattern '^[0-9A-F]{64}$' -Keep 2 -MinimumAgeMinutes 0 -Preserve @($cache) | Out-Null
    $global:LASTEXITCODE=0
    Write-Host ('[COMPILE] {0}: {1:F3}s' -f [IO.Path]::GetFileName($output),$clock.Elapsed.TotalSeconds)
    } finally { $compilerLease.ReleaseMutex(); $compilerLease.Dispose() }
}
