function Get-QuestInputHash([string]$Path) {
    $taskBytes = [Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($Path).Replace("`r`n","`n"))
    $taskHasher = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($taskHasher.ComputeHash($taskBytes)).Replace('-','').ToLowerInvariant() }
    finally { $taskHasher.Dispose() }
}

function Get-QuestToolchainHash([string]$Directory) {
    $taskRecords = @(Get-ChildItem -LiteralPath $Directory -File | Where-Object { $_.Extension -in @('.py','.cs','.ps1','.txt') } | Sort-Object Name | ForEach-Object { $_.Name + ':' + (Get-QuestInputHash $_.FullName) })
    $taskBytes = [Text.Encoding]::UTF8.GetBytes(($taskRecords -join "`n"))
    $taskHasher = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($taskHasher.ComputeHash($taskBytes)).Replace('-','').ToLowerInvariant() }
    finally { $taskHasher.Dispose() }
}
