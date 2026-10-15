param([string]$GameDir, [string]$BinaryDir, [string]$SessionRoot, [string]$OutputFile, [string]$MapDir, [int]$ParentId)
$ErrorActionPreference = 'Stop'
# the game can inherit pwsh's module path, which hides Windows PowerShell's utility module
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop
. (Join-Path $PSScriptRoot 'stage-lab.ps1')
$script:labStageCancel = Join-Path $SessionRoot 'cancel'
$script:labStageParent = $ParentId
$multiplayer = Join-Path (Split-Path (Split-Path $GameDir)) 'workshop/content/1061090/3190590114'
try {
$session = New-LabSession -GameDir $GameDir -MultiplayerDir $multiplayer -BinaryDir $BinaryDir -SessionRoot $SessionRoot -MapDir $MapDir -ModDirectory @() -Roles @(2)
Assert-LabStagingActive -Force
[IO.File]::WriteAllText($OutputFile, $session)
} catch [OperationCanceledException] { Write-Host '[stage] Cancelled'; exit 2 }
