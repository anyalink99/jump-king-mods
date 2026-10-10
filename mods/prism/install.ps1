param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
if (Get-Process JumpKing -ErrorAction SilentlyContinue) { throw 'Close Jump King before installing Prism.' }
$prismRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$prismPackage = Join-Path $prismRepo 'build/prism/UPLOAD_TO_WORKSHOP'
$prismTheme = Join-Path $prismRepo 'build/prism/LOCAL_THEME/event-horizon'
foreach ($file in @("$prismPackage/Prism.dll", "$prismPackage/0Harmony.dll", "$prismTheme/music.wav", "$prismTheme/score.osu")) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Build Prism and import the local theme first: $file" }
}
$prismLocal = Join-Path $GameDir 'Content/JKMods'
$prismWorkshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop/content/1061090'
$prismRoots = @($prismLocal,$prismWorkshop) | Where-Object { Test-Path -LiteralPath $_ }
$prismInstalled = @($prismRoots | ForEach-Object { Get-ChildItem -LiteralPath $_ -Recurse -File -Filter Prism.dll })
if ($prismInstalled.Count -gt 1) { throw 'Duplicate Prism installations must be resolved before installing.' }
$prismRuntimes = @($prismRoots | ForEach-Object { Get-ChildItem -LiteralPath $_ -Recurse -File -Filter JKRuntime.dll })
if ($prismRuntimes.Count -ne 1) { throw 'Install exactly one JK Runtime 1.37+ first.' }
$prismVersion = [Version](Get-Item -LiteralPath $prismRuntimes[0].FullName).VersionInfo.FileVersion
if ($prismVersion.Major -ne 1 -or $prismVersion.Minor -lt 37) { throw "Prism needs JK Runtime 1.37+; installed $prismVersion" }
$prismDestination = if ($prismInstalled.Count -eq 1) { $prismInstalled[0].DirectoryName } else { Join-Path $prismLocal 'Prism' }
$prismDestination = [IO.Path]::GetFullPath($prismDestination)
if (-not @($prismLocal,$prismWorkshop | Where-Object { $prismDestination.StartsWith([IO.Path]::GetFullPath($_).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) })) { throw 'Destination escaped the game mod roots' }
$prismFiles = @()
foreach ($file in Get-ChildItem -LiteralPath $prismPackage -File) { $prismFiles += @{ Source=$file.FullName; Target=(Join-Path $prismDestination $file.Name) } }
foreach ($name in @('music.wav','score.osu')) { $prismFiles += @{ Source=(Join-Path $prismTheme $name); Target=(Join-Path $prismDestination "themes/event-horizon/$name") } }
$prismBackup = Join-Path $prismRepo ('build/_work/prism/install/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $prismBackup | Out-Null
$prismChanges = @()
try {
    foreach ($operation in $prismFiles) {
        if (Get-Process JumpKing -ErrorAction SilentlyContinue) { throw 'Jump King started during installation.' }
        $operation.Hash = (Get-FileHash -LiteralPath $operation.Source).Hash
        if (Test-Path -LiteralPath $operation.Target) {
            if ((Get-FileHash -LiteralPath $operation.Target).Hash -eq $operation.Hash) { continue }
            if ([IO.Path]::GetFileName($operation.Target) -eq '0Harmony.dll') { throw 'Existing Harmony differs; refusing to replace a shared engine.' }
            $operation.Backup = Join-Path $prismBackup ($prismChanges.Count.ToString() + '-' + [IO.Path]::GetFileName($operation.Target))
            Copy-Item -LiteralPath $operation.Target -Destination $operation.Backup
        }
        $prismChanges += $operation
        New-Item -ItemType Directory -Force (Split-Path $operation.Target -Parent) | Out-Null
        Copy-Item -LiteralPath $operation.Source -Destination $operation.Target -Force
        if ((Get-FileHash -LiteralPath $operation.Target).Hash -ne $operation.Hash) { throw "Copy verification failed: $($operation.Target)" }
    }
} catch {
    foreach ($operation in $prismChanges) {
        if ($operation.Backup) { Copy-Item -LiteralPath $operation.Backup -Destination $operation.Target -Force }
        elseif (Test-Path -LiteralPath $operation.Target) { Remove-Item -LiteralPath $operation.Target }
    }
    throw
}
$prismFiles | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $prismBackup 'receipt.json') -Encoding UTF8
Write-Host "[OK] Installed Prism and local Event Horizon theme: $prismDestination"
