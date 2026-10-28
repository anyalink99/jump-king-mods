param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [ValidateSet('Fast','Integration','Full')][string]$Tier = 'Fast',
    [string]$BallAssembly,
    [string]$ItemsAssembly
)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskRepo = (Resolve-Path (Join-Path $taskRoot '..\..')).Path
$taskCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskBuild = Join-Path $taskRepo 'build\powers\_INTERNAL'
$taskUpload = Join-Path $taskRepo 'build\powers\UPLOAD_TO_WORKSHOP'
$taskRuntime = Join-Path $taskRepo 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
New-Item -ItemType Directory -Force -Path $taskBuild,$taskUpload | Out-Null
& python (Join-Path $taskRepo 'scripts\block_registry.py')
if ($LASTEXITCODE -ne 0) { throw 'Block colour reservation check failed' }
$taskRefs = @('/reference:System.Windows.Forms.dll', '/reference:System.Xml.Linq.dll', "/reference:$GameDir\JumpKing.exe", "/reference:$GameDir\MonoGame.Framework.dll", "/reference:$GameDir\LanguageJK.dll", "/reference:$taskRuntime")
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$taskAssembly = Join-Path $taskBuild 'Powers.Module.dll'
$taskAudio = "/resource:$taskRoot\assets\audio\air-dash-8bit.wav,Powers.air-dash-8bit.wav"
& $taskCompiler /nologo /optimize+ /target:library /langversion:5 "/out:$taskAssembly" $taskAudio $taskRefs $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Powers compilation failed' }
Copy-Item -LiteralPath "$GameDir\JumpKing.exe","$GameDir\MonoGame.Framework.dll","$GameDir\LanguageJK.dll",$taskRuntime -Destination $taskBuild -Force
# use a reproducible engine fixture
# Runtime integration checks installed engine versions separately
$taskHarmony = Join-Path $taskRepo 'mods/subframe-charge/lib/0Harmony.dll'
$taskFixtureHarmony = Join-Path $taskBuild '0Harmony.dll'
if ((Test-Path -LiteralPath $taskFixtureHarmony) -and (Get-FileHash -LiteralPath $taskFixtureHarmony).Hash -ne (Get-FileHash -LiteralPath $taskHarmony).Hash) {
    Copy-Item -LiteralPath $taskFixtureHarmony -Destination ($taskFixtureHarmony + '.' + (Get-FileHash -LiteralPath $taskFixtureHarmony).Hash + '.previous') -Force
}
Copy-Item -LiteralPath $taskHarmony -Destination $taskFixtureHarmony -Force
$taskTests = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'tests') -Filter '*.cs' | ForEach-Object FullName)
& $taskCompiler /nologo /optimize+ /target:library /langversion:5 "/out:$taskBuild\UnknownProvider.dll" $taskRefs (Join-Path $taskRepo "mods\jk-runtime\tests\Inspection\fixtures\UnknownProvider.cs")
if ($LASTEXITCODE -ne 0) { throw 'Gimmick provider fixture compilation failed' }
$taskEdgeModule = Join-Path $taskRepo 'build\no-walk-off\_INTERNAL\NoWalkOff.Module.dll'
if (-not (Test-Path -LiteralPath $taskEdgeModule)) { throw 'Build No Walk Off before running the combined Powers checks.' }
Copy-Item -LiteralPath $taskEdgeModule -Destination $taskBuild -Force
$taskTestRefs = $taskRefs + @("/reference:$taskEdgeModule") + @('/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', "/reference:$taskBuild\UnknownProvider.dll")
& $taskCompiler /nologo /optimize+ /target:exe /langversion:5 "/out:$taskBuild\PowersTests.exe" /main:Powers.Tests $taskAudio $taskTestRefs $taskSources $taskTests
if ($LASTEXITCODE -ne 0) { throw 'Powers tests compilation failed' }
$taskTestArgs = @($GameDir, $Tier)
if ($BallAssembly -and $ItemsAssembly -and $Tier -ne 'Fast') { $taskTestArgs += @($BallAssembly, $ItemsAssembly) }
& "$taskBuild\PowersTests.exe" @taskTestArgs
if ($LASTEXITCODE -ne 0) { throw 'Powers tests failed' }
& (Join-Path $taskRepo 'mods\jk-runtime\sdk\package.ps1') -Implementation $taskAssembly -Output "$taskUpload\Powers.dll" -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Powers package failed' }
Write-Host "[OK] Powers build and $Tier verification"
