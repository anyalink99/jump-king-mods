$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../build-stages.ps1')
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture = Join-Path $repo ('build/_work/compiler-tests/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $fixture | Out-Null
$compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$source = Join-Path $fixture 'Probe.cs'
$output = Join-Path $fixture 'CompilerCacheProbe.exe'
$previousRebuild = $env:JK_COMPILER_REBUILD
try {
    # this suite tests reuse even when its caller is benchmarking fresh compilation
    $env:JK_COMPILER_REBUILD = $null
    [IO.File]::WriteAllText($source,'class Probe { static int Main(){ return 7; } }')
    $arguments = @('/nologo','/target:exe',"/out:$output")
    Invoke-ModCompiler $compiler $arguments @($source)
    $debugArguments = @('/nologo','/debug:pdbonly','/target:exe',"/out:$output")
    Invoke-ModCompiler $compiler $debugArguments @($source)
    $pdb = [IO.Path]::ChangeExtension($output,'.pdb'); $pdbHash = (Get-FileHash -LiteralPath $pdb).Hash
    [IO.File]::Delete($pdb); Invoke-ModCompiler $compiler $debugArguments @($source)
    if ((Get-FileHash -LiteralPath $pdb).Hash -ne $pdbHash) { throw 'Debug symbols were not restored with their executable' }
    Invoke-ModCompiler $compiler $arguments @($source)
    $first = (Get-FileHash -LiteralPath $output).Hash
    [IO.File]::Delete($output)
    Invoke-ModCompiler $compiler $arguments @($source)
    if ((Get-FileHash -LiteralPath $output).Hash -ne $first) { throw 'Compiler reuse changed verified bytes' }
    & $output
    if ($LASTEXITCODE -ne 7) { throw 'Cached executable has wrong behavior' }
    [IO.File]::WriteAllText($source,'class Probe { static int Main(){ return 9; } }')
    Invoke-ModCompiler $compiler $arguments @($source)
    & $output
    if ($LASTEXITCODE -ne 9) { throw 'Changed source reused old compilation' }
    $longArguments = @(1..600 | ForEach-Object { '/define:LONG_COMPILER_ARGUMENT_FOR_EXPORTED_WORKSPACE_PATH_' + $_ })
    if (($longArguments -join ' ').Length -lt 32768) { throw 'Long command fixture is too short' }
    Invoke-ModCompiler $compiler $arguments $longArguments @($source)
    & $output
    if ($LASTEXITCODE -ne 9) { throw 'Response-file compilation changed behavior' }
    [IO.File]::WriteAllText($source,'invalid csharp')
    $refused=$false
    try { Invoke-ModCompiler $compiler $arguments @($source) } catch { $refused=$true }
    if (-not $refused) { throw 'Failed compilation became successful reuse' }
    $global:LASTEXITCODE=0
} finally {
    $env:JK_COMPILER_REBUILD = $previousRebuild
    $parent = [IO.Path]::GetFullPath((Join-Path $repo 'build/_work/compiler-tests')) + [IO.Path]::DirectorySeparatorChar
    if (-not $fixture.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe compiler fixture cleanup' }
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
Write-Host '[OK] Compiler stage: nested arguments, verified-byte reuse, changed inputs and failed compilation'
