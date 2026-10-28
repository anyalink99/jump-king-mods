param([string]$GameDir, [string]$RuntimeAssembly, [string]$HarmonyAssembly, [string]$FixtureAssembly)
$ErrorActionPreference = 'Stop'
foreach ($Name in @('MonoGame.Framework.dll', 'LanguageJK.dll', 'JumpKing.exe')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $GameDir $Name)) | Out-Null
}
$Runtime = [Reflection.Assembly]::LoadFrom($RuntimeAssembly)
$Harmony = [Reflection.Assembly]::LoadFrom($HarmonyAssembly)
$Fixture = [Reflection.Assembly]::LoadFrom($FixtureAssembly).GetType('HarmonyFixture')
$Target = $Fixture.GetMethod('Target')
$Postfix = $Fixture.GetMethod('Postfix')
$HarmonyType = $Harmony.GetType('HarmonyLib.Harmony', $true)
$MethodType = $Harmony.GetType('HarmonyLib.HarmonyMethod', $true)
$Owner = [Activator]::CreateInstance($HarmonyType, [object[]]@('jk.runtime.tests.inspection'))
$PatchMethod = [Activator]::CreateInstance($MethodType, [object[]]@($Postfix))
$Patch = $HarmonyType.GetMethod('Patch')
$Arguments = New-Object object[] $Patch.GetParameters().Length
$Arguments[0] = $Target
$Arguments[2] = $PatchMethod
$Patch.Invoke($Owner, $Arguments) | Out-Null
$ValueBefore = $Target.Invoke($null, $null)
$Inspector = $Runtime.GetType('JKRuntime.RuntimeDiagnostics', $true).GetMethod('InspectHarmony', [Reflection.BindingFlags]'Static,NonPublic')
$Report = $Inspector.Invoke($null, [object[]]@($Harmony))
if ($Report.ContainsKey('inspectionError')) { throw $Report['inspectionError'] }
$Owners = @($Report['patches'] | Where-Object { $_.owner -eq 'jk.runtime.tests.inspection' })
if ($Owners.Count -ne 1 -or $ValueBefore -ne 8 -or $Target.Invoke($null, $null) -ne 8) { throw 'Harmony inspection lost metadata or changed a patch.' }
Write-Host "[OK] Harmony $($Harmony.GetName().Version): real patch owner detected; inspector leaves the patch intact."
