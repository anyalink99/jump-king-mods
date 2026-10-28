param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskWorkshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop/content/1061090'
$taskMod = Join-Path $taskWorkshop '3786443795/BoGMod3.dll'
if (-not (Test-Path -LiteralPath $taskMod)) { Write-Host '[SKIP] Optional BoGMod3 fixture: Workshop item absent'; return }
$taskOutput = Join-Path $taskRepo ('build/_work/tests/moving-platform-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskExe = Join-Path $taskOutput 'MovingPlatformCompatibilityTests.exe'
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File -Recurse | ForEach-Object FullName)
$taskReferences = @("/reference:$(Join-Path $GameDir 'JumpKing.exe')", "/reference:$(Join-Path $GameDir 'MonoGame.Framework.dll')", "/reference:$(Join-Path $GameDir 'LanguageJK.dll')", '/reference:System.Web.Extensions.dll', '/reference:System.Xml.Linq.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll')
foreach ($taskName in @('JumpKing.exe', 'MonoGame.Framework.dll', 'LanguageJK.dll', 'Steamworks.NET.dll')) {
    Copy-Item -LiteralPath (Join-Path $GameDir $taskName) -Destination $taskOutput
}
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $taskOutput
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /optimize+ /target:exe /langversion:5 /main:JKRuntime.MovingPlatformCompatibilityTests "/out:$taskExe" $taskReferences $taskSources (Join-Path $PSScriptRoot 'tests/MovingPlatformCompatibilityTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Moving platform fixture compilation failed' }
$taskSeen = @{}
foreach ($taskFile in (Get-ChildItem -LiteralPath $taskWorkshop -Recurse -File -Filter '0Harmony.dll' | Sort-Object FullName)) {
    $taskVersion = [Reflection.AssemblyName]::GetAssemblyName($taskFile.FullName).Version.ToString()
    if ($taskSeen.ContainsKey($taskVersion)) { continue }
    $taskSeen[$taskVersion] = $true
    $taskLastEngine = $taskFile.FullName
    foreach ($taskOrder in @('observer-first', 'adapter-first')) {
        & $taskExe $taskFile.FullName $taskMod $taskOrder
        if ($LASTEXITCODE -ne 0) { throw "Moving platform fixture failed: Harmony $taskVersion, $taskOrder" }
    }
}
if ($taskSeen.Count -eq 0) { throw 'No installed Harmony for moving platform verification' }
& $taskExe 'none' $taskMod 'refuse'
if ($LASTEXITCODE -ne 0) { throw 'Missing-Harmony moving platform refusal failed' }
$taskUnknown = Join-Path $taskOutput 'BoGMod3.dll'
[IO.File]::WriteAllBytes($taskUnknown, [byte[]]([IO.File]::ReadAllBytes($taskMod) + [byte]0))
& $taskExe $taskLastEngine $taskUnknown 'refuse'
if ($LASTEXITCODE -ne 0) { throw 'Unknown-build moving platform refusal failed' }
