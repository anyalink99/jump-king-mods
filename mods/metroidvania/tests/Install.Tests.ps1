param([Parameter(Mandatory=$true)][string]$Scratch)
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$Scratch = [IO.Path]::GetFullPath($Scratch)
if (-not $Scratch.StartsWith((Join-Path $taskRepo 'build/_work')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Installer fixtures must stay under build/_work.' }
$taskInstaller = Join-Path $PSScriptRoot '../install.ps1'
$taskRuntime = Join-Path $taskRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$taskPackage = Join-Path $taskRepo 'build/metroidvania/UPLOAD_TO_WORKSHOP/Metroidvania.dll'
$taskFixture = Join-Path $Scratch 'install'
$taskGame = Join-Path $taskFixture 'steamapps/common/Jump King'
$taskMods = Join-Path $taskGame 'Content/JKMods'
$taskWorkshop = Join-Path $taskFixture 'steamapps/workshop/content/1061090'
$taskHistory = Join-Path $taskFixture 'history'
New-Item -ItemType Directory -Force $taskMods,(Join-Path $taskWorkshop '3793086563') | Out-Null
Copy-Item -LiteralPath $taskRuntime -Destination (Join-Path $taskWorkshop '3793086563/JKRuntime.dll')
function Assert-Install([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
& $taskInstaller -GameDir $taskGame -HistoryDirectory $taskHistory
$taskTarget = Join-Path $taskMods 'Metroidvania/Metroidvania.dll'
Assert-Install ((Get-FileHash -LiteralPath $taskTarget).Hash -eq (Get-FileHash -LiteralPath $taskPackage).Hash) 'First install must use the local mod folder'
$taskSettings = Join-Path (Split-Path $taskTarget) 'Metroidvania.Settings.xml'
$taskProfile = Join-Path (Split-Path $taskTarget) 'Metroidvania.Character.xml'
[IO.File]::WriteAllText($taskSettings,'personal settings')
[IO.File]::WriteAllText($taskProfile,'personal character')
[IO.File]::WriteAllText($taskTarget,'old package')
& $taskInstaller -GameDir $taskGame -HistoryDirectory $taskHistory
Assert-Install (([IO.File]::ReadAllText($taskSettings) -eq 'personal settings') -and ([IO.File]::ReadAllText($taskProfile) -eq 'personal character')) 'Updating must preserve settings and character'
$taskRollback = @(Get-ChildItem -LiteralPath $taskHistory -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'content/Metroidvania.dll') })
Assert-Install ($taskRollback.Count -eq 1 -and [IO.File]::ReadAllText((Join-Path $taskRollback[0].FullName 'content/Metroidvania.Character.xml')) -eq 'personal character') 'Rollback must include the whole previous character'
$taskSteamFolder = Join-Path $taskWorkshop 'development-item'
New-Item -ItemType Directory -Force $taskSteamFolder | Out-Null
Copy-Item -LiteralPath $taskPackage -Destination (Join-Path $taskSteamFolder 'Metroidvania.dll')
$taskRejected = $false
try { & $taskInstaller -GameDir $taskGame -HistoryDirectory $taskHistory } catch { $taskRejected = $true }
Assert-Install $taskRejected 'Duplicate active packages must be rejected'
# remove only the generated fixture DLL, then exercise an existing Workshop installation
Remove-Item -LiteralPath $taskTarget
[IO.File]::WriteAllText((Join-Path $taskSteamFolder 'Metroidvania.dll'),'old workshop package')
& $taskInstaller -GameDir $taskGame -HistoryDirectory $taskHistory
Assert-Install (-not (Test-Path -LiteralPath $taskTarget)) 'Workshop updates must not add a local duplicate'
Assert-Install ((Get-FileHash -LiteralPath (Join-Path $taskSteamFolder 'Metroidvania.dll')).Hash -eq (Get-FileHash -LiteralPath $taskPackage).Hash) 'Existing Workshop item must receive the release'
Write-Host '[OK] Metroidvania installer: local/Workshop selection, duplicate rejection and character-preserving rollback'
