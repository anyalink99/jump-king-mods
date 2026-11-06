param([Parameter(Mandatory=$true)][string]$Scratch)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../build-support/content-inputs.ps1')
$taskLf = Join-Path $Scratch 'content-inputs/lf'
$taskCrlf = Join-Path $Scratch 'content-inputs/crlf'
New-Item -ItemType Directory -Force $taskLf,$taskCrlf | Out-Null
foreach ($taskName in @('helper.py','helper.cs','helper.ps1','requirements.txt','recipe.json')) {
    [IO.File]::WriteAllText((Join-Path $taskLf $taskName),"first`nsecond`n",[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $taskCrlf $taskName),"first`r`nsecond`r`n",[Text.UTF8Encoding]::new($true))
}
if ((Get-QuestToolchainHash $taskLf) -ne (Get-QuestToolchainHash $taskCrlf)) { throw 'Git line endings or BOM changed the toolchain fingerprint' }
if ((Get-QuestInputHash (Join-Path $taskLf 'recipe.json')) -ne (Get-QuestInputHash (Join-Path $taskCrlf 'recipe.json'))) { throw 'Git line endings or BOM changed the recipe fingerprint' }
[IO.File]::AppendAllText((Join-Path $taskCrlf 'helper.py'),"changed`n")
if ((Get-QuestToolchainHash $taskLf) -eq (Get-QuestToolchainHash $taskCrlf)) { throw 'Changed importer content failed to invalidate its fingerprint' }
Write-Host '[OK] Content fingerprints: LF/CRLF/BOM parity and changed-tool invalidation'
