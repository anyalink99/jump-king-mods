param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskInternal = Join-Path $taskRepo 'build/jk-runtime/_INTERNAL'
$taskWorkshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop/content/1061090'
$taskManager = Join-Path $taskWorkshop '3169568082/JumpKingManager.dll'
if (-not (Test-Path -LiteralPath $taskManager)) { Write-Host '[SKIP] Optional installed Jump King Manager fixture: Workshop item absent'; return }
$taskExe = Join-Path $taskInternal 'ManagerCompatibilityTests.exe'
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File -Recurse | ForEach-Object FullName)
$taskReferences = @("/reference:$(Join-Path $GameDir 'JumpKing.exe')", "/reference:$(Join-Path $GameDir 'MonoGame.Framework.dll')", "/reference:$(Join-Path $GameDir 'LanguageJK.dll')", '/reference:System.Web.Extensions.dll', '/reference:System.Xml.Linq.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll')
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /optimize+ /target:exe /langversion:5 /main:JKRuntime.ManagerCompatibilityTests "/out:$taskExe" $taskReferences $taskSources (Join-Path $PSScriptRoot 'tests/ManagerCompatibilityTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Manager fixture compilation failed' }
$taskSeen = @{}
foreach ($taskFile in (Get-ChildItem -LiteralPath $taskWorkshop -Recurse -File -Filter '0Harmony.dll' | Sort-Object FullName)) {
    $taskVersion = [Reflection.AssemblyName]::GetAssemblyName($taskFile.FullName).Version.ToString()
    if ($taskSeen.ContainsKey($taskVersion)) { continue }
    $taskSeen[$taskVersion] = $true
    $taskLastEngine = $taskFile.FullName
    & $taskExe $taskFile.FullName $taskManager
    if ($LASTEXITCODE -ne 0) { throw "Manager fixture failed: Harmony $taskVersion" }
}
if ($taskSeen.Count -eq 0) { throw 'No installed Harmony for Manager verification' }
& $taskExe 'none' $taskManager 'refuse'
if ($LASTEXITCODE -ne 0) { throw 'Missing-Harmony Manager refusal failed' }
$taskUnknownDir = Join-Path $taskInternal 'manager-unreviewed'
New-Item -ItemType Directory -Force -Path $taskUnknownDir | Out-Null
$taskUnknown = Join-Path $taskUnknownDir 'JumpKingManager.dll'
[IO.File]::WriteAllBytes($taskUnknown, [byte[]]([IO.File]::ReadAllBytes($taskManager) + [byte]0))
& $taskExe $taskLastEngine $taskUnknown 'refuse'
if ($LASTEXITCODE -ne 0) { throw 'Unknown-build Manager refusal failed' }
