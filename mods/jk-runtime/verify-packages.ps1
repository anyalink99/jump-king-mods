param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$taskInternal = Join-Path $taskRepo 'build\jk-runtime\_INTERNAL\PACKAGE_TESTS'
New-Item -ItemType Directory -Force -Path $taskInternal | Out-Null
$taskGame = Join-Path $GameDir 'JumpKing.exe'
$taskMono = Join-Path $GameDir 'MonoGame.Framework.dll'
$taskLanguage = Join-Path $GameDir 'LanguageJK.dll'
Copy-Item -LiteralPath $taskGame,$taskMono,$taskLanguage -Destination $taskInternal -Force
Copy-Item -LiteralPath (Join-Path $GameDir 'Steamworks.NET.dll') -Destination $taskInternal -Force
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $taskInternal -Force
$taskExe = Join-Path $taskInternal 'PackageTests.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /langversion:5 "/out:$taskExe" "/reference:$taskGame" (Join-Path $PSScriptRoot 'tests\PackageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Package tests compilation failed' }
$taskRuntime = Join-Path $taskRepo 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
$taskPackages = @('casual-jumping\UPLOAD_TO_WORKSHOP\CasualJumping.dll','subframe-charge\UPLOAD_TO_WORKSHOP\SubframeCharge.dll',
    'morph-ball\UPLOAD_TO_WORKSHOP\MorphBall.dll','more-items\UPLOAD_TO_WORKSHOP\MoreItems.dll','replays\UPLOAD_TO_WORKSHOP\Replays.dll',
    'wardrobe-plus\UPLOAD_TO_WORKSHOP\WardrobePlus.dll','smooth-camera\UPLOAD_TO_WORKSHOP\SmoothCamera.dll',
    'mega-mapping-expansion\UPLOAD_TO_WORKSHOP\MegaMappingExpansion.dll','mega-gameplay-expansion\UPLOAD_TO_WORKSHOP\MegaGameplayExpansion.dll',
    'more-items\API_TEST\MoreItemsExample.dll') | ForEach-Object { Join-Path (Join-Path $taskRepo 'build') $_ }
foreach ($taskOffset in 0..($taskPackages.Count - 1)) {
    $taskOrder = @(0..($taskPackages.Count - 1) | ForEach-Object { $taskPackages[($_ + $taskOffset) % $taskPackages.Count] })
    & $taskExe $taskRuntime $taskOrder
    if ($LASTEXITCODE -ne 0) { throw "Native discovery permutation failed: $taskOffset" }
}
[Array]::Reverse($taskPackages)
& $taskExe $taskRuntime $taskPackages
if ($LASTEXITCODE -ne 0) { throw 'Reverse native discovery failed' }
