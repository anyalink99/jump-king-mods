param([string]$GameDir)
$ErrorActionPreference = 'Stop'
$godRepo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$godFixture = Join-Path $godRepo ('build/_work/god/installer-' + [Guid]::NewGuid().ToString('N'))
$godGame = Join-Path $godFixture 'steamapps/common/Jump King'
$godLocal = Join-Path $godGame 'Content/JKMods'
$godWorkshop = Join-Path $godFixture 'steamapps/workshop/content/1061090/7777777'
$godBackups = Join-Path $godFixture 'backups'
New-Item -ItemType Directory -Path $godLocal,$godWorkshop -Force | Out-Null
function Get-Process { return @() }
function Start-Process {
    param($FilePath,$ArgumentList,$WindowStyle,[switch]$Wait,[switch]$PassThru,$RedirectStandardOutput,$RedirectStandardError)
    if ($WindowStyle -ne 'Hidden' -or -not $Wait -or -not $PassThru) { throw 'Installer verification must run hidden and wait for completion.' }
    $godCommand = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($ArgumentList[$ArgumentList.Count - 1]))
    if ($godCommand -notmatch "-Mod @\('god'\)") { throw 'God installation must request focused verification.' }
    [IO.File]::WriteAllText($RedirectStandardOutput, 'fixture verification')
    [IO.File]::WriteAllText($RedirectStandardError, '')
    [pscustomobject]@{ ExitCode=0 }
}
function Assert-GodInstall([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }
try {
    [IO.File]::WriteAllText((Join-Path $godWorkshop 'God.dll'), 'previous-god')
    [IO.File]::WriteAllText((Join-Path $godWorkshop 'God.Settings.xml'), 'preserved-user-settings')
    [IO.File]::WriteAllText((Join-Path $godLocal 'Unrelated.dll'), 'unrelated-mod')
    & (Join-Path $godRepo 'mods/jk-runtime/install-release.ps1') -GameDir $godGame -Include god -OnlyRequested -BackupRoot $godBackups
    $godPayload = Join-Path $godRepo 'build/god/UPLOAD_TO_WORKSHOP'
    Assert-GodInstall ((Get-FileHash -LiteralPath (Join-Path $godWorkshop 'God.dll')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $godPayload 'God.dll')).Hash) 'God did not update its existing Workshop folder.'
    Assert-GodInstall (Test-Path -LiteralPath (Join-Path $godWorkshop '0Harmony.dll')) 'God shared engine dependency is missing.'
    Assert-GodInstall (Test-Path -LiteralPath (Join-Path $godWorkshop 'God.THIRD_PARTY_NOTICES.md')) 'God dependency notice is missing.'
    Assert-GodInstall (-not (Test-Path -LiteralPath (Join-Path $godLocal 'God.dll'))) 'God was duplicated in the local mod root.'
    Assert-GodInstall ([IO.File]::ReadAllText((Join-Path $godWorkshop 'God.Settings.xml')) -eq 'preserved-user-settings') 'God installation changed user settings.'
    Assert-GodInstall ([IO.File]::ReadAllText((Join-Path $godLocal 'Unrelated.dll')) -eq 'unrelated-mod') 'God installation changed an unrelated mod.'
    $godOldHash = (Get-FileHash -LiteralPath (Join-Path $godWorkshop 'God.dll')).Hash
    [IO.File]::WriteAllText((Join-Path $godWorkshop '0Harmony.dll'), 'incompatible-shared-engine')
    $godRefused = $false
    try { & (Join-Path $godRepo 'mods/jk-runtime/install-release.ps1') -GameDir $godGame -Include god -OnlyRequested -BackupRoot $godBackups } catch { $godRefused = $_.ToString() -match 'different Harmony' }
    Assert-GodInstall $godRefused 'An incompatible shared engine must reject God installation.'
    Assert-GodInstall ((Get-FileHash -LiteralPath (Join-Path $godWorkshop 'God.dll')).Hash -eq $godOldHash) 'Failed installation changed God.dll.'
    Write-Host '[OK] God installer: Workshop preference, dependencies, settings, no duplicate and shared-engine refusal'
}
finally {
    $godBoundary = [IO.Path]::GetFullPath((Join-Path $godRepo 'build/_work/god')).TrimEnd('\') + '\'
    $godResolved = [IO.Path]::GetFullPath($godFixture)
    if (-not $godResolved.StartsWith($godBoundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Installer fixture escaped its generated test directory.' }
    Remove-Item -LiteralPath $godResolved -Recurse -Force
}
