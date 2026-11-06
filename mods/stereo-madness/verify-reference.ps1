param([string]$ReferencePlayer = '', [string]$Node = 'C:\Program Files\nodejs\node.exe')
$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $ReferencePlayer) { $ReferencePlayer = Join-Path $RepoRoot 'build/stereo-madness/_INTERNAL/research/src_game_player_Player.js' }
if (-not (Test-Path -LiteralPath $ReferencePlayer)) { throw 'Pass the preserved reference Player.js with -ReferencePlayer.' }
$Internal = Join-Path $RepoRoot 'build/stereo-madness-mod/_INTERNAL'
$Test = Join-Path $Internal 'TraceTests.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /langversion:5 /reference:System.Xml.Linq.dll /reference:System.Web.Extensions.dll /reference:Microsoft.CSharp.dll "/out:$Test" (Join-Path $PSScriptRoot 'src/Simulation.cs') (Join-Path $PSScriptRoot 'tests/TraceTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Trace fixture compilation failed' }
$rng = [Random]::new(1729)
foreach ($scenario in @('cube-held','cube-taps','ship','solid','hazard','stairs')) {
    $objects = @()
    if ($scenario -eq 'solid') { $objects = @(@{type='solid';x=500;y=30;w=60;h=60}) }
    if ($scenario -eq 'hazard') { $objects = @(@{type='hazard';x=500;y=12;w=10;h=12}) }
    if ($scenario -eq 'stairs') { $objects = @(0..12 | ForEach-Object { @{type='solid';x=500+240*$_;y=30+60*$_;w=60;h=60} }) }
    $held = @(0..1199 | ForEach-Object { if ($scenario -eq 'ship') { ($_ % 55) -lt 26 } elseif ($scenario -eq 'cube-taps') { $rng.Next(5) -eq 0 } else { $true } })
    $case = @{x=0;y=$(if($scenario -eq 'ship'){300}else{30});ship=($scenario -eq 'ship');objects=$objects;held=$held}
    $casePath = Join-Path $Internal ($scenario+'.json'); $tracePath = Join-Path $Internal ($scenario+'.reference.json')
    [IO.File]::WriteAllText($casePath, ($case | ConvertTo-Json -Depth 6 -Compress))
    & $Node (Join-Path $PSScriptRoot 'tests/reference-trace.js') $ReferencePlayer $casePath $tracePath
    if ($LASTEXITCODE -ne 0) { throw "Reference execution failed: $scenario" }
    & $Test $casePath $tracePath
    if ($LASTEXITCODE -ne 0) { throw "Reference comparison failed: $scenario" }
}
Get-FileHash -LiteralPath $ReferencePlayer -Algorithm SHA256 | Format-List
