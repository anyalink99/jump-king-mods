param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King'
)
$ErrorActionPreference = 'Stop'
$RuntimeRoot = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $RuntimeRoot '..\..')).Path
$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$BuildRoot = Join-Path $RepoRoot 'build\jk-runtime'
$UploadDir = Join-Path $BuildRoot 'UPLOAD_TO_WORKSHOP'
$InternalDir = Join-Path $BuildRoot '_INTERNAL'
$PublishedAssembly = Join-Path $UploadDir 'JKRuntime.dll'
$JumpKing = Join-Path $GameDir 'JumpKing.exe'
$MonoGame = Join-Path $GameDir 'MonoGame.Framework.dll'
$Language = Join-Path $GameDir 'LanguageJK.dll'
$Steamworks = Join-Path $GameDir 'Steamworks.NET.dll'
$SessionVariable = Get-Variable -Name JKModBuildSession -Scope Global -ErrorAction SilentlyContinue
$Session = if ($SessionVariable) { $SessionVariable.Value } else { $null }
$InputKey = $null
$DependencyArtifacts = @($PublishedAssembly, (Join-Path $InternalDir 'PackageBuilder.exe'))
if ($null -ne $Session) {
    . (Join-Path $RepoRoot 'scripts/mod-build-cache.ps1')
    $NativeDependencies = @(Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | ForEach-Object FullName)
    $InputKey = Get-ModContentKey (@($RuntimeRoot, $Compiler, $JumpKing, $MonoGame, $Language, $Steamworks,
        (Join-Path $RepoRoot 'scripts/mod-build-cache.ps1'), (Join-Path $RepoRoot 'mods/subframe-charge/lib/0Harmony.dll'), (Join-Path $RepoRoot 'docs/modding/block-registry/catalog.json')) + $NativeDependencies)
    $Verified = $Session['jk-runtime']
    if ($Verified -and $Verified.Input -eq $InputKey -and @($DependencyArtifacts | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }).Count -eq 0) {
        if ((Get-ModContentKey $DependencyArtifacts) -eq $Verified.Output) {
            Write-Host '[REUSE] Runtime already built and tested from identical inputs in this check session.'
            return
        }
    }
}
foreach ($required in @($Compiler, $JumpKing, $MonoGame, $Language, $Steamworks)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Missing build input: $required" }
}
$StableInternal = $InternalDir
$InternalDir = Join-Path $StableInternal ('validation\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $InternalDir | Out-Null
$Assembly = Join-Path $InternalDir 'JKRuntime.dll'
$ObsoleteAlias = Join-Path $UploadDir 'UIApiPlus.dll'
$Sources = @(Get-ChildItem -LiteralPath (Join-Path $RuntimeRoot 'src') -Filter '*.cs' -File -Recurse |
    Sort-Object FullName | ForEach-Object FullName)
$References = @("/reference:$JumpKing", "/reference:$MonoGame", "/reference:$Language", '/reference:System.Web.Extensions.dll', '/reference:System.Xml.Linq.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll')
$Resources = @("/resource:$(Join-Path $RepoRoot 'docs\modding\block-registry\catalog.json'),JKRuntime.BlockCatalog.json")
& $Compiler /nologo /optimize+ /target:library /langversion:5 /nowarn:1591 "/doc:$(Join-Path $InternalDir 'JKRuntime.xml')" "/out:$Assembly" $References $Resources $Sources
if ($LASTEXITCODE -ne 0) { throw 'JK Runtime compilation failed' }
Copy-Item -LiteralPath $JumpKing, $MonoGame, $Language, $Steamworks -Destination $InternalDir -Force
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $InternalDir -Force
& $Compiler /nologo /target:exe /langversion:5 "/out:$(Join-Path $InternalDir 'PackageBuilder.exe')" $References "/reference:$Assembly" /reference:Microsoft.CSharp.dll (Join-Path $RuntimeRoot 'sdk\PackageBuilder.cs')
if ($LASTEXITCODE -ne 0) { throw 'Runtime SDK packaging tool failed' }
& $Compiler /nologo /target:exe /langversion:5 "/out:$(Join-Path $InternalDir 'ApiSurface.exe')" (Join-Path $RuntimeRoot 'tools\ApiSurface.cs')
if ($LASTEXITCODE -ne 0) { throw 'API surface verifier compilation failed' }
& (Join-Path $InternalDir 'ApiSurface.exe') $Assembly $GameDir (Join-Path $RuntimeRoot 'tests\api-1.2.txt')
if ($LASTEXITCODE -ne 0) { throw 'Previously shipped Runtime API changed' }
& (Join-Path $InternalDir 'ApiSurface.exe') $Assembly $GameDir (Join-Path $RuntimeRoot 'tests\api-1.3.txt')
if ($LASTEXITCODE -ne 0) { throw 'Previously shipped Runtime 1.3 API changed' }
& (Join-Path $InternalDir 'ApiSurface.exe') $Assembly $GameDir (Join-Path $RuntimeRoot 'tests\api-1.11.txt')
if ($LASTEXITCODE -ne 0) { throw 'Previously shipped Runtime 1.11 API changed' }
& (Join-Path $InternalDir 'ApiSurface.exe') $Assembly $GameDir (Join-Path $RuntimeRoot 'tests\api-1.24.txt')
if ($LASTEXITCODE -ne 0) { throw 'Previously shipped Runtime 1.24 API changed' }
& (Join-Path $InternalDir 'ApiSurface.exe') $Assembly $GameDir (Join-Path $RuntimeRoot 'tests\api-1.25.txt')
if ($LASTEXITCODE -ne 0) { throw 'Previously shipped Runtime 1.25 API changed' }
& (Join-Path $InternalDir 'ApiSurface.exe') $Assembly $GameDir (Join-Path $RuntimeRoot 'tests\api-1.26.txt')
if ($LASTEXITCODE -ne 0) { throw 'Previously shipped Runtime 1.26 API changed' }
$LegacyDirectory = Join-Path $InternalDir 'LegacyConsumer13'
New-Item -ItemType Directory -Force -Path $LegacyDirectory | Out-Null
$LegacyReference = Join-Path $LegacyDirectory 'JKRuntime.dll'
& $Compiler /nologo /target:library /langversion:5 "/out:$LegacyReference" (Join-Path $RuntimeRoot 'tests\LegacyReference13.cs')
if ($LASTEXITCODE -ne 0) { throw 'Frozen reference compilation failed' }
$FrozenSurface = @(Get-Content -LiteralPath (Join-Path $RuntimeRoot 'tests\api-1.3.txt'))
foreach ($LegacySignature in @(& (Join-Path $InternalDir 'ApiSurface.exe') $LegacyReference $GameDir)) {
    if ($FrozenSurface -notcontains $LegacySignature) { throw "Old consumer reference is not a shipped 1.3 signature: $LegacySignature" }
}
& $Compiler /nologo /target:exe /langversion:5 "/out:$(Join-Path $LegacyDirectory 'LegacyConsumer13.exe')" "/reference:$LegacyReference" (Join-Path $RuntimeRoot 'tests\LegacyConsumer13.cs')
if ($LASTEXITCODE -ne 0) { throw 'Old consumer fixture compilation failed' }
Copy-Item -LiteralPath $Assembly,$JumpKing,$MonoGame,$Language -Destination $LegacyDirectory -Force
& (Join-Path $LegacyDirectory 'LegacyConsumer13.exe')
if ($LASTEXITCODE -ne 0) { throw 'Unchanged old consumer failed against current Runtime' }
$Suites = @('UiTests', 'MenuTreeSessionTests', 'BindingPageTests', 'TextEntryTests', 'RuntimeTests', 'PreparationTests', 'DiscoveryTests', 'FoundationTests', 'SettingsFileTests', 'BackgroundWorkTests', 'OwnedPatchesTests', 'NativePerformanceTests', 'RunModifierTests', 'MouseInputTests', 'SharedKeyboardTests', 'PointerTests', 'SimulationTests', 'LifecycleSafetyTests', 'GeometryMechanicTests', 'ExtensibilityTests', 'JumpSlotTests', 'PerformanceDiagnosticsTests')
$Suites += 'MapPolicyTests'
$Suites += 'UiPageSessionTests'
$Suites += 'ManagerMapTests'
$Suites += 'MotionObservationTests'
$Suites += 'InteropTests'
$TestSources = @($Suites | ForEach-Object { Join-Path $RuntimeRoot "tests\$_.cs" })
$InteropFixture = Join-Path $InternalDir 'InteropProvider.dll'
& $Compiler /nologo /optimize+ /target:library /langversion:5 "/out:$InteropFixture" $References (Join-Path $RuntimeRoot 'tests/InteropProvider.cs')
if ($LASTEXITCODE -ne 0) { throw 'Independent interop provider compilation failed' }
$TestExecutable = Join-Path $InternalDir 'RuntimeChecks.exe'
& $Compiler /nologo /optimize+ /target:exe /langversion:5 /main:JKRuntime.TestHost "/out:$TestExecutable" "/reference:$InteropFixture" $References $Resources $Sources $TestSources (Join-Path $RuntimeRoot 'tests/TestHost.cs')
if ($LASTEXITCODE -ne 0) { throw 'Runtime checks compilation failed' }
foreach ($suite in $Suites) {
    $MainType = if ($suite -eq 'UiTests') { 'JKRuntime.UI.UiTests' } else { 'JKRuntime.' + $suite }
    $SuiteClock = [Diagnostics.Stopwatch]::StartNew()
    [string[]]$SuiteArguments = @(if ($suite -in @('PerformanceDiagnosticsTests', 'OwnedPatchesTests', 'NativePerformanceTests', 'MotionObservationTests', 'InteropTests')) { Join-Path $RepoRoot 'mods/subframe-charge/lib/0Harmony.dll' })
    & $TestExecutable $MainType @SuiteArguments
    if ($LASTEXITCODE -ne 0) { throw "$suite failed" }
    if ($suite -eq 'LifecycleSafetyTests') {
        & $TestExecutable $MainType drain
        if ($LASTEXITCODE -ne 0) { throw 'Command failure/cancellation isolation failed' }
    }
    Write-Host ('[TIME] Runtime/{0}: {1:F3}s' -f $suite, $SuiteClock.Elapsed.TotalSeconds)
}
& $Compiler /nologo /target:library /langversion:5 "/out:$(Join-Path $InternalDir 'RuntimeExample.Module.dll')" $References "/reference:$Assembly" (Join-Path $RuntimeRoot 'examples\RuntimeExample.cs')
if ($LASTEXITCODE -ne 0) { throw 'Runtime SDK example failed' }
& $Compiler /nologo /target:library /langversion:5 "/out:$(Join-Path $InternalDir 'UIExample.Module.dll')" $References "/reference:$Assembly" (Join-Path $RuntimeRoot 'examples\ExampleIntegration.cs')
if ($LASTEXITCODE -ne 0) { throw 'UIApi+ SDK example failed' }
& $Compiler /nologo /target:library /langversion:5 "/out:$(Join-Path $InternalDir 'UiPageExample.dll')" $References "/reference:$Assembly" (Join-Path $RuntimeRoot 'examples/ScopedPageExample.cs')
if ($LASTEXITCODE -ne 0) { throw 'Scoped UI page example failed' }
foreach ($Example in @('PreparationExample', 'StateExample', 'UiModExample', 'MotionObservationExample', 'InteropExample')) {
    & $Compiler /nologo /target:library /langversion:5 "/out:$(Join-Path $InternalDir ($Example + '.Module.dll'))" $References "/reference:$Assembly" (Join-Path $RuntimeRoot "examples\$Example.cs")
    if ($LASTEXITCODE -ne 0) { throw "$Example compilation failed" }
}
$DocumentationExamples = @((Join-Path $RuntimeRoot 'examples\PreparationExample.cs'), (Join-Path $RuntimeRoot 'examples\StateExample.cs'))
& $Compiler /nologo /target:exe /langversion:5 /main:JKRuntime.DocumentationExamplesTests "/out:$(Join-Path $InternalDir 'DocumentationExamplesTests.exe')" $References $Resources $Sources $DocumentationExamples (Join-Path $RuntimeRoot 'tests\DocumentationExamplesTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Documentation examples tests compilation failed' }
& (Join-Path $InternalDir 'DocumentationExamplesTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Documentation examples tests failed' }
foreach ($Example in @('RuntimeExample','UIExample','PreparationExample','StateExample','UiModExample')) {
    & (Join-Path $InternalDir 'PackageBuilder.exe') (Join-Path $InternalDir ($Example+'.Module.dll')) (Join-Path $InternalDir ($Example+'.dll')) $GameDir
    if ($LASTEXITCODE -ne 0) { throw "$Example SDK packaging failed" }
}
if (Test-Path -LiteralPath (Join-Path $UploadDir '0Harmony.dll')) { throw 'Unexpected Harmony in runtime payload' }
$SdkDirectory = Join-Path $InternalDir 'SDK'
New-Item -ItemType Directory -Force -Path $SdkDirectory | Out-Null
Copy-Item -LiteralPath $Assembly,(Join-Path $InternalDir 'JKRuntime.xml'),(Join-Path $InternalDir 'PackageBuilder.exe') -Destination $SdkDirectory -Force
Get-ChildItem -LiteralPath (Join-Path $RuntimeRoot 'sdk\template') -File | Copy-Item -Destination $SdkDirectory -Force
Copy-Item -LiteralPath (Join-Path $RuntimeRoot 'examples\UiModExample.cs') -Destination (Join-Path $SdkDirectory 'ModEntry.cs')
Copy-Item -LiteralPath (Join-Path $RuntimeRoot 'docs') -Destination $SdkDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $RuntimeRoot 'examples/ScopedPageExample.cs') -Destination $SdkDirectory -Force
Copy-Item -LiteralPath (Join-Path $RuntimeRoot 'examples') -Destination $SdkDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $RuntimeRoot 'CHANGELOG.md') -Destination $SdkDirectory -Force
& $Compiler /nologo /target:exe /langversion:5 /reference:System.Web.Extensions.dll /reference:System.Xml.Linq.dll "/out:$(Join-Path $InternalDir 'DocumentationIndex.exe')" (Join-Path $RuntimeRoot 'tools\DocumentationIndex.cs')
if ($LASTEXITCODE -ne 0) { throw 'Documentation index compilation failed' }
$DocumentationIndex = Join-Path $InternalDir 'public-api.json'
& (Join-Path $InternalDir 'DocumentationIndex.exe') $Assembly $GameDir (Join-Path $InternalDir 'JKRuntime.xml') $DocumentationIndex
if ($LASTEXITCODE -ne 0) { throw 'Documentation reflection failed' }
& python (Join-Path $RuntimeRoot 'tests\test_docs.py')
if ($LASTEXITCODE -ne 0) { throw 'Documentation verifier tests failed' }
& python (Join-Path $RuntimeRoot 'tools\check_docs.py') --source $RuntimeRoot --sdk $SdkDirectory --api-index $DocumentationIndex
if ($LASTEXITCODE -ne 0) { throw 'Source/SDK documentation validation failed' }
$MigrationOutput = Join-Path $BuildRoot ('_INTERNAL/example-' + [Guid]::NewGuid().ToString('N'))
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $SdkDirectory 'examples/less-auto-equipping/build.ps1') -SdkDirectory $SdkDirectory -GameDir $GameDir -HarmonyPath (Join-Path $RepoRoot 'mods/subframe-charge/lib/0Harmony.dll') -OutputDirectory $MigrationOutput
if ($LASTEXITCODE -ne 0) { throw 'Detached LessAutoEquipping migration example failed' }
$SdkSmoke = Join-Path $InternalDir 'SDKSmoke'
New-Item -ItemType Directory -Force -Path $SdkSmoke | Out-Null
Get-ChildItem -LiteralPath $SdkDirectory -File | Copy-Item -Destination $SdkSmoke -Force
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $SdkSmoke 'build.ps1') -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Standalone SDK example failed' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RuntimeRoot 'tests\PublishBuildTests.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Publication safety checks failed' }
$Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Assembly).Hash.ToLowerInvariant()
# Preserve diagnostic history/backups. Only verified tool files replace stable outputs.
Get-ChildItem -LiteralPath $InternalDir -File | Copy-Item -Destination $StableInternal -Force
. (Join-Path $RuntimeRoot 'tools\publish-build.ps1')
Publish-RuntimeBuild -BuildRoot $BuildRoot -CandidateDll $Assembly -CandidateSdk $SdkDirectory
if (Test-Path -LiteralPath $ObsoleteAlias -PathType Leaf) {
    Move-Item -LiteralPath $ObsoleteAlias -Destination (Join-Path $InternalDir 'old-ui-alias.dll')
}
Write-Host "[OK] JK Runtime (including UIApi+): $PublishedAssembly"
Write-Host "[OK] SHA-256: $Hash"
if ($null -ne $Session) { $Session['jk-runtime'] = @{ Input=$InputKey; Output=(Get-ModContentKey $DependencyArtifacts) } }
