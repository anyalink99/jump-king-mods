param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Assembly = Join-Path $RepoRoot 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
$Fixture = Join-Path $RepoRoot 'build\jk-runtime\_INTERNAL\HarmonyFixture.dll'
$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $Compiler /nologo /target:library "/out:$Fixture" (Join-Path $PSScriptRoot 'tests\HarmonyFixture.cs')
if ($LASTEXITCODE -ne 0) { throw 'Harmony fixture failed to compile' }
$ForeignFixture = Join-Path $RepoRoot 'build\jk-runtime\_INTERNAL\ForeignModifierFixture.dll'
$ForeignTests = Join-Path $RepoRoot 'build\jk-runtime\_INTERNAL\ForeignModifierTests.exe'
Copy-Item -LiteralPath (Join-Path $GameDir 'Steamworks.NET.dll') -Destination (Split-Path $ForeignTests) -Force
$References = @("/reference:$(Join-Path $GameDir 'JumpKing.exe')", "/reference:$(Join-Path $GameDir 'MonoGame.Framework.dll')", "/reference:$(Join-Path $GameDir 'LanguageJK.dll')", '/reference:System.Web.Extensions.dll', '/reference:System.Xml.Linq.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll')
& $Compiler /nologo /optimize+ /target:library /langversion:5 "/out:$ForeignFixture" $References (Join-Path $PSScriptRoot 'tests\ForeignModifierFixture.cs')
if ($LASTEXITCODE -ne 0) { throw 'Foreign mod fixture failed to compile' }
$Sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Recurse -File -Filter '*.cs' | Sort-Object FullName | ForEach-Object FullName)
& $Compiler /nologo /optimize+ /target:exe /langversion:5 /main:JKRuntime.ForeignModifierTests "/out:$ForeignTests" $References $Sources (Join-Path $PSScriptRoot 'tests\ForeignModifierTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Foreign attribution tests failed to compile' }
$Workshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop\content\1061090'
$Seen = @{}
if (Test-Path -LiteralPath $Workshop) {
    foreach ($File in (Get-ChildItem -LiteralPath $Workshop -Filter '0Harmony.dll' -File -Recurse | Sort-Object FullName)) {
        $Version = [Reflection.AssemblyName]::GetAssemblyName($File.FullName).Version.ToString()
        if ($Seen.ContainsKey($Version)) { continue }
        $Seen[$Version] = $true
        & (Join-Path $RepoRoot 'build/jk-runtime/_INTERNAL/RuntimeChecks.exe') 'JKRuntime.MotionObservationTests' $File.FullName
        if ($LASTEXITCODE -ne 0) { throw "Harmony $Version motion observation failed" }
        & (Join-Path $RepoRoot 'build/jk-runtime/_INTERNAL/RuntimeChecks.exe') 'JKRuntime.InteropTests' $File.FullName
        if ($LASTEXITCODE -ne 0) { throw "Harmony $Version interop observation failed" }
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests\verify-harmony.ps1') -GameDir $GameDir -RuntimeAssembly $Assembly -HarmonyAssembly $File.FullName -FixtureAssembly $Fixture
        if ($LASTEXITCODE -ne 0) { throw "Harmony $Version inspection fixture failed" }
        & $ForeignTests $File.FullName $ForeignFixture
        if ($LASTEXITCODE -ne 0) { throw "Harmony $Version foreign attribution fixture failed" }
        & $ForeignTests $File.FullName $ForeignFixture 'foreign-first'
        if ($LASTEXITCODE -ne 0) { throw "Harmony $Version foreign-first attribution fixture failed" }
        $Seen[$Version] = $File.FullName
    }
}
if ($Seen.Count -eq 0) { Write-Host '[SKIP] Optional real Harmony tests: no installed Workshop Harmony.' }
if ($Seen.Count -gt 1) {
    $Paths = @($Seen.Keys | Sort-Object | ForEach-Object { $Seen[$_] })
    & $ForeignTests $Paths[0] $ForeignFixture 'foreign-first' $Paths[-1]
    if ($LASTEXITCODE -ne 0) { throw 'Mixed loaded Harmony attribution fixture failed' }
    & $ForeignTests $Paths[-1] $ForeignFixture 'foreign-first' $Paths[0]
    if ($LASTEXITCODE -ne 0) { throw 'Reverse mixed loaded Harmony attribution fixture failed' }
    foreach ($Mode in @('conflict','late-conflict')) {
        & $ForeignTests $Paths[0] $ForeignFixture $Mode $Paths[-1]
        if ($LASTEXITCODE -ne 0) { throw "$Mode Harmony attribution fixture failed" }
        & $ForeignTests $Paths[-1] $ForeignFixture $Mode $Paths[0]
        if ($LASTEXITCODE -ne 0) { throw "Reverse $Mode Harmony attribution fixture failed" }
    }
}
