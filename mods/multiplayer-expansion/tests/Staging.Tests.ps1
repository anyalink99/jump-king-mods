$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../stage-lab.ps1')
$labTestRoot = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../../..')) ('build/_work/multiplayer-expansion/tests/' + [Guid]::NewGuid().ToString('N'))
$labTestGame = Join-Path $labTestRoot 'source-game'
$labTestMod = Join-Path $labTestRoot 'source-mod'
$labTestBin = Join-Path $labTestRoot 'binary'
$labTestMap = Join-Path $labTestRoot 'map'
New-Item -ItemType Directory -Force -Path "$labTestRoot/3793086563" | Out-Null
[IO.File]::WriteAllText("$labTestRoot/3793086563/JKRuntime.dll", 'runtime dependency')
foreach ($labTestFolder in @("$labTestGame/Content/Saves", "$labTestGame/Content/ControllerBinds", "$labTestGame/Content/JKMods/unrelated", "$labTestGame/Content/screens", $labTestMod, $labTestBin, $labTestMap)) {
    New-Item -ItemType Directory -Force $labTestFolder | Out-Null
}
foreach ($labTestFile in @("$labTestGame/JumpKing.exe", "$labTestGame/JumpKing.exe.config", "$labTestGame/Steamworks.NET.dll", "$labTestGame/steam_api64.dll", "$labTestMod/JumpKingMultiplayer.dll", "$labTestMod/Newtonsoft.Json.dll", "$labTestBin/MultiplayerExpansion.exe", "$labTestBin/0Harmony.dll", "$labTestGame/Content/screens/1.xnb", "$labTestGame/Content/Saves/player.sav", "$labTestGame/Content/ControllerBinds/keyboard.xml", "$labTestGame/Content/JKMods/unrelated/Other.dll", "$labTestMap/level_settings.xml")) {
    [IO.File]::WriteAllText($labTestFile, 'original')
}
[IO.File]::WriteAllText("$labTestBin/MultiplayerExpansion.exe.config", 'original')
[IO.File]::WriteAllText("$labTestBin/MultiplayerExpansion.dll", 'SDK shell')
[IO.File]::WriteAllText("$labTestMod/run.log.2", 'keep the original log')
[IO.File]::WriteAllText("$labTestMod/preferences.json", 'keep settings')
function Assert-Lab([bool]$Value, [string]$Message) { if (-not $Value) { throw $Message } }
# Workshop users don't have the development folder name
$labTestSessions = Join-Path $labTestRoot 'WorkshopDefault'
$labTestSession = New-LabSession $labTestGame $labTestMod $labTestBin $labTestSessions $labTestMap @() $false $true
Assert-Lab (-not (Test-Path "$labTestSession/client1/Content/Saves/player.sav")) 'Player saves escaped into the test session'
Assert-Lab (-not (Test-Path "$labTestSession/client1/Content/JKMods/unrelated/Other.dll")) 'Unselected mods were staged'
Assert-Lab ((Get-Content "$labTestSession/client2/Content/ControllerBinds/keyboard.xml") -eq 'original') 'Installed bindings were not preserved'
Assert-Lab (Test-Path "$labTestSession/client1/map/level_settings.xml") 'Compiled map was not staged'
Assert-Lab (-not (Test-Path "$labTestSession/client2/WorkshopMods/3190590114/run.log.2")) 'Rotated logs were copied into another session'
Assert-Lab (Test-Path "$labTestMod/run.log.2") 'Original log was removed'
Assert-Lab (Test-Path "$labTestSession/client2/WorkshopMods/3190590114/preferences.json") 'Mod settings were excluded with logs'
[IO.File]::WriteAllText("$labTestSession/client1/Content/screens/1.xnb", 'changed')
[IO.File]::WriteAllText("$labTestSession/client1/WorkshopMods/3190590114/JumpKingMultiplayer.dll", 'changed')
Assert-Lab ((Get-Content "$labTestGame/Content/screens/1.xnb") -eq 'original') 'Staged game assets alias the installed files'
Assert-Lab ((Get-Content "$labTestSession/client2/Content/screens/1.xnb") -eq 'original') 'The two clients share writable assets'
Assert-Lab ((Get-Content "$labTestMod/JumpKingMultiplayer.dll") -eq 'original') 'Staged mod files alias the installed files'
$labTestSecond = New-LabSession $labTestGame $labTestMod $labTestBin $labTestSessions '' @() $true
Assert-Lab ($labTestSecond -ne $labTestSession) 'A new launch reused existing saves'
Assert-Lab (-not (Test-Path "$labTestSecond/client1/Content/screens/1.xnb")) 'Transport probe staged unnecessary game content'
Assert-Lab (Test-Path "$labTestSecond/client1/Content/JKMods/MultiplayerExpansion/MultiplayerExpansion.dll") 'Transport probe lost its SDK implementation'
Assert-Lab (Test-Path "$labTestSecond/client1/WorkshopMods/3793086563/JKRuntime.dll") 'Transport probe lost Runtime'
$labTestAll = New-LabSession $labTestGame $labTestMod $labTestBin $labTestSessions '' @()
Assert-Lab (Test-Path "$labTestAll/client2/Content/JKMods/unrelated/Other.dll") 'The default lab omitted installed local mods'
$labMetadata = [xml](Get-Content "$labTestAll/client1/Content/level_settings.xml")
Assert-Lab ($null -ne $labMetadata.SelectSingleNode('/LevelSettings/Tags')) 'Base Debug mode lacks metadata for installed map mods'
foreach ($ending in @('Main','Second','Third')) {
    Assert-Lab (-not [string]::IsNullOrWhiteSpace($labMetadata.LevelSettings.Ending."${ending}Babe")) 'A Debug ending has no required babe asset'
    Assert-Lab (-not [string]::IsNullOrWhiteSpace($labMetadata.LevelSettings.Ending."${ending}Item".image)) 'A Debug ending has no required item asset'
}
$labTestNative = New-LabSession $labTestGame $labTestMod $labTestBin $labTestSessions '' @() $false $false @(2)
Assert-Lab ((Test-Path "$labTestNative/client2/Content/screens/1.xnb") -and -not (Test-Path "$labTestNative/client1")) 'Native mode must stage only the extra client'
$labRejected = $false
try { New-LabSession $labTestGame $labTestMod $labTestBin $labTestSessions "$labTestRoot/missing-map" @() | Out-Null } catch { $labRejected = $true }
Assert-Lab $labRejected 'An invalid compiled map was accepted'
# the game inherits the caller's environment, including pwsh-only module paths
$labNativeGame = Join-Path $labTestRoot 'native/steamapps/common/Jump King'
Copy-LabTree $labTestGame $labNativeGame
Copy-LabTree $labTestMod (Join-Path $labTestRoot 'native/steamapps/workshop/content/1061090/3190590114')
$labNativeOutput = Join-Path $labTestRoot 'native-session.txt'
$labOld = Join-Path $labTestSessions '20260101-000000-11223344'
New-Item -ItemType Directory -Path "$labOld/client2" | Out-Null
[IO.File]::WriteAllText("$labOld/mpex-session.txt", 'old fixture')
[IO.File]::WriteAllText("$labOld/client2/old.xnb", 'rebuildable')
[IO.File]::WriteAllText("$labOld/client2/save.sav", 'keep')
(Get-Item -LiteralPath $labOld).CreationTimeUtc = [datetime]::UtcNow.AddDays(-2)
$labNativeScript = (Resolve-Path (Join-Path $PSScriptRoot '../stage-native.ps1')).Path
$labNativeStart = New-Object Diagnostics.ProcessStartInfo
$labNativeStart.FileName = "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe"
$labNativeStart.UseShellExecute = $false
$labNativeStart.CreateNoWindow = $true
$labNativeStart.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$labNativeStart.RedirectStandardError = $true
$labNativeStart.RedirectStandardOutput = $true
$labNativeStart.EnvironmentVariables['PSModulePath'] = (Join-Path $labTestRoot 'missing-modules')
$labNativeStart.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -GameDir "{1}" -BinaryDir "{2}" -SessionRoot "{3}" -OutputFile "{4}" -ParentId {5}' -f $labNativeScript,$labNativeGame,$labTestBin,$labTestSessions,$labNativeOutput,$PID
$labNativeProcess = [Diagnostics.Process]::Start($labNativeStart)
$labNativeStdout = $labNativeProcess.StandardOutput.ReadToEnd()
$labNativeStderr = $labNativeProcess.StandardError.ReadToEnd()
$labNativeProcess.WaitForExit()
Assert-Lab ($labNativeProcess.ExitCode -eq 0) "Native staging failed with inherited module paths: $labNativeStderr"
Assert-Lab (-not (Test-Path "$labOld/client2/old.xnb") -and (Test-Path "$labOld/client2/save.sav")) 'Native staging did not trim old resources safely with inherited module paths'
$labNativeResult = [IO.File]::ReadAllText($labNativeOutput).Trim()
$labNativeHashes = Get-Content "$labNativeResult/inputs.json" -Raw | ConvertFrom-Json
Assert-Lab ($labNativeHashes.Count -eq 5 -and $labNativeHashes[0].SHA256.Length -eq 64) 'Native staging did not record input hashes'
$labNativeProcess.Dispose()
$labCancelledOutput = Join-Path $labTestRoot 'cancelled-session.txt'
[IO.File]::WriteAllText((Join-Path $labTestSessions 'cancel'), 'Cancel preparation')
$labNativeStart.Arguments = $labNativeStart.Arguments.Replace($labNativeOutput, $labCancelledOutput)
$labNativeProcess = [Diagnostics.Process]::Start($labNativeStart)
$labNativeStdout = $labNativeProcess.StandardOutput.ReadToEnd()
$labNativeStderr = $labNativeProcess.StandardError.ReadToEnd()
$labNativeProcess.WaitForExit()
Assert-Lab ($labNativeProcess.ExitCode -eq 2 -and -not (Test-Path $labCancelledOutput)) 'Cancelled preparation published a launchable session'
$labNativeProcess.Dispose()
Write-Host '[OK] Lab staging: separate writable copies, preserved bindings, fresh sessions, no personal saves or unselected mods, compiled maps, probe scope and invalid paths'
