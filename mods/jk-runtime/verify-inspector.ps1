param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [switch]$Graphics,
    [switch]$Integration
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$out = Join-Path $repo 'build\_work\inspector-validation'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs = @("/reference:$GameDir\JumpKing.exe", "/reference:$GameDir\MonoGame.Framework.dll", "/reference:$GameDir\LanguageJK.dll",
    '/reference:System.Web.Extensions.dll', '/reference:System.Xml.Linq.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll')
$fixture = Join-Path $PSScriptRoot 'tests\Inspection\fixtures\UnknownProvider.cs'
& $compiler /nologo /target:library "/out:$out\UnknownProvider.dll" $refs $fixture
if ($LASTEXITCODE -ne 0) { throw 'Inspector provider fixture failed' }
$sources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -Recurse | ForEach-Object FullName)
$tests = @(Get-ChildItem (Join-Path $PSScriptRoot 'tests\Inspection') -Filter '*.cs' | ForEach-Object FullName)
$resource = "/resource:$(Join-Path $repo 'docs\modding\block-registry\catalog.json'),JKRuntime.BlockCatalog.json"
& $compiler /nologo /optimize+ /target:exe /langversion:5 /main:JKRuntime.Inspection.Tests "/out:$out\InspectorTests.exe" "/reference:$out\UnknownProvider.dll" $refs $resource $sources $tests
if ($LASTEXITCODE -ne 0) { throw 'Inspector regression compilation failed' }
Get-ChildItem $GameDir -File | Where-Object { $_.Name -in 'JumpKing.exe','MonoGame.Framework.dll','LanguageJK.dll','Steamworks.NET.dll' -or $_.Name -like 'SharpDX*.dll' } | Copy-Item -Destination $out -Force
Copy-Item (Join-Path $repo 'mods\subframe-charge\lib\0Harmony.dll') $out -Force
Push-Location $out
try {
    $mode = if ($Graphics) { 'graphics' } else { 'fast' }
    & .\InspectorTests.exe $GameDir $mode
    if ($LASTEXITCODE -ne 0) { throw 'Inspector regression checks failed' }
    if ($Integration) {
        $workshop = [IO.Path]::GetFullPath((Join-Path $GameDir '..\..\workshop\content\1061090'))
        $fixtures = @{
            '3353090188\ForcedSlopeBlocks.dll' = '5AF16339E2D0E20E49569727A78006FC58E20E156B722313F52CCB73E7844C2D'
            '3470750355\MoreBlockSizes.dll' = '2318F08EDD9D7EA8B039D3271E53C4320CE20280A8897DBF19D4259D009619CF'
        }
        foreach ($relative in $fixtures.Keys) {
            if ((Get-FileHash -LiteralPath (Join-Path $workshop $relative)).Hash -ne $fixtures[$relative]) { throw "Review changed inspector fixture: $relative" }
        }
        foreach ($harmony in @((Join-Path $workshop '3353090188\0Harmony.dll'), (Join-Path $repo 'mods\subframe-charge\lib\0Harmony.dll'))) {
            Copy-Item -LiteralPath $harmony -Destination (Join-Path $out '0Harmony.dll') -Force
            Write-Host "[MATRIX] Harmony $([Reflection.AssemblyName]::GetAssemblyName($harmony).Version)"
            foreach ($order in @('runtime-first', 'slopes-first')) {
                foreach ($loader in @('native', 'sizes', 'mesh')) {
                    & .\InspectorTests.exe $GameDir slopes $order $loader
                    if ($LASTEXITCODE -ne 0) { throw "Inspector compatibility failed: $order / $loader" }
                }
            }
        }
    }
} finally { Pop-Location }
