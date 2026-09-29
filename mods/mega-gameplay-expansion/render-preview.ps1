param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King', [ValidateSet('Warp','AirDash')][string]$Effect = 'Warp')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path "$PSScriptRoot\..\..").Path
$output=Join-Path $repo 'build\mega-gameplay-expansion\_INTERNAL'
$sources=@(Get-ChildItem "$PSScriptRoot\src" -Filter '*.cs' | ForEach-Object FullName)
$refs=@("/reference:$GameDir\JumpKing.exe","/reference:$GameDir\MonoGame.Framework.dll","/reference:$GameDir\LanguageJK.dll","/reference:$repo\build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll",'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll')
$previewType = 'Render' + $Effect + 'Preview'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /langversion:5 "/main:MegaGameplayExpansion.$previewType" "/out:$output\$previewType.exe" $refs $sources "$PSScriptRoot\tools\$previewType.cs"
if($LASTEXITCODE -ne 0) { throw 'Preview compilation failed' }
& "$output\$previewType.exe" $GameDir "$repo\build\mega-gameplay-expansion\preview"
if($LASTEXITCODE -ne 0) { throw 'Offscreen visual verification failed' }
