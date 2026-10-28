param(
    [string]$QuestDir = 'C:\Program Files (x86)\Steam\steamapps\common\JUMP KING QUEST',
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$Python = 'python',
    [string]$Manifest = (Join-Path $PSScriptRoot '../content/jkq-import.json')
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'content-inputs.ps1')
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$taskRoot = Join-Path $taskRepo 'build/_work/metroidvania/content-imports'
New-Item -ItemType Directory -Force $taskRoot | Out-Null
. (Join-Path $taskRepo 'mods/jk-runtime/build-support/build-retention.ps1')
# these histories contain reproducible authoring output; keep source receipts for distinct inputs
$taskPreserve = @{}
foreach ($taskHistory in Get-ChildItem -LiteralPath $taskRoot -Directory | Sort-Object LastWriteTimeUtc -Descending) {
    $taskReceipt = Join-Path $taskHistory.FullName 'receipt.json'
    if (Test-Path -LiteralPath $taskReceipt) {
        $taskIdentity = (Get-Content -LiteralPath $taskReceipt -Raw | ConvertFrom-Json).SourceIdentity
        if (-not $taskPreserve.ContainsKey($taskIdentity)) { $taskPreserve[$taskIdentity]=$taskHistory.FullName }
    }
}
Remove-BuildArtifactHistory -Directory $taskRoot -NamePattern '^run-[0-9a-f]{32}$' -Keep 2 -CompletionMarker '.build-complete' -Preserve @($taskPreserve.Values) | Out-Null
$taskRun = Join-Path $taskRoot ('run-'+[Guid]::NewGuid().ToString('N'))
$taskModels = Join-Path $taskRun 'enemies'
$taskGear = Join-Path $taskRun 'gear'
$taskAssets = Join-Path $taskRun 'assets'
$taskDrivers = Join-Path $taskRun 'drivers'
New-Item -ItemType Directory -Force $taskDrivers,(Join-Path $taskAssets 'enemies'),(Join-Path $taskAssets 'equipment'),(Join-Path $taskAssets 'audio') | Out-Null
$taskCompiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskManaged = Join-Path $QuestDir 'JKQ_Data/Managed/FMODUnity.dll'
$taskNative = Join-Path $QuestDir 'JKQ_Data/Plugins/x86_64/fmodstudio.dll'
function Invoke-ImportPython([string]$Script,[string[]]$Arguments) {
    & $Python (Join-Path $PSScriptRoot $Script) @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Quest pipeline failed: $Script" }
}
function Build-Driver([string]$Name,[string[]]$References) {
    $taskSource = (Resolve-Path (Join-Path $PSScriptRoot "$Name.cs")).Path
    $taskOutput = Join-Path $taskDrivers "$Name.exe"
    & $taskCompiler /nologo /optimize+ /target:exe /platform:x64 /nowarn:0649 /reference:System.Web.Extensions.dll "/out:$taskOutput" @References $taskSource
    if ($LASTEXITCODE -ne 0) { throw "Authoring driver compilation failed: $Name" }
    return $taskOutput
}
try {
    Invoke-ImportPython '../tests/test_quest_pipeline.py' @()
    Invoke-ImportPython '../tests/test_quest_animation.py' @()
    Write-Host '[IMPORT] Resolve enemy prefab graphs and source timelines'
    Invoke-ImportPython 'import_quest_enemy.py' @('--quest-dir',$QuestDir,'--output',$taskModels,'--manifest',$Manifest)
    Invoke-ImportPython 'compile_quest_content.py' @('--manifest',$Manifest,'--models',$taskModels,'--output',$taskAssets)
    Write-Host '[IMPORT] Decode equipment selected by the same manifest'
    Invoke-ImportPython 'import_quest_art.py' @('--quest-dir',$QuestDir,'--output',$taskGear,'--manifest',$Manifest)
    Copy-Item -LiteralPath "$GameDir/MonoGame.Framework.dll",$taskManaged,$taskNative -Destination $taskDrivers
    Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $taskDrivers
    $taskGraphicsRefs = @('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll',"/reference:$GameDir/MonoGame.Framework.dll")
    $taskEnemyBaker = Build-Driver 'BakeQuestEnemies' $taskGraphicsRefs
    $taskGearBaker = Build-Driver 'BakeQuestModels' $taskGraphicsRefs
    $taskSpriteBaker = Build-Driver 'BuildQuestSprites' @('/reference:System.Drawing.dll')
    $taskAudioBaker = Build-Driver 'ImportQuestAudio' @("/reference:$taskManaged")
    foreach ($taskEnemy in Get-Content (Join-Path $taskModels 'index.json') -Raw | ConvertFrom-Json) {
        & $taskEnemyBaker (Join-Path $taskModels $taskEnemy.id) (Join-Path $taskAssets 'enemies')
        if ($LASTEXITCODE -ne 0) { throw "Enemy render failed: $($taskEnemy.id)" }
        Copy-Item -LiteralPath (Join-Path $taskModels "$($taskEnemy.id)/$($taskEnemy.id).bin") -Destination (Join-Path $taskAssets 'enemies')
    }
    & $taskGearBaker (Join-Path $taskGear 'models') (Join-Path $taskGear 'renders') $GameDir
    if ($LASTEXITCODE -ne 0) { throw 'Equipment render failed' }
    & $taskSpriteBaker $taskGear (Join-Path $taskAssets 'equipment')
    if ($LASTEXITCODE -ne 0) { throw 'Equipment sprite compilation failed' }
    & $taskAudioBaker $QuestDir (Join-Path $taskAssets 'audio') (Join-Path $taskRun 'raw-audio') (Join-Path $taskAssets 'audio-recipe.json')
    if ($LASTEXITCODE -ne 0) { throw 'Audio capture failed' }
    Invoke-ImportPython 'compile_quest_content.py' @('--manifest',$Manifest,'--models',$taskModels,'--output',$taskAssets,'--verify-assets',$taskAssets)
    [xml]$taskCatalogue = Get-Content -LiteralPath (Join-Path $taskAssets 'catalog.xml') -Raw
    $taskCatalogue.DocumentElement.SetAttribute('ToolchainHash',(Get-QuestToolchainHash $PSScriptRoot))
    $taskXmlSettings = [Xml.XmlWriterSettings]::new()
    $taskXmlSettings.NewLineChars = "`n"
    $taskXmlSettings.Indent = $true
    $taskXmlSettings.Encoding = [Text.UTF8Encoding]::new($false)
    $taskWriter = [Xml.XmlWriter]::Create((Join-Path $taskAssets 'catalog.xml'),$taskXmlSettings)
    try { $taskCatalogue.Save($taskWriter) } finally { $taskWriter.Dispose() }
    $taskReceipts = @(Get-ChildItem -LiteralPath $taskModels -File -Recurse -Filter 'source-receipt.json' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
    $taskIdentity = ($taskReceipts | ForEach-Object { $_.inputs } | Sort-Object name -Unique | ConvertTo-Json -Compress)
    @{Actor='Niotid';ManifestSHA256=(Get-FileHash -LiteralPath $Manifest).Hash;SourceIdentity=$taskIdentity;EnemyReceipts=$taskReceipts;AudioReceipt=(Get-Content (Join-Path $taskAssets 'audio/source-receipt.json') -Raw | ConvertFrom-Json)} | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $taskRun 'receipt.json') -Encoding UTF8
    # promote only a completely validated pack; catalogue goes last so readers can't accept partial output
    $taskDestination = Join-Path $PSScriptRoot '../assets/imported'
    $taskBackup = Join-Path $taskRun 'previous-assets'
    $taskChanged = [Collections.Generic.List[object]]::new()
    try {
        foreach ($taskFile in Get-ChildItem -LiteralPath $taskAssets -File -Recurse | Sort-Object @{Expression={if($_.Name -eq 'catalog.xml'){1}else{0}}},FullName) {
            $taskRelative=$taskFile.FullName.Substring($taskAssets.Length+1);$taskTarget=Join-Path $taskDestination $taskRelative;$taskOld=Join-Path $taskBackup $taskRelative;$taskHad=Test-Path -LiteralPath $taskTarget
            New-Item -ItemType Directory -Force (Split-Path $taskTarget -Parent) | Out-Null
            if($taskHad){New-Item -ItemType Directory -Force (Split-Path $taskOld -Parent) | Out-Null;Copy-Item -LiteralPath $taskTarget -Destination $taskOld}
            $taskChanged.Add(@{Target=$taskTarget;Backup=$taskOld;Had=$taskHad});Copy-Item -LiteralPath $taskFile.FullName -Destination $taskTarget -Force
            if((Get-FileHash -LiteralPath $taskTarget).Hash -ne (Get-FileHash -LiteralPath $taskFile.FullName).Hash){throw 'Promoted asset hash mismatch'}
        }
    } catch {
        for($taskIndex=$taskChanged.Count-1;$taskIndex -ge 0;$taskIndex--){$taskEntry=$taskChanged[$taskIndex];if($taskEntry.Had){Copy-Item -LiteralPath $taskEntry.Backup -Destination $taskEntry.Target -Force}else{Remove-Item -LiteralPath $taskEntry.Target -ErrorAction SilentlyContinue}}
        throw
    }
    [IO.File]::WriteAllText((Join-Path $taskRun '.build-complete'),[DateTime]::UtcNow.ToString('o'))
    Write-Host "[OK] Compiled content pack and receipts: $taskRun"
} catch {
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $taskRun 'failure.txt') -Encoding UTF8
    throw
} finally {
    Get-ChildItem -LiteralPath $taskDrivers -File | Remove-Item
}
