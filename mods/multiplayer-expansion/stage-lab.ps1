$script:labStageCancel = $null
$script:labStageParent = 0
$script:labStageCheck = [datetime]::MinValue
function Remove-OldLabAssets([string]$Root, [string]$Current, [datetime]$Now = [datetime]::UtcNow) {
    $boundary = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    if (-not (Test-Path -LiteralPath $boundary)) { return }
    for ($ancestor = Get-Item -LiteralPath $boundary; $null -ne $ancestor; $ancestor = $ancestor.Parent) {
        if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked lab history is not safe to trim.' }
    }
    $sessions = @(foreach ($directory in Get-ChildItem -LiteralPath $boundary -Directory) {
        if ($directory.Name -cmatch '^\d{8}-\d{6}-[0-9a-f]{8}$') { $directory }
        elseif ($directory.Name -cmatch '^native-\d{8}-\d{6}-[0-9a-f]{8}$') {
            if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked native session.' }
            Get-ChildItem -LiteralPath $directory.FullName -Directory | Where-Object { $_.Name -cmatch '^\d{8}-\d{6}-[0-9a-f]{8}$' }
        }
    })
    $sessions = @($sessions | Where-Object {
        (Test-Path -LiteralPath (Join-Path $_.FullName 'inputs.json')) -or
        (Test-Path -LiteralPath (Join-Path $_.FullName 'mpex-session.txt'))
    } | Sort-Object CreationTimeUtc, Name -Descending)
    Import-Module (Join-Path $PSHOME 'Modules/CimCmdlets/CimCmdlets.psd1') -ErrorAction Stop
    $processes = @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object ProcessId -ne $PID)
    foreach ($session in @($sessions | Select-Object -Skip 2)) {
        $path = [IO.Path]::GetFullPath($session.FullName)
        $complete = Test-Path -LiteralPath (Join-Path $path 'inputs.json')
        if ($path -eq $Current -or (-not $complete -and $session.CreationTimeUtc -gt $Now.AddHours(-1))) { continue }
        if (-not $path.StartsWith($boundary + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Lab history path escaped its root.' }
        if (@($processes | Where-Object {
            ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($path + '\', [StringComparison]::OrdinalIgnoreCase)) -or
            ($_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($path, [StringComparison]::OrdinalIgnoreCase) -ge 0)
        }).Count) { continue }
        $pending = New-Object 'Collections.Generic.Stack[IO.FileSystemInfo]'
        $pending.Push($session)
        $remove = New-Object 'Collections.Generic.List[string]'
        while ($pending.Count) {
            $entry = $pending.Pop()
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked lab contents.' }
            if ($entry -is [IO.DirectoryInfo]) {
                foreach ($child in $entry.GetFileSystemInfos()) { $pending.Push($child) }
            } else {
                $relative = $entry.FullName.Substring($path.Length + 1)
                if ($relative -match '^client[12]\\' -and $relative -notmatch '(?i)\\[^\\]*(backup|rollback|save|replay|log|research)[^\\]*\\' -and
                    $entry.Extension -in @('.xnb','.dll','.exe','.pdb','.ttf','.wav')) { $remove.Add($entry.FullName) }
            }
        }
        # keep state and diagnostics in place, only trim reproducible client files
        foreach ($file in $remove) { Remove-Item -LiteralPath $file -ErrorAction Stop }
    }
}
function Assert-LabStagingActive([switch]$Force) {
    if (-not $Force -and ([datetime]::UtcNow - $script:labStageCheck).TotalMilliseconds -lt 100) { return }
    $script:labStageCheck = [datetime]::UtcNow
    if ($script:labStageCancel -and (Test-Path -LiteralPath $script:labStageCancel)) { throw [OperationCanceledException]::new('Preparation cancelled.') }
    if ($script:labStageParent -gt 0 -and -not (Get-Process -Id $script:labStageParent -ErrorAction SilentlyContinue)) { throw [OperationCanceledException]::new('Main game closed.') }
}
function Write-LabStage([string]$Name) { Assert-LabStagingActive; Write-Host "[stage] $Name" }
function Copy-LabTree([string]$Source, [string]$Destination, [switch]$SkipLogs) {
    Assert-LabStagingActive
    if (-not (Test-Path -LiteralPath $Source -PathType Container)) { throw "Missing source directory: $Source" }
    if ((Get-Item -LiteralPath $Source).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked source is not safe to stage: $Source" }
    New-Item -ItemType Directory -Force $Destination | Out-Null
    foreach ($labItem in Get-ChildItem -LiteralPath $Source -Force) {
        Assert-LabStagingActive
        if ($labItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked source is not safe to stage: $($labItem.FullName)" }
        $labTarget = Join-Path $Destination $labItem.Name
        if ($SkipLogs -and -not $labItem.PSIsContainer -and $labItem.Name -match '(?i)\.log(?:\.\d+)?$') { continue }
        if ($labItem.PSIsContainer) { Copy-LabTree $labItem.FullName $labTarget -SkipLogs:$SkipLogs }
        else { Copy-Item -LiteralPath $labItem.FullName -Destination $labTarget }
    }
}

function Write-BaseDebugMetadata([string]$Directory) {
    # native Debug treats Content as a custom map and requires explicit ending assets
    [IO.File]::WriteAllText((Join-Path $Directory 'level_settings.xml'), @'
<LevelSettings>
  <About><title>Jump King</title><ending_screen>43</ending_screen><ending_screen_second>100</ending_screen_second><ending_screen_third>154</ending_screen_third></About>
  <Ending>
    <MainBabe>imagecrown</MainBabe><MainItem><item>Shoes</item><image>imageshoes</image></MainItem>
    <SecondBabe>nbp_imagecrown</SecondBabe><SecondItem><item>GiantBoots</item><image>nbp_imageshoes</image></SecondItem>
    <ThirdBabe>owl_imagecrown</ThirdBabe><ThirdItem><item>CapeOwl</item><image>owl_imagebird</image></ThirdItem>
  </Ending>
  <EndingLines />
  <Tags />
</LevelSettings>
'@)
}

function New-LabSession([string]$GameDir, [string]$MultiplayerDir, [string]$BinaryDir, [string]$SessionRoot, [string]$MapDir, [string[]]$ModDirectory, [bool]$TransportOnly = $false, [bool]$MultiplayerOnly = $false, [int[]]$Roles = @(1,2)) {
    Assert-LabStagingActive
    foreach ($labRequired in @("$GameDir/JumpKing.exe", "$GameDir/steam_api64.dll", "$MultiplayerDir/JumpKingMultiplayer.dll", "$BinaryDir/MultiplayerExpansion.exe")) {
        if (-not (Test-Path -LiteralPath $labRequired -PathType Leaf)) { throw "Missing test input: $labRequired" }
    }
    if ($MapDir -and -not (Test-Path -LiteralPath (Join-Path $MapDir 'level_settings.xml'))) { throw 'MapDir must contain a compiled Jump King map (level_settings.xml).' }
    # always stage into a new directory, never overwrite a previous test's saves
    $labSession = Join-Path $SessionRoot ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $labSession -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $labSession 'mpex-session.txt'), 'Multiplayer Expansion isolated test session')
    $labHistory = [IO.Path]::GetFullPath($SessionRoot).TrimEnd('\','/')
    if ((Split-Path $labHistory -Leaf) -cmatch '^native-\d{8}-\d{6}-[0-9a-f]{8}$') { $labHistory = Split-Path $labHistory }
    # Workshop's default root and custom roots need the same retention limit
    try { Remove-OldLabAssets -Root $labHistory -Current $labSession }
    catch { Write-Warning "Old client assets were retained: $_" }
    $labInputs = @()
    foreach ($labRole in $Roles) {
        Write-Host "Preparing isolated client $labRole..."
        Write-LabStage 'Copying game files'
        $labClient = Join-Path $labSession "client$labRole"
        New-Item -ItemType Directory -Path "$labClient/Content/JKMods" -Force | Out-Null
        foreach ($labFile in Get-ChildItem -LiteralPath $GameDir -File | Where-Object { $_.Extension -in '.dll','.exe','.config' -or $_.Name -eq 'language.xml' }) {
            Copy-Item -LiteralPath $labFile.FullName -Destination $labClient
        }
        if (-not $TransportOnly) {
        if (Test-Path "$GameDir/en-US") { Copy-LabTree "$GameDir/en-US" "$labClient/en-US" }
        if (Test-Path "$GameDir/Content/ControllerBinds") { Copy-LabTree "$GameDir/Content/ControllerBinds" "$labClient/Content/ControllerBinds" }
        Write-LabStage 'Copying game content'
        foreach ($labItem in Get-ChildItem -LiteralPath "$GameDir/Content" -Force) {
            if ($labItem.Name -in @('Saves','SavesPerma','ControllerBinds','JKMods','OverlayPlus','Replays','RunVerifier','WardrobePlus')) { continue }
            if ($labItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked game content: $($labItem.FullName)" }
            if ($labItem.PSIsContainer) { Copy-LabTree $labItem.FullName "$labClient/Content/$($labItem.Name)" }
            else { Copy-Item -LiteralPath $labItem.FullName -Destination "$labClient/Content" }
        }
        Copy-LabTree $MultiplayerDir "$labClient/WorkshopMods/3190590114" -SkipLogs
        Write-LabStage 'Copying installed mods'
        if ($MultiplayerOnly) {
            $labRuntime = Join-Path (Split-Path $MultiplayerDir) '3793086563'
            if (-not (Test-Path "$labRuntime/JKRuntime.dll")) { throw 'The minimal test setup requires JK Runtime.' }
            Copy-LabTree $labRuntime "$labClient/WorkshopMods/3793086563" -SkipLogs
            Copy-LabTree $BinaryDir "$labClient/Content/JKMods/MultiplayerExpansion" -SkipLogs
        }
        if (-not $MultiplayerOnly) {
            if (Test-Path "$GameDir/Content/JKMods") { Copy-LabTree "$GameDir/Content/JKMods" "$labClient/Content/JKMods" -SkipLogs }
            $labWorkshop = Join-Path (Split-Path (Split-Path $GameDir)) 'workshop/content/1061090'
            if (Test-Path -LiteralPath $labWorkshop) {
                foreach ($labInstalled in Get-ChildItem -LiteralPath $labWorkshop -Directory) {
                    if ($labInstalled.Name -ne '3190590114' -and @(Get-ChildItem -LiteralPath $labInstalled.FullName -File -Filter '*.dll').Count) {
                        Copy-LabTree $labInstalled.FullName "$labClient/WorkshopMods/$($labInstalled.Name)" -SkipLogs
                    }
                }
            }
        }
        foreach ($labExtra in $ModDirectory) {
            $labName = Split-Path $labExtra -Leaf
            if (Test-Path "$labClient/Content/JKMods/$labName") { throw "Duplicate lab mod folder: $labName" }
            Copy-LabTree $labExtra "$labClient/Content/JKMods/$labName" -SkipLogs
        }
        Write-LabStage 'Preparing test map'
        if ($MapDir -and -not (Test-Path "$MapDir/mpex-base-game.txt")) { Copy-LabTree $MapDir "$labClient/map" }
        else {
            # some map mods read metadata even for the base game in Debug mode
            if (-not (Test-Path "$labClient/Content/level_settings.xml")) {
                Write-BaseDebugMetadata "$labClient/Content"
            }
            if ($MapDir) { [IO.File]::WriteAllText("$labClient/debug-root.txt", 'Content') }
        }
        }
        # the bootstrap resolves these before the native mod loader starts
        if ($TransportOnly) {
            Copy-LabTree $MultiplayerDir "$labClient/WorkshopMods/3190590114"
            Copy-LabTree (Join-Path (Split-Path $MultiplayerDir) '3793086563') "$labClient/WorkshopMods/3793086563" -SkipLogs
        }
        # the client runs the same SDK package, including in transport-only probes
        if (-not (Test-Path "$labClient/WorkshopMods/3816170337/MultiplayerExpansion.dll") -and
            -not (Test-Path "$labClient/Content/JKMods/MultiplayerExpansion/MultiplayerExpansion.dll")) {
            Copy-LabTree $BinaryDir "$labClient/Content/JKMods/MultiplayerExpansion" -SkipLogs
        }
        Copy-Item "$BinaryDir/0Harmony.dll","$BinaryDir/MultiplayerExpansion.exe" $labClient
        foreach ($labHarmony in Get-ChildItem -LiteralPath "$labClient/Content/JKMods","$labClient/WorkshopMods" -Recurse -File -Filter '0Harmony.dll') {
            # one engine per isolated process; leave every installed original untouched
            Copy-Item "$BinaryDir/0Harmony.dll" $labHarmony.FullName -Force
        }
        Copy-Item "$BinaryDir/MultiplayerExpansion.exe.config" "$labClient/MultiplayerExpansion.exe.config"
        [IO.File]::WriteAllText("$labClient/steam_appid.txt", '1061090')
    }
    Write-LabStage 'Checking files'
    foreach ($labInput in @("$GameDir/JumpKing.exe", "$GameDir/Steamworks.NET.dll", "$GameDir/steam_api64.dll", "$MultiplayerDir/JumpKingMultiplayer.dll", "$BinaryDir/MultiplayerExpansion.exe")) {
        $labInputs += [pscustomobject]@{ Path=$labInput; SHA256=(Get-FileHash -LiteralPath $labInput -Algorithm SHA256).Hash }
    }
    $labInputs | ConvertTo-Json | Set-Content "$labSession/inputs.json"
    Copy-Item "$BinaryDir/MultiplayerExpansion.exe" $labSession
    return $labSession
}
