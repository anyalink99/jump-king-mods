param(
    [string]$GameDir = 'C:/Program Files (x86)/Steam/steamapps/common/Jump King',
    [string]$MapDir,
    [ValidateSet('Off','Local','Steam')][string]$Connection = 'Local',
    [switch]$RefreshBase,
    [switch]$SynchronizeWorld
)
$ErrorActionPreference = 'Stop'
if (Get-Process JumpKing,MultiplayerExpansion -ErrorAction SilentlyContinue) { throw 'Close the running game before starting another Debug session.' }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$scratch = Join-Path $repo 'build/_work/multiplayer-expansion'
$cache = Join-Path $scratch 'base-debug-path.txt'
. (Join-Path $PSScriptRoot 'stage-lab.ps1')
if (-not $MapDir) {
    if (-not $RefreshBase -and (Test-Path $cache)) { $MapDir = (Get-Content -LiteralPath $cache -Raw).Trim() }
    if (-not $MapDir -or -not (Test-Path "$MapDir/level_settings.xml")) {
        $package = Join-Path $repo 'build/multiplayer-expansion/LAB'
        $multiplayer = Join-Path (Split-Path (Split-Path $GameDir)) 'workshop/content/1061090/3190590114'
        $session = New-LabSession -GameDir $GameDir -MultiplayerDir $multiplayer -BinaryDir $package -SessionRoot "$scratch/base-debug" -ModDirectory @() -Roles @(1)
        $MapDir = Join-Path $session 'client1/Content'
        [IO.File]::WriteAllText("$MapDir/mpex-base-game.txt", 'Base-game Debug content; stage the second client from the installed base game.')
        [IO.File]::WriteAllText($cache, $MapDir)
    }
}
$MapDir = (Resolve-Path -LiteralPath $MapDir).Path
if (Test-Path "$MapDir/mpex-base-game.txt") { Write-BaseDebugMetadata $MapDir }
if (-not (Test-Path "$MapDir/level_settings.xml")) { throw 'Debug map must contain level_settings.xml.' }
$installedLocal = Join-Path $GameDir 'Content/JKMods/MultiplayerExpansion/MultiplayerExpansion.dll'
$installedWorkshop = Join-Path (Split-Path (Split-Path $GameDir)) 'workshop/content/1061090/3816170337/MultiplayerExpansion.dll'
if (-not (Test-Path -LiteralPath $installedLocal) -and -not (Test-Path -LiteralPath $installedWorkshop)) { throw 'Install or subscribe to Multiplayer Expansion first.' }
$info = [Diagnostics.ProcessStartInfo]::new("$GameDir/JumpKing.exe")
$info.WorkingDirectory = $GameDir
$info.UseShellExecute = $false
$info.Arguments = '-debug "' + $MapDir.TrimEnd('\') + '"'
$info.EnvironmentVariables['SteamAppId'] = '1061090'
$info.EnvironmentVariables['SteamGameId'] = '1061090'
$info.EnvironmentVariables['MPEX_NATIVE_TRANSPORT'] = $Connection
$info.EnvironmentVariables['MPEX_WORLD_SYNC'] = if ($SynchronizeWorld) { '1' } else { '0' }
$game = [Diagnostics.Process]::Start($info)
Write-Host "Jump King Debug started (PID $($game.Id)); connection=$Connection"
