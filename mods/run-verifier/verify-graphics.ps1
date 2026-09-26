param([string]$GameDir='C:/Program Files (x86)/Steam/steamapps/common/Jump King')
$ErrorActionPreference='Stop'
$taskRepo=(Resolve-Path "$PSScriptRoot/../..").Path
$taskBuild=Join-Path $taskRepo 'build/run-verifier/_INTERNAL'
$taskRefs=@("$GameDir/JumpKing.exe","$GameDir/MonoGame.Framework.dll","$GameDir/LanguageJK.dll","$GameDir/Steamworks.NET.dll","$taskRepo/build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll") | ForEach-Object { "/reference:$_" }
$taskSources=Get-ChildItem "$PSScriptRoot/src/*.cs" | ForEach-Object FullName
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo /target:exe /platform:x64 /langversion:5 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll "/out:$taskBuild/GraphicsTests.exe" $taskRefs $taskSources (Join-Path $PSScriptRoot 'tests/GraphicsTests.cs')
if($LASTEXITCODE -ne 0){throw 'Graphics fixture compilation failed'}
& "$taskBuild/GraphicsTests.exe" $GameDir "$taskBuild/graphics"
if($LASTEXITCODE -ne 0){throw 'Graphics fixture failed'}

