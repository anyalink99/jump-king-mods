param(
    [string]$GameDir = 'C:/Program Files (x86)/Steam/steamapps/common/Jump King',
    [string]$Candidate
)
$ErrorActionPreference = 'Stop'
if (Get-Process JumpKing,MultiplayerExpansion -ErrorAction SilentlyContinue) { throw 'Close Jump King and the test clients before installing.' }
$arguments = @{ GameDir=$GameDir; Include=@('multiplayer-expansion'); OnlyRequested=$true }
if ($Candidate) { $arguments.Candidate=$Candidate }
& (Join-Path $PSScriptRoot '../jk-runtime/install-release.ps1') @arguments
