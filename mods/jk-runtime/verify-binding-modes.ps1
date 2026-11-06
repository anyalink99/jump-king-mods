param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskWorkshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop/content/1061090'
$taskProviders = @('3779149753/Sprinting.dll', '3161216998/JumpKingSaveStates.dll', '3214349391/JumpKing-Expansion-Blocks.dll', '3410235901/UpSideDownCore.dll') |
    ForEach-Object { Join-Path $taskWorkshop $_ }
if (@($taskProviders | Where-Object { -not (Test-Path -LiteralPath $_) }).Count) {
    Write-Host '[SKIP] Installed binding modes need Sprinting, SaveStates, Expansion Blocks and UpsideDownCore'; return
}
$taskOutput = Join-Path $taskRepo 'build/_work/binding-modes/check'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskExe = Join-Path $taskOutput 'InstalledBindingModeTests.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /platform:x64 "/out:$taskExe" (Join-Path $PSScriptRoot 'tests/InstalledBindingModeTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installed binding mode tests failed to compile' }
& $taskExe (Join-Path $taskRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll') $GameDir (Join-Path $taskRepo 'mods/subframe-charge/lib/0Harmony.dll') @taskProviders
if ($LASTEXITCODE -ne 0) { throw 'Installed binding modes compatibility failed' }
