param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King', [string]$HarmonyPath, [switch]$StartupTrace, [switch]$Audio)
$ErrorActionPreference = 'Stop'
$uiRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$uiBuild = Join-Path $uiRepo 'build/jk-runtime/_INTERNAL/ui-graphics'
New-Item -ItemType Directory -Force -Path $uiBuild | Out-Null
$uiDependencies = @("$GameDir/JumpKing.exe", "$GameDir/MonoGame.Framework.dll", "$GameDir/LanguageJK.dll", "$GameDir/Steamworks.NET.dll", "$uiRepo/build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll")
foreach ($uiDependency in $uiDependencies) { Copy-Item -LiteralPath $uiDependency -Destination $uiBuild -Force }
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $uiBuild -Force
$uiReferences = @($uiDependencies | ForEach-Object { "/reference:$_" })
$uiExe = Join-Path $uiBuild 'UiGraphicsTests.exe'
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo /target:exe /platform:x64 /langversion:5 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$uiExe" $uiReferences (Join-Path $PSScriptRoot 'tests/UiGraphicsTests.cs') (Join-Path $PSScriptRoot 'tests/InventoryGraphicsTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'UI graphics fixture compilation failed' }
$uiOutput = Join-Path $uiBuild ([Guid]::NewGuid().ToString('N'))
if (!$HarmonyPath) { $HarmonyPath = Join-Path $GameDir 'Content/JKMods/0Harmony.dll' }
$uiTraceMarker = Join-Path $uiBuild 'JKRuntime.StartupTrace.enabled'
$uiHadTraceMarker = Test-Path -LiteralPath $uiTraceMarker
if ($StartupTrace -and -not $uiHadTraceMarker) { [IO.File]::WriteAllText($uiTraceMarker, 'Isolated native dispatch test') }
try {
    & $uiExe $GameDir $uiRepo $uiOutput $HarmonyPath $(if ($Audio) { 'audio' } else { 'graphics' })
    if ($LASTEXITCODE -ne 0) { throw 'UI graphics checks failed' }
}
finally {
    if ($StartupTrace -and -not $uiHadTraceMarker) { Remove-Item -LiteralPath $uiTraceMarker }
}
