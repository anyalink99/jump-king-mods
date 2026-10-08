param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$probeRepo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$probeBuild = Join-Path $probeRepo 'build/jk-runtime/_INTERNAL/restart-probe'
$probeCompiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$probeDependencies = @("$GameDir/JumpKing.exe", "$GameDir/MonoGame.Framework.dll", "$GameDir/LanguageJK.dll", "$GameDir/Steamworks.NET.dll", "$GameDir/Content/JKMods/0Harmony.dll")
New-Item -ItemType Directory -Force -Path $probeBuild | Out-Null
foreach ($probeDependency in $probeDependencies) { Copy-Item -LiteralPath $probeDependency -Destination $probeBuild -Force }
$probeReferences = @($probeDependencies | ForEach-Object { "/reference:$_" })
$probeSource = Join-Path $PSScriptRoot 'RestartProbe.cs'
& $probeCompiler /nologo /optimize+ /target:library /langversion:5 /nowarn:1685 "/out:$probeBuild/JKRestartProbe.dll" $probeReferences $probeSource
if ($LASTEXITCODE -ne 0) { throw 'Restart probe compilation failed' }
& $probeCompiler /nologo /optimize+ /target:exe /langversion:5 /nowarn:1685 "/out:$probeBuild/ProbeTests.exe" $probeReferences $probeSource (Join-Path $PSScriptRoot 'ProbeTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Restart probe fixture compilation failed' }
& "$probeBuild/ProbeTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Restart probe checks failed' }
Write-Host "[OK] Temporary diagnostic only; not installed: $probeBuild/JKRestartProbe.dll"
