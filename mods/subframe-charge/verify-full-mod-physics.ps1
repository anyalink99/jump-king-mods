param([Parameter(Mandatory=$true)][string]$StagedGame, [string]$RuntimeAssembly, [switch]$KeepStagedPackages, [switch]$EdgesOnly)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$stage = (Resolve-Path -LiteralPath $StagedGame).Path
$scratch = [IO.Path]::GetFullPath((Join-Path $repo 'build/_work')).TrimEnd('\') + '\'
if (-not $stage.StartsWith($scratch, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $stage 'WorkshopMods'))) {
    throw 'Use a separate staged game under build/_work with copied WorkshopMods and saves.'
}
foreach ($item in @(
    @{Id='jk-runtime';Dll='JKRuntime.dll'},
    @{Id='subframe-charge';Dll='SubframeCharge.dll'},
    @{Id='mega-gameplay-expansion';Dll='MegaGameplayExpansion.dll'},
    @{Id='morph-ball';Dll='MorphBall.dll'})) {
    $targets = @(Get-ChildItem -LiteralPath (Join-Path $stage 'WorkshopMods') -Filter $item.Dll -Recurse -File)
    if ($targets.Count -ne 1) { throw "Expected one staged $($item.Dll)" }
    if ($KeepStagedPackages -and $item.Id -ne 'jk-runtime') { continue }
    $source = if ($item.Id -eq 'jk-runtime' -and $RuntimeAssembly) { (Resolve-Path -LiteralPath $RuntimeAssembly).Path } else { Join-Path $repo "build/$($item.Id)/UPLOAD_TO_WORKSHOP/$($item.Dll)" }
    Copy-Item -LiteralPath $source -Destination $targets[0].FullName
}
$runner = Join-Path $stage 'FullModPhysicsGame.exe'
$references = @('JumpKing.exe','MonoGame.Framework.dll','Steamworks.NET.dll') | ForEach-Object { '/reference:' + (Join-Path $stage $_) }
$references += '/reference:' + (Join-Path $PSScriptRoot 'lib/0Harmony.dll')
$references += '/reference:System.Windows.Forms.dll'
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /nowarn:1685 /target:exe /platform:x64 /langversion:5 "/out:$runner" @references (Join-Path $PSScriptRoot 'tests/FullModPhysicsGame.cs') (Join-Path $PSScriptRoot 'tests/FullModEdgeTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Full-mod game runner compilation failed' }
Copy-Item -LiteralPath (Join-Path $stage 'JumpKing.exe.config') -Destination ($runner + '.config')
$oldEdges=$env:JK_PHYSICS_EDGES_ONLY
try {
$env:JK_PHYSICS_EDGES_ONLY=if($EdgesOnly){'1'}else{''}
$process = Start-Process -FilePath $runner -ArgumentList @('-debug', ('"' + (Join-Path $stage 'Content') + '"')) -WorkingDirectory $stage -WindowStyle Hidden -RedirectStandardOutput (Join-Path $stage 'stdout.txt') -RedirectStandardError (Join-Path $stage 'stderr.txt') -PassThru
$deadline = [DateTime]::UtcNow.AddSeconds(180)
while (-not $process.WaitForExit(20000)) {
    if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw 'Staged game timed out; inspect its crashlog and stdout.' }
    Write-Host 'Combined game scenario is running.'
}
Get-Content -LiteralPath (Join-Path $stage 'physics-game.txt')
if ($process.ExitCode -ne 0) { throw "Combined game failed ($($process.ExitCode))" }
} finally { $env:JK_PHYSICS_EDGES_ONLY=$oldEdges }
