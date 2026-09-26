param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$taskCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskOutput = Join-Path $PSScriptRoot 'build'
$taskPayload = Join-Path $taskOutput 'UPLOAD_TO_WORKSHOP'
New-Item -ItemType Directory -Force -Path $taskOutput,$taskPayload | Out-Null
$taskRuntime = Join-Path $PSScriptRoot 'JKRuntime.dll'
$taskDependencies = @("$GameDir\JumpKing.exe", "$GameDir\MonoGame.Framework.dll", "$GameDir\LanguageJK.dll", $taskRuntime)
foreach ($taskFile in $taskDependencies + @($taskCompiler, (Join-Path $PSScriptRoot 'PackageBuilder.exe'))) {
    if (-not (Test-Path -LiteralPath $taskFile -PathType Leaf)) { throw "Missing SDK/build input: $taskFile" }
}
Copy-Item -LiteralPath $taskDependencies -Destination $taskOutput -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PackageBuilder.exe') -Destination $taskOutput -Force
$taskReferences = @($taskDependencies | ForEach-Object { "/reference:$_" })
$taskImplementation = Join-Path $taskOutput 'Example.Module.dll'
& $taskCompiler /nologo /target:library /langversion:5 "/out:$taskImplementation" $taskReferences (Join-Path $PSScriptRoot 'ModEntry.cs')
if ($LASTEXITCODE -ne 0) { throw 'Example compilation failed' }
& (Join-Path $taskOutput 'PackageBuilder.exe') $taskImplementation (Join-Path $taskPayload 'Example.dll') $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Example packaging failed' }
Write-Host "[OK] Standalone mod package: $taskPayload"
