param(
    [Parameter(Mandatory=$true)][string]$StagedGame,
    [string]$RuntimeAssembly,
    [ValidateSet('on','off','bytes')][string]$Compilation = 'on',
    [ValidateRange(1,5)][int]$Attempts = 5,
    [switch]$FailHarmonyLog
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$stage = (Resolve-Path -LiteralPath $StagedGame).Path
$scratch = [IO.Path]::GetFullPath((Join-Path $repo 'build/_work')).TrimEnd('\') + '\'
if (-not $stage.StartsWith($scratch, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $stage 'WorkshopMods'))) {
    throw 'Use an existing isolated full-mod game under build/_work.'
}
if ($RuntimeAssembly) {
    $targets = @(Get-ChildItem -LiteralPath (Join-Path $stage 'WorkshopMods') -Filter JKRuntime.dll -Recurse -File)
    if ($targets.Count -ne 1) { throw 'Expected one staged Runtime' }
    Copy-Item -LiteralPath $RuntimeAssembly -Destination $targets[0].FullName
}
$runner = Join-Path $stage 'FullModLifecycleGame.exe'
$references = @('JumpKing.exe','MonoGame.Framework.dll','Steamworks.NET.dll','LanguageJK.dll') | ForEach-Object { '/reference:' + (Join-Path $stage $_) }
$references += '/reference:' + (Join-Path $repo 'mods/subframe-charge/lib/0Harmony.dll')
$references += '/reference:System.Windows.Forms.dll'
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /nowarn:1685 /target:exe /platform:x64 /langversion:5 "/out:$runner" @references (Join-Path $PSScriptRoot 'tests/FullModLifecycleGame.cs')
if ($LASTEXITCODE) { throw 'Lifecycle runner compilation failed' }
Copy-Item -LiteralPath (Join-Path $stage 'JumpKing.exe.config') -Destination ($runner + '.config')
$previousProfile = $env:JK_LIFECYCLE_PROFILE
$previousAttempts = $env:JK_LIFECYCLE_ATTEMPTS
$previousLogFailure = $env:JK_LIFECYCLE_FAIL_HARMONY_LOG
try {
    $env:JK_LIFECYCLE_PROFILE = $Compilation
    $env:JK_LIFECYCLE_ATTEMPTS = [string]$Attempts
    $env:JK_LIFECYCLE_FAIL_HARMONY_LOG = if ($FailHarmonyLog) { '1' } else { $null }
    $process = Start-Process -FilePath $runner -WorkingDirectory $stage -WindowStyle Hidden -RedirectStandardOutput (Join-Path $stage 'lifecycle-stdout.txt') -RedirectStandardError (Join-Path $stage 'lifecycle-stderr.txt') -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(210)
    while (-not $process.WaitForExit(20000)) {
        if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw 'Lifecycle game timed out' }
        Write-Host 'Full-mod lifecycle scenario is running.'
    }
    Get-Content -LiteralPath (Join-Path $stage 'lifecycle-game.txt')
    if ($process.ExitCode) { throw "Lifecycle game failed ($($process.ExitCode))" }
} finally {
    $env:JK_LIFECYCLE_PROFILE = $previousProfile
    $env:JK_LIFECYCLE_ATTEMPTS = $previousAttempts
    $env:JK_LIFECYCLE_FAIL_HARMONY_LOG = $previousLogFailure
}
