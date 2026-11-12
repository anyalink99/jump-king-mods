param(
    [string]$QuestDir = 'C:\Program Files (x86)\Steam\steamapps\common\JUMP KING QUEST',
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$Python = 'python',
    [string]$Manifest = (Join-Path $PSScriptRoot '../content/jkq-import.json')
)
# keep the old entry point, but never rebuild one piece against a stale catalogue
& (Join-Path $PSScriptRoot 'import-jkq.ps1') -QuestDir $QuestDir -GameDir $GameDir -Python $Python -Manifest $Manifest
if (-not $?) { throw 'Quest content import failed' }
