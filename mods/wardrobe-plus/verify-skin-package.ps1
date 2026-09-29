param([Parameter(Mandatory=$true)][string]$Package, [string]$WorldsmithDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King Workshop')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core' -or [IntPtr]::Size -ne 4) {
    & "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -File $PSCommandPath -Package $Package -WorldsmithDir $WorldsmithDir
    exit $LASTEXITCODE
}
# exercise the installed uploader without Steam initialization, UI or upload
$skinRoot = (Resolve-Path -LiteralPath $Package).Path
Add-Type -AssemblyName System.Windows.Forms
$skinAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $WorldsmithDir 'JKWorldsmith.exe'))
$skinList = New-Object Windows.Forms.ListView
try {
    $null = $skinList.Handle
    $null = $skinAssembly.GetType('WorkshopJK.ConsoleManager', $true).GetConstructors()[0].Invoke([object[]](,$skinList.PSObject.BaseObject))
    $skinSteamType = $skinAssembly.GetType('WorkshopJK.SteamData', $true)
    $skinSteamType.GetField('_instance', [Reflection.BindingFlags]'Static,NonPublic').SetValue($null,[Runtime.Serialization.FormatterServices]::GetUninitializedObject($skinSteamType))
    $skinManagerType = $skinAssembly.GetType('WorkshopJK.UploadManager', $true)
    $skinManager = [Activator]::CreateInstance($skinManagerType)
    if (-not $skinManager.HasLoadedFolderUGC($skinRoot)) { throw 'Worldsmith could not classify the skin package' }
    $skinType = $skinManager.UGCType
    if ($skinType.Name -notin @('Skin','Set')) { throw "Expected Skin/Set classification, got $($skinType.FullName)" }
    $skinFile = if ($skinType.Name -eq 'Skin') { 'cosmetic_settings.xml' } else { 'set_settings.xml' }
    if (-not $skinManager.Validate($skinType, (Join-Path $skinRoot $skinFile))) { throw 'Worldsmith rejected native skin settings' }
    $skinValue = $skinType.GetConstructor([type[]]@([string])).Invoke([object[]](,$skinRoot))
    if (-not $skinManagerType.GetMethod('ValidateFiles').MakeGenericMethod($skinType).Invoke($skinManager,[object[]](,$skinValue.PSObject.BaseObject))) { throw 'Worldsmith rejected skin assets' }
    [pscustomobject]@{Valid=$true;Type=$skinType.Name;Uploaded=$false;Package=$skinRoot} | ConvertTo-Json
} finally { $skinList.Dispose() }
