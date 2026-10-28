param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [ValidateSet('Fast','Integration','Full')][string]$Tier = 'Fast'
)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskRepo = (Resolve-Path (Join-Path $taskRoot '..\..')).Path
$taskCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskBuild = Join-Path $taskRepo 'build\no-walk-off\_INTERNAL'
$taskUpload = Join-Path $taskRepo 'build\no-walk-off\UPLOAD_TO_WORKSHOP'
$taskRuntime = Join-Path $taskRepo 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
New-Item -ItemType Directory -Force -Path $taskBuild,$taskUpload | Out-Null
& python (Join-Path $taskRepo 'scripts\block_registry.py')
if ($LASTEXITCODE -ne 0) { throw 'Block colour reservation check failed' }
$taskRefs = @('/reference:System.Windows.Forms.dll', '/reference:System.Xml.Linq.dll', "/reference:$GameDir\JumpKing.exe", "/reference:$GameDir\MonoGame.Framework.dll", "/reference:$GameDir\LanguageJK.dll", "/reference:$taskRuntime")
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$taskAssembly = Join-Path $taskBuild 'NoWalkOff.Module.dll'
& $taskCompiler /nologo /optimize+ /target:library /langversion:5 "/out:$taskAssembly" $taskRefs $taskSources
if ($LASTEXITCODE -ne 0) { throw 'No Walk Off compilation failed' }
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
$taskTestRefs = $taskRefs + @('/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', "/reference:$taskBuild\UnknownProvider.dll")
& $taskCompiler /nologo /optimize+ /target:exe /langversion:5 "/out:$taskBuild\NoWalkOffTests.exe" /main:NoWalkOff.Tests $taskTestRefs $taskSources $taskTests
if ($LASTEXITCODE -ne 0) { throw 'No Walk Off tests compilation failed' }
$taskTestArgs = @($GameDir, $Tier)
& "$taskBuild\NoWalkOffTests.exe" @taskTestArgs
if ($LASTEXITCODE -ne 0) { throw 'No Walk Off tests failed' }
& (Join-Path $taskRepo 'mods\jk-runtime\sdk\package.ps1') -Implementation $taskAssembly -Output "$taskUpload\NoWalkOff.dll" -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { throw 'No Walk Off package failed' }
Write-Host "[OK] No Walk Off build and $Tier verification"
