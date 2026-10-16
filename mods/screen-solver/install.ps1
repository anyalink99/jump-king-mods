param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$RuntimeWorkshopDir = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\1061090\3793086563'
)
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (Get-Process -Name JumpKing -ErrorAction SilentlyContinue) { throw 'Close Jump King before installing.' }
$taskLocal = [IO.Path]::GetFullPath((Join-Path $GameDir 'Content\JKMods'))
$taskWorkshop = [IO.Path]::GetFullPath($RuntimeWorkshopDir)
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'JumpKing.exe') -PathType Leaf)) { throw 'Jump King executable not found.' }
if (-not (Test-Path -LiteralPath $taskWorkshop -PathType Container)) { throw 'JK Runtime Workshop directory not found.' }
if (Test-Path -LiteralPath (Join-Path $taskLocal 'JKRuntime.dll')) { throw 'A local JK Runtime copy would conflict with the Workshop copy.' }
$taskWorkshopRoot = Split-Path $taskWorkshop
foreach ($taskName in @('JKRuntime.dll','ScreenSolver.dll')) {
    $taskDuplicates = @(Get-ChildItem -LiteralPath $taskWorkshopRoot -Filter $taskName -File -Recurse | Where-Object { $_.FullName -ne (Join-Path $taskWorkshop 'JKRuntime.dll') })
    if ($taskDuplicates.Count) { throw "Conflicting Workshop DLLs: $($taskDuplicates.FullName -join ', ')" }
}
& (Join-Path $taskRepo 'mods\jk-runtime\build.ps1') -GameDir $GameDir
& (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir
if (Get-Process -Name JumpKing -ErrorAction SilentlyContinue) { throw 'Jump King started during the build. Close it and retry.' }
$taskOperations = @(
    @{Source=(Join-Path $taskRepo 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'); Target=(Join-Path $taskWorkshop 'JKRuntime.dll')},
    @{Source=(Join-Path $taskRepo 'build\screen-solver\UPLOAD_TO_WORKSHOP\ScreenSolver.dll'); Target=(Join-Path $taskLocal 'ScreenSolver.dll')}
)
New-Item -ItemType Directory -Force -Path $taskLocal | Out-Null
$taskBackup = Join-Path $taskRepo ('build\screen-solver\install-backup-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskBackup | Out-Null
$taskChanged = @()
try {
    foreach ($taskOperation in $taskOperations) {
        $taskOperation.Existed = Test-Path -LiteralPath $taskOperation.Target -PathType Leaf
        $taskOperation.Backup = Join-Path $taskBackup (Split-Path $taskOperation.Target -Leaf)
        if ($taskOperation.Existed) { Copy-Item -LiteralPath $taskOperation.Target -Destination $taskOperation.Backup }
        $taskChanged += $taskOperation
        Copy-Item -LiteralPath $taskOperation.Source -Destination $taskOperation.Target -Force
        if ((Get-FileHash -LiteralPath $taskOperation.Source).Hash -ne (Get-FileHash -LiteralPath $taskOperation.Target).Hash) { throw "Installed hash mismatch: $($taskOperation.Target)" }
    }
}
catch {
    $taskFailure = $_; $taskRollbackErrors = @()
    foreach ($taskOperation in $taskChanged) {
        try {
            if ($taskOperation.Existed) { Copy-Item -LiteralPath $taskOperation.Backup -Destination $taskOperation.Target -Force }
            elseif (Test-Path -LiteralPath $taskOperation.Target) { Remove-Item -LiteralPath $taskOperation.Target }
        } catch { $taskRollbackErrors += $_.ToString() }
    }
    throw "Installation failed: $taskFailure. Rollback errors: $($taskRollbackErrors -join '; '). Backups: $taskBackup"
}
$taskOperations | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskBackup 'manifest.json') -Encoding UTF8
Write-Host '[OK] JK Runtime installed in Workshop; Screen Solver installed locally.'
Write-Host "[OK] Previous DLLs backed up to $taskBackup"
