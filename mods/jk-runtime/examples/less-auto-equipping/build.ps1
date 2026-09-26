param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$SdkDirectory = (Join-Path $PSScriptRoot '../..'),
    [Parameter(Mandatory=$true)][string]$HarmonyPath,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'build')
)
$ErrorActionPreference = 'Stop'
$exampleCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exampleInternal = Join-Path $OutputDirectory '_INTERNAL'
$examplePayload = Join-Path $OutputDirectory 'UPLOAD_TO_WORKSHOP'
$exampleRuntime = Join-Path $SdkDirectory 'JKRuntime.dll'
$examplePackager = Join-Path $SdkDirectory 'PackageBuilder.exe'
$exampleReferences = @("$GameDir/JumpKing.exe", "$GameDir/MonoGame.Framework.dll", "$GameDir/LanguageJK.dll", "$GameDir/Steamworks.NET.dll", $exampleRuntime, $HarmonyPath)
foreach ($exampleFile in $exampleReferences + @($exampleCompiler, $examplePackager)) {
    if (-not (Test-Path -LiteralPath $exampleFile -PathType Leaf)) { throw "Missing example input: $exampleFile" }
}
if ((Get-FileHash -LiteralPath $HarmonyPath -Algorithm SHA256).Hash -ne '89F4321499AD00F8127F576CE0A35CFE88DFB8701F19B519B61A7F8F868143FB') {
    throw 'This example is verified and licensed for the unmodified Harmony 2.3.6 distribution; use that DLL.'
}
New-Item -ItemType Directory -Force -Path $exampleInternal,$examplePayload | Out-Null
Copy-Item -LiteralPath $exampleReferences -Destination $exampleInternal -Force
Copy-Item -LiteralPath $examplePackager -Destination $exampleInternal -Force
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $exampleInternal -Force
$exampleArguments = @($exampleReferences | ForEach-Object { "/reference:$_" })
$exampleSources = @((Join-Path $PSScriptRoot 'ModEntry.cs'), (Join-Path $PSScriptRoot 'Preferences.cs')) + @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Patches') -Filter '*.cs' -File | ForEach-Object FullName)
$exampleAssembly = Join-Path $exampleInternal 'LessAutoEquipping.Module.dll'
& $exampleCompiler /nologo /target:library /langversion:5 /nowarn:1685 /reference:System.Xml.Linq.dll "/out:$exampleAssembly" $exampleArguments $exampleSources
if ($LASTEXITCODE -ne 0) { throw 'LessAutoEquipping example compilation failed' }
$exampleTests = Join-Path $exampleInternal 'MigrationTests.exe'
& $exampleCompiler /nologo /target:exe /langversion:5 /nowarn:1685 /reference:System.Xml.Linq.dll "/out:$exampleTests" $exampleArguments $exampleSources (Join-Path $PSScriptRoot 'tests/MigrationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Migration test compilation failed' }
& $exampleTests
if ($LASTEXITCODE -ne 0) { throw 'Migration behavior checks failed' }
$exampleShell = Join-Path $examplePayload 'LessAutoEquipping.dll'
& (Join-Path $exampleInternal 'PackageBuilder.exe') $exampleAssembly $exampleShell $GameDir $HarmonyPath
if ($LASTEXITCODE -ne 0) { throw 'Migration example packaging failed' }
Copy-Item -LiteralPath $HarmonyPath -Destination (Join-Path $examplePayload '0Harmony.dll') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE.md'),(Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') -Destination $examplePayload -Force
$exampleSmoke = Join-Path $exampleInternal 'PackageSmoke.exe'
& $exampleCompiler /nologo /target:exe /langversion:5 "/out:$exampleSmoke" "/reference:$GameDir/JumpKing.exe" (Join-Path $PSScriptRoot 'tests/PackageSmoke.cs')
if ($LASTEXITCODE -ne 0) { throw 'Package smoke test compilation failed' }
# Exercise discovery in a scratch payload; never write fixture preferences into a reusable release folder.
$exampleScratch = Join-Path $exampleInternal ('package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $exampleScratch | Out-Null
Copy-Item -LiteralPath $exampleShell,(Join-Path $examplePayload '0Harmony.dll') -Destination $exampleScratch
& $exampleSmoke (Join-Path $exampleInternal 'JKRuntime.dll') (Join-Path $exampleScratch 'LessAutoEquipping.dll')
if ($LASTEXITCODE -ne 0) { throw 'Packaged migration checks failed' }
Write-Host "[OK] LessAutoEquipping migration example: $examplePayload"
