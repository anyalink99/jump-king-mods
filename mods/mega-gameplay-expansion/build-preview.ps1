param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path "$PSScriptRoot\..\..").Path
$taskOutput = Join-Path $taskRepo 'build\mega-gameplay-expansion\_INTERNAL'
$taskPreview = Join-Path $taskRepo 'build\mega-gameplay-expansion\preview'
New-Item -ItemType Directory -Force -Path $taskOutput,$taskPreview | Out-Null
$taskSources = @(Get-ChildItem "$PSScriptRoot\src" -Filter '*.cs' | ForEach-Object FullName)
$taskRefs = @("/reference:$GameDir\JumpKing.exe","/reference:$GameDir\MonoGame.Framework.dll","/reference:$GameDir\LanguageJK.dll","/reference:$taskRepo\build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll",'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll')
$taskExecutable = Join-Path $taskOutput 'RenderWorkshopPreview.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /langversion:5 /main:MegaGameplayExpansion.RenderWorkshopPreview "/out:$taskExecutable" $taskRefs $taskSources "$PSScriptRoot\tools\RenderWorkshopPreview.cs"
if ($LASTEXITCODE -ne 0) { throw 'Workshop preview compilation failed' }
& $taskExecutable $GameDir "$PSScriptRoot\workshop-preview.png" "$taskPreview\workshop-preview-1024.png"
if ($LASTEXITCODE -ne 0) { throw 'Workshop preview rendering failed' }
