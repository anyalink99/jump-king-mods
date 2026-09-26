param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskBuild = Join-Path $taskRepo 'build/run-verifier'
$taskInternal = Join-Path $taskBuild '_INTERNAL'
$taskUpload = Join-Path $taskBuild 'UPLOAD_TO_WORKSHOP'
New-Item -ItemType Directory -Force -Path $taskInternal,$taskUpload | Out-Null
$taskCompiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskRuntime = Join-Path $taskRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
$taskDeps = @("$GameDir/JumpKing.exe","$GameDir/MonoGame.Framework.dll","$GameDir/LanguageJK.dll","$GameDir/Steamworks.NET.dll",$taskRuntime)
$taskRefs = @($taskDeps | ForEach-Object { "/reference:$_" }) + @('/reference:System.Web.Extensions.dll','/reference:System.Security.dll')
$taskSources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$taskAssembly = Join-Path $taskInternal 'RunVerifier.Module.dll'
& $taskCompiler /nologo /optimize+ /target:library /langversion:5 "/out:$taskAssembly" $taskRefs $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Run Verifier compilation failed' }
Copy-Item -LiteralPath $taskDeps -Destination $taskInternal -Force
Get-ChildItem $GameDir -Filter 'SharpDX*.dll' | Copy-Item -Destination $taskInternal -Force
$taskTests = Join-Path $taskInternal 'RunVerifierTests.exe'
& $taskCompiler /nologo /target:exe /langversion:5 "/out:$taskTests" $taskRefs $taskSources (Join-Path $PSScriptRoot 'tests/RunVerifierTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Run Verifier test compilation failed' }
& $taskTests
if ($LASTEXITCODE -ne 0) { throw 'Run Verifier tests failed' }
& (Join-Path $taskRepo 'mods/jk-runtime/sdk/package.ps1') -Implementation $taskAssembly -Output (Join-Path $taskUpload 'RunVerifier.dll') -GameDir $GameDir
Copy-Item (Join-Path $PSScriptRoot 'README.md') $taskUpload -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $taskUpload -Recurse -Force
