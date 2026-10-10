param([string]$GameDir)
$ErrorActionPreference = 'Stop'
$overlayTestRepo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$overlayTestRoot = Join-Path $overlayTestRepo ('build/overlay-plus/_INTERNAL/install-test-'+[Guid]::NewGuid().ToString('N'))
$overlayTestGame = Join-Path $overlayTestRoot 'steamapps/common/Jump King'
$overlayTestMods = Join-Path $overlayTestGame 'Content/JKMods'
$overlayTestData = Join-Path $overlayTestGame 'Content/OverlayPlus'
New-Item -ItemType Directory -Force -Path $overlayTestMods,$overlayTestData | Out-Null
[IO.File]::WriteAllText((Join-Path $overlayTestMods 'WardrobePlus.dll'),'unrelated-mod')
[IO.File]::WriteAllText((Join-Path $overlayTestData 'Layouts.xml'),'user-layouts')
$overlayCalls = [Collections.Generic.List[object]]::new()
$overlayBusy = $false
$overlayVerifyFailure = $false
function powershell.exe { $overlayCalls.Add(@($args)); $global:LASTEXITCODE = [int]$overlayVerifyFailure }
function Get-Process { if ($overlayBusy) { return [pscustomobject]@{ ProcessName='JumpKing' } }; return @() }
function Assert-OverlayInstall([bool]$Value,[string]$Message) { if (-not $Value) { throw $Message } }
$overlayInstaller = Join-Path $overlayTestRepo 'mods/jk-runtime/install-release.ps1'
& $overlayInstaller -GameDir $overlayTestGame -Include overlay-plus -OnlyRequested
Assert-OverlayInstall ($overlayCalls.Count -eq 1 -and $overlayCalls[0] -contains '-EncodedCommand' -and $overlayCalls[0] -contains '-OutputFormat' -and $overlayCalls[0] -contains 'Text') 'Focused install must use an isolated text-output verification command'
$overlayEncodedIndex = [Array]::IndexOf($overlayCalls[0], '-EncodedCommand')
$overlayVerifyCommand = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($overlayCalls[0][$overlayEncodedIndex + 1]))
Assert-OverlayInstall ($overlayVerifyCommand.Contains("-Mod @('overlay-plus')") -and $overlayVerifyCommand.Contains('-Integration') -and $overlayVerifyCommand.Contains($overlayTestGame)) 'Focused install must validate the selected module and runtime against the requested game'
foreach ($overlayName in @('OverlayPlus.dll','OverlayPlusApi.dll','0Harmony.dll','JKRuntime.dll','OverlayPlus.THIRD_PARTY_NOTICES.md')) {
    Assert-OverlayInstall (Test-Path -LiteralPath (Join-Path $overlayTestMods $overlayName)) "Missing installed payload: $overlayName"
}
Assert-OverlayInstall ([IO.File]::ReadAllText((Join-Path $overlayTestMods 'WardrobePlus.dll')) -eq 'unrelated-mod') 'Focused installation modified an unrelated mod'
Assert-OverlayInstall ([IO.File]::ReadAllText((Join-Path $overlayTestData 'Layouts.xml')) -eq 'user-layouts') 'Installation modified user layouts'
$overlayWorkshop = Join-Path $overlayTestRoot 'steamapps/workshop/content/1061090/existing-overlay'
New-Item -ItemType Directory -Path $overlayWorkshop | Out-Null
$overlayLocalDll = Join-Path $overlayTestMods 'OverlayPlus.dll'
$overlayWorkshopDll = Join-Path $overlayWorkshop 'OverlayPlus.dll'
[IO.File]::Move($overlayLocalDll,$overlayWorkshopDll)
& $overlayInstaller -GameDir $overlayTestGame -Include overlay-plus -OnlyRequested
Assert-OverlayInstall (-not (Test-Path -LiteralPath $overlayLocalDll)) 'Existing Workshop mod was duplicated locally'
Assert-OverlayInstall (Test-Path -LiteralPath (Join-Path $overlayWorkshop 'OverlayPlusApi.dll')) 'Workshop dependencies were not installed together'
[IO.File]::WriteAllText($overlayLocalDll,'duplicate')
$overlayFailed=$false
try { & $overlayInstaller -GameDir $overlayTestGame -Include overlay-plus -OnlyRequested } catch { $overlayFailed=$true }
Assert-OverlayInstall $overlayFailed 'Duplicate active mod was not rejected'
Remove-Item -LiteralPath $overlayLocalDll
[IO.File]::WriteAllText($overlayWorkshopDll,'rollback-overlay')
[IO.File]::WriteAllText((Join-Path $overlayTestMods 'JKRuntime.dll'),'rollback-runtime')
$overlayApi = Join-Path $overlayWorkshop 'OverlayPlusApi.dll'
$overlayLock = [IO.File]::Open($overlayApi,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$overlayFailed=$false
try { & $overlayInstaller -GameDir $overlayTestGame -Include overlay-plus -OnlyRequested } catch { $overlayFailed=$true } finally { $overlayLock.Dispose() }
Assert-OverlayInstall $overlayFailed 'Locked dependency was not rejected'
Assert-OverlayInstall ([IO.File]::ReadAllText($overlayWorkshopDll) -eq 'rollback-overlay') 'Failed installation changed Overlay payload'
Assert-OverlayInstall ([IO.File]::ReadAllText((Join-Path $overlayTestMods 'JKRuntime.dll')) -eq 'rollback-runtime') 'Failed installation changed runtime'
function Copy-Item {
    param([string]$LiteralPath,[string]$Destination,[switch]$Force)
    if ($LiteralPath -like '*UPLOAD_TO_WORKSHOP*OverlayPlus.dll' -and [IO.Path]::GetFullPath($Destination) -eq [IO.Path]::GetFullPath($overlayWorkshopDll)) { throw 'Injected failure after runtime replacement' }
    Microsoft.PowerShell.Management\Copy-Item @PSBoundParameters
}
$overlayFailed=$false
try { & $overlayInstaller -GameDir $overlayTestGame -Include overlay-plus -OnlyRequested } catch { $overlayFailed=$true }
Assert-OverlayInstall $overlayFailed 'Injected mid-transaction failure was not observed'
Assert-OverlayInstall ([IO.File]::ReadAllText((Join-Path $overlayTestMods 'JKRuntime.dll')) -eq 'rollback-runtime') 'Runtime replacement was not rolled back'
Assert-OverlayInstall ([IO.File]::ReadAllText($overlayWorkshopDll) -eq 'rollback-overlay') 'Overlay replacement was not rolled back'
$overlayBusy=$true;$overlayFailed=$false
try { & $overlayInstaller -GameDir $overlayTestGame -Include overlay-plus -OnlyRequested } catch { $overlayFailed=$true }
Assert-OverlayInstall $overlayFailed 'Running game must block DLL replacement'
$overlayBusy=$false;$overlayVerifyFailure=$true;$overlayFailed=$false
try { & $overlayInstaller -GameDir $overlayTestGame -Include overlay-plus -OnlyRequested } catch { $overlayFailed=$true }
Assert-OverlayInstall $overlayFailed 'Failed validation must block installation'
Write-Host '[OK] Overlay installer: selected scope, local/Workshop placement, preserved data, rollback, dependency/duplicate preflight, running-game and validation guards'
