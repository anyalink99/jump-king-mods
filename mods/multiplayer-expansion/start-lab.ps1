param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$MultiplayerDir,
    [string]$MapDir,
    [string[]]$ModDirectory,
    [string]$SessionRoot,
    [switch]$Probe,
    [switch]$Smoke,
    [switch]$StageOnly,
    [switch]$MultiplayerOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'stage-lab.ps1')
$labBinary = $PSScriptRoot
if (-not (Test-Path "$labBinary/MultiplayerExpansion.exe")) {
    & (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir -MultiplayerDir $MultiplayerDir
    $labBinary = (Resolve-Path (Join-Path $PSScriptRoot '../../build/multiplayer-expansion/LAB')).Path
}
if (-not $SessionRoot) {
    $labRepo = if (Test-Path (Join-Path $PSScriptRoot '../../AGENTS.md')) { (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path } else { (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path }
    $SessionRoot = Join-Path $labRepo 'build/_work/multiplayer-expansion/sessions'
}
if (-not $MultiplayerDir) { $MultiplayerDir = Join-Path (Split-Path (Split-Path $GameDir)) 'workshop/content/1061090/3190590114' }
$labSession = New-LabSession $GameDir $MultiplayerDir $labBinary $SessionRoot $MapDir $ModDirectory ([bool]$Probe) ([bool]$MultiplayerOnly)
Write-Host "Session: $labSession"
if ($StageOnly) { return $labSession }
$labToken = [Guid]::NewGuid().ToString('N')
function New-LabStartInfo([string]$Path, [string]$WorkingDirectory) {
    $labInfo = [Diagnostics.ProcessStartInfo]::new($Path)
    $labInfo.WorkingDirectory = $WorkingDirectory
    $labInfo.UseShellExecute = $false
    $labInfo.CreateNoWindow = $true
    $labInfo.EnvironmentVariables['MPEX_SESSION'] = $labSession
    $labInfo.EnvironmentVariables['MPEX_TOKEN'] = $labToken
    $labInfo.EnvironmentVariables['SteamAppId'] = '1061090'
    $labInfo.EnvironmentVariables['SteamGameId'] = '1061090'
    if ($Smoke) { $labInfo.EnvironmentVariables['MPEX_SMOKE'] = '1' }
    return $labInfo
}
if ($Probe) {
    $labProcesses = @()
    try {
        foreach ($labRole in @(1,2)) {
            $labFolder = Join-Path $labSession "client$labRole"
            $labInfo = New-LabStartInfo "$labFolder/MultiplayerExpansion.exe" $labFolder
            $labInfo.EnvironmentVariables['MPEX_ROLE'] = "$labRole"
            $labInfo.EnvironmentVariables['MPEX_PROBE'] = '1'
            $labProcesses += [Diagnostics.Process]::Start($labInfo)
        }
        foreach ($labProcess in $labProcesses) {
            if (-not $labProcess.WaitForExit(40000)) { throw "Steam probe timed out. Logs: $labSession" }
            if ($labProcess.ExitCode -ne 0) { throw "Steam probe failed. Logs: $labSession" }
        }
        Get-Content "$labSession/client1.probe.txt","$labSession/client2.probe.txt"
    } finally {
        foreach ($labProcess in $labProcesses) { if (-not $labProcess.HasExited) { $labProcess.Kill() }; $labProcess.Dispose() }
    }
} else {
    $labInfo = New-LabStartInfo "$labSession/MultiplayerExpansion.exe" $labSession
    $labHostProcess = [Diagnostics.Process]::Start($labInfo)
    Write-Host "Multiplayer Expansion running (PID $($labHostProcess.Id)). Close its window to stop both clients."
    $labHostProcess.Dispose()
}
