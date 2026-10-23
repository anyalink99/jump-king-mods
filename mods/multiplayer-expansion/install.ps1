param([string]$GameDir = 'C:/Program Files (x86)/Steam/steamapps/common/Jump King')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$package = Join-Path $repo 'build/multiplayer-expansion/LAB'
if (Get-Process JumpKing,MultiplayerExpansion -ErrorAction SilentlyContinue) { throw 'Close Jump King and the test clients before installing.' }
$workshop = Join-Path (Split-Path (Split-Path $GameDir)) 'workshop/content/1061090'
$existing = @(Get-ChildItem -LiteralPath $workshop -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'MultiplayerExpansion.dll') })
if ($existing.Count -gt 1) { throw 'Multiple Multiplayer Expansion Workshop installations found.' }
$target = if ($existing.Count) { $existing[0].FullName } else { Join-Path $GameDir 'Content/JKMods/MultiplayerExpansion' }
New-Item -ItemType Directory -Force $target | Out-Null
$files = @('MultiplayerExpansion.dll','MultiplayerExpansion.exe','MultiplayerExpansion.exe.config','0Harmony.dll','stage-native.ps1','stage-lab.ps1','README.md','CHANGELOG.md','THIRD_PARTY_NOTICES.md','Harmony-LICENSE.txt')
$files += @(Get-ChildItem -LiteralPath (Join-Path $package 'docs') -Recurse -File | Sort-Object FullName | ForEach-Object { $_.FullName.Substring($package.Length + 1) })
$backup = Join-Path $repo ('build/_work/multiplayer-expansion/install-backups/' + (Get-Date -Format yyyyMMdd-HHmmss))
$oldWorldShell = Join-Path $target 'MultiplayerExpansion.World.dll'
if (Test-Path -LiteralPath $oldWorldShell) {
    New-Item -ItemType Directory -Force $backup | Out-Null
    Move-Item -LiteralPath $oldWorldShell -Destination (Join-Path $backup 'MultiplayerExpansion.World.dll')
}
foreach ($name in $files) {
    if (-not (Test-Path "$package/$name")) { throw "Build the package first: missing $name" }
    if (Test-Path "$target/$name") { New-Item -ItemType Directory -Force (Split-Path "$backup/$name") | Out-Null; Copy-Item -LiteralPath "$target/$name" -Destination "$backup/$name" }
    New-Item -ItemType Directory -Force (Split-Path "$target/$name") | Out-Null
    Copy-Item -LiteralPath "$package/$name" -Destination "$target/$name" -Force
}
if (-not (Test-Path "$target/session-root.txt")) { [IO.File]::WriteAllText("$target/session-root.txt", (Join-Path $repo 'build/_work/multiplayer-expansion/sessions')) }
Write-Host "[OK] Installed native Multiplayer Expansion: $target"
