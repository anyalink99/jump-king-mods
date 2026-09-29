$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\tools\publish-build.ps1')
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('jk-runtime-publish-' + [Guid]::NewGuid().ToString('N'))
$candidate = Join-Path $fixture 'candidate'
New-Item -ItemType Directory -Path "$candidate\SDK", "$fixture\UPLOAD_TO_WORKSHOP", "$fixture\SDK" | Out-Null
[IO.File]::WriteAllText("$fixture\UPLOAD_TO_WORKSHOP\JKRuntime.dll", 'old')
[IO.File]::WriteAllText("$fixture\SDK\marker", 'old SDK')
[IO.File]::WriteAllText("$candidate\JKRuntime.dll", 'new')
[IO.File]::WriteAllText("$candidate\SDK\marker", 'new SDK')
$lock = [IO.File]::Open("$fixture\UPLOAD_TO_WORKSHOP\JKRuntime.dll", 'Open', 'Read', 'None')
$failed = $false
try { Publish-RuntimeBuild $fixture "$candidate\JKRuntime.dll" "$candidate\SDK" }
catch { $failed = $true }
finally { $lock.Dispose() }
if (-not $failed -or [IO.File]::ReadAllText("$fixture\SDK\marker") -ne 'old SDK' -or [IO.File]::ReadAllText("$fixture\UPLOAD_TO_WORKSHOP\JKRuntime.dll") -ne 'old') {
    throw 'Failed publication did not preserve the previous release.'
}
Publish-RuntimeBuild $fixture "$candidate\JKRuntime.dll" "$candidate\SDK"
if ([IO.File]::ReadAllText("$fixture\SDK\marker") -ne 'new SDK' -or [IO.File]::ReadAllText("$fixture\UPLOAD_TO_WORKSHOP\JKRuntime.dll") -ne 'new') {
    throw 'Successful publication did not publish both artifacts.'
}
Write-Host "[OK] Atomic DLL publication and SDK rollback; fixture retained: $fixture"
