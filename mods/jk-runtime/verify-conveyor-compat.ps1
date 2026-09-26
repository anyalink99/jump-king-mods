param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$ChargeAssembly
)
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskInternal = Join-Path $taskRepo 'build/jk-runtime/_INTERNAL'
$taskRuntime = Join-Path $taskRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$taskWorkshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop/content/1061090'
$taskConveyor = Join-Path $taskWorkshop '3330536917/ConveyorBlockMod.dll'
if (-not (Test-Path -LiteralPath $taskConveyor)) { Write-Host '[SKIP] Optional installed conveyor fixture: Workshop item absent'; return }
$taskExe = Join-Path $taskInternal 'ConveyorCompatibilityTests.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /langversion:5 "/out:$taskExe" "/reference:$taskRuntime" "/reference:$(Join-Path $GameDir 'JumpKing.exe')" "/reference:$(Join-Path $GameDir 'MonoGame.Framework.dll')" (Join-Path $PSScriptRoot 'tests/ConveyorCompatibilityTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Conveyor fixture compilation failed' }
$taskSeen = @{}
foreach ($taskFile in (Get-ChildItem -LiteralPath $taskWorkshop -Recurse -File -Filter '0Harmony.dll' | Sort-Object FullName)) {
    $taskVersion = [Reflection.AssemblyName]::GetAssemblyName($taskFile.FullName).Version.ToString()
    if ($taskSeen.ContainsKey($taskVersion)) { continue }
    $taskSeen[$taskVersion] = $true
    $taskLastEngine = $taskFile.FullName
    $taskArgs = @($taskFile.FullName, $taskConveyor)
    if ($ChargeAssembly) { $taskArgs += $ChargeAssembly }
    & $taskExe @taskArgs
    if ($LASTEXITCODE -ne 0) { throw "Conveyor fixture failed: Harmony $taskVersion" }
}
if ($taskSeen.Count -eq 0) { throw 'No installed Harmony for conveyor compatibility verification' }
& $taskExe 'none' $taskConveyor 'refuse'
if ($LASTEXITCODE -ne 0) { throw 'Missing-Harmony conveyor refusal failed' }
# An isolated copy with a trailing byte remains loadable but is not the reviewed
# binary. Never mutate subscribed content to exercise the fingerprint guard.
$taskUnknownDir = Join-Path $taskInternal 'conveyor-unreviewed'
New-Item -ItemType Directory -Force -Path $taskUnknownDir | Out-Null
$taskUnknown = Join-Path $taskUnknownDir 'ConveyorBlockMod.dll'
$taskBytes = [IO.File]::ReadAllBytes($taskConveyor)
[IO.File]::WriteAllBytes($taskUnknown, [byte[]]($taskBytes + [byte]0))
& $taskExe $taskLastEngine $taskUnknown 'refuse'
if ($LASTEXITCODE -ne 0) { throw 'Unknown-build conveyor refusal failed' }
