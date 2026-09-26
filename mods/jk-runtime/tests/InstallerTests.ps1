param([string]$GameDir)
$ErrorActionPreference = 'Stop'
$testRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$testRoot = Join-Path $testRepo ('build\jk-runtime\_INTERNAL\installer-test-'+[Guid]::NewGuid().ToString('N'))
$testGame = Join-Path $testRoot 'steamapps\common\Jump King'
$testMods = Join-Path $testGame 'Content\JKMods'
New-Item -ItemType Directory -Path $testMods -Force | Out-Null
# Only the installer transaction is under test. Full verification is run by
# check-mods outside this test; never recurse into it or target the real game.
$testVerificationCalls = [Collections.Generic.List[object]]::new()
function powershell.exe { $testVerificationCalls.Add(@($args)); $global:LASTEXITCODE = 0 }
function Get-Process { return @() }
function Assert-Installer([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }
$testInstaller = Join-Path $testRepo 'mods\jk-runtime\install-release.ps1'
foreach ($testFile in @('UIApiPlus.dll','CasualJumping.dll','MoreItems.dll','Jetpack.dll','HammerKing.dll','ScreenSolver.dll')) {
    [IO.File]::WriteAllText((Join-Path $testMods $testFile),'old-'+$testFile)
}
$testSettings = Join-Path $testMods 'UIApiPlus.Settings.xml'
[IO.File]::WriteAllText($testSettings,'<UIApiSettings />')
$testWardrobeDirectory = Join-Path $testGame 'Content\WardrobePlus'
New-Item -ItemType Directory -Path $testWardrobeDirectory | Out-Null
$testWardrobeSettings = Join-Path $testWardrobeDirectory 'Settings.xml'
[IO.File]::WriteAllText($testWardrobeSettings,'user-outfits-and-fits')
& $testInstaller -GameDir $testGame -Include @('mega-gameplay-expansion','mega-mapping-expansion','wardrobe-plus') -RemoveLocalScreenSolver
Assert-Installer ($testVerificationCalls[0] -contains '-Integration') 'Release install must request package/rollback integration checks, not only the fast tier'
Assert-Installer ($testVerificationCalls[0] -contains '-OutputFormat' -and $testVerificationCalls[0] -contains 'Text') 'Native diagnostics must use text rather than mixed CLIXML serialization'
Assert-Installer (Test-Path -LiteralPath (Join-Path $testMods 'JKRuntime.dll')) 'Runtime not installed in fixture'
Assert-Installer (-not (Test-Path -LiteralPath (Join-Path $testMods 'UIApiPlus.dll'))) 'Old UI DLL was not removed'
Assert-Installer (-not (Test-Path -LiteralPath (Join-Path $testMods 'Jetpack.dll'))) 'Superseded Jetpack DLL was not removed'
Assert-Installer (-not (Test-Path -LiteralPath (Join-Path $testMods 'HammerKing.dll'))) 'Superseded Hammer King DLL was not removed'
Assert-Installer (-not (Test-Path -LiteralPath (Join-Path $testMods 'ScreenSolver.dll'))) 'Requested local Screen Solver removal failed'
foreach ($testFile in @('MegaGameplayExpansion.dll','MegaMappingExpansion.dll','MegaMappingApi.dll','WardrobePlus.dll','0Harmony.dll','MegaMappingExpansion.THIRD_PARTY_NOTICES.md')) {
    Assert-Installer (Test-Path -LiteralPath (Join-Path $testMods $testFile)) "Missing local expansion payload: $testFile"
}
Assert-Installer ([IO.File]::ReadAllText($testSettings) -eq '<UIApiSettings />') 'User settings changed during DLL installation'
Assert-Installer ([IO.File]::ReadAllText($testWardrobeSettings) -eq 'user-outfits-and-fits') 'Wardrobe user data changed during installation'
Assert-Installer ((Get-FileHash -LiteralPath (Join-Path $testMods 'WardrobePlus.dll')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $testRepo 'build\wardrobe-plus\UPLOAD_TO_WORKSHOP\WardrobePlus.dll')).Hash) 'Installed Wardrobe payload differs from the verified package'
foreach ($testFile in @('JKRuntime.dll','CasualJumping.dll')) { [IO.File]::WriteAllText((Join-Path $testMods $testFile),'rollback-'+$testFile) }
$testLock = [IO.File]::Open((Join-Path $testMods 'MoreItems.dll'),[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$testFailed = $false
try { & $testInstaller -GameDir $testGame } catch { $testFailed = $true } finally { $testLock.Dispose() }
Assert-Installer $testFailed 'Locked destination should reject installation'
foreach ($testFile in @('JKRuntime.dll','CasualJumping.dll')) {
    Assert-Installer ([IO.File]::ReadAllText((Join-Path $testMods $testFile)) -eq ('rollback-'+$testFile)) 'Earlier replacement was not rolled back'
}
$testDuplicate = Join-Path $testRoot 'steamapps\workshop\content\1061090\duplicate'
New-Item -ItemType Directory -Path $testDuplicate -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $testDuplicate 'CasualJumping.dll'),'duplicate')
$testFailed = $false
try { & $testInstaller -GameDir $testGame } catch { $testFailed = $true }
Assert-Installer $testFailed 'Duplicate mod directories should reject installation'
Assert-Installer ([IO.File]::ReadAllText((Join-Path $testMods 'JKRuntime.dll')) -eq 'rollback-JKRuntime.dll') 'Duplicate preflight changed runtime'
Write-Host '[OK] Isolated installer: coordinated replacement, old DLL backups/removal, settings preservation, locked-file rollback, duplicate preflight'

# The expanded SFC payload must include its scheduler's dependency in a
# Workshop install, without creating another local copy or changing settings.
$testSfc = Join-Path $testRoot 'steamapps/workshop/content/1061090/sfc-fixture'
New-Item -ItemType Directory -Path $testSfc -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $testSfc 'SubframeCharge.dll'),'old-sfc')
[IO.File]::WriteAllText((Join-Path $testSfc 'SubframeCharge.Settings.xml'),'preserved-sfc-settings')
& $testInstaller -GameDir $testGame -Include subframe-charge -OnlyRequested
Assert-Installer (Test-Path -LiteralPath (Join-Path $testSfc '0Harmony.dll')) 'SFC shared dependency missing from Workshop install'
Assert-Installer (Test-Path -LiteralPath (Join-Path $testSfc 'SubframeCharge.THIRD_PARTY_NOTICES.md')) 'SFC dependency notice missing'
Assert-Installer (-not (Test-Path -LiteralPath (Join-Path $testMods 'SubframeCharge.dll'))) 'SFC was duplicated locally'
Assert-Installer ([IO.File]::ReadAllText((Join-Path $testSfc 'SubframeCharge.Settings.xml')) -eq 'preserved-sfc-settings') 'SFC installation changed settings'
$testSfcHash = (Get-FileHash -LiteralPath (Join-Path $testSfc 'SubframeCharge.dll')).Hash
[IO.File]::WriteAllText((Join-Path $testSfc '0Harmony.dll'),'different-shared-engine')
$testFailed = $false
try { & $testInstaller -GameDir $testGame -Include subframe-charge -OnlyRequested } catch { $testFailed = $true }
Assert-Installer $testFailed 'SFC installer overwrote a different shared Harmony engine'
Assert-Installer ((Get-FileHash -LiteralPath (Join-Path $testSfc 'SubframeCharge.dll')).Hash -eq $testSfcHash) 'Dependency preflight changed SFC before refusal'
Write-Host '[OK] SFC Workshop dependency, settings preservation, single active copy and shared-engine refusal'

$testCamera = Join-Path $testRoot 'steamapps/workshop/content/1061090/camera-fixture'
New-Item -ItemType Directory -Path $testCamera -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $testCamera 'SmoothCamera.dll'),'old-camera')
[IO.File]::WriteAllText((Join-Path $testCamera 'SmoothCamera.Settings.xml'),'preserved-camera-settings')
& $testInstaller -GameDir $testGame -Include smooth-camera -OnlyRequested
Assert-Installer ($testVerificationCalls[$testVerificationCalls.Count - 1] -contains '-OutputFormat' -and $testVerificationCalls[$testVerificationCalls.Count - 1] -contains 'Text') 'Encoded focused verification must preserve native text diagnostics'
Assert-Installer (Test-Path -LiteralPath (Join-Path $testCamera '0Harmony.dll')) 'Camera shared dependency missing from Workshop install'
Assert-Installer (Test-Path -LiteralPath (Join-Path $testCamera 'SmoothCamera.THIRD_PARTY_NOTICES.md')) 'Camera dependency notice missing'
Assert-Installer (-not (Test-Path -LiteralPath (Join-Path $testMods 'SmoothCamera.dll'))) 'Camera was duplicated locally'
Assert-Installer ([IO.File]::ReadAllText((Join-Path $testCamera 'SmoothCamera.Settings.xml')) -eq 'preserved-camera-settings') 'Camera installation changed settings'
Write-Host '[OK] Smooth Camera coordinated Workshop installation and settings preservation'

# A UI-only Runtime patch must not rebuild or overwrite unrelated installed mods.
$testPreserved = @{}
Get-ChildItem -LiteralPath $testMods,$testSfc,$testCamera -File | Where-Object Name -ne 'JKRuntime.dll' | ForEach-Object {
    $testPreserved[$_.FullName] = (Get-FileHash -LiteralPath $_.FullName).Hash
}
& $testInstaller -GameDir $testGame -Include jk-runtime -OnlyRequested
$testArgs = $testVerificationCalls[$testVerificationCalls.Count - 1]
$testCommand = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($testArgs[$testArgs.Count - 1]))
Assert-Installer ($testCommand -match "-Mod @\('jk-runtime'\)") 'Runtime-only install did not request focused Runtime checks'
foreach ($testPath in $testPreserved.Keys) {
    Assert-Installer ((Get-FileHash -LiteralPath $testPath).Hash -eq $testPreserved[$testPath]) "Runtime-only install changed unrelated file: $testPath"
}
Assert-Installer ((Get-FileHash -LiteralPath (Join-Path $testMods 'JKRuntime.dll')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $testRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll')).Hash) 'Runtime-only install did not replace Runtime'
Write-Host '[OK] Runtime-only checks and replacement preserve unrelated DLLs and settings'
