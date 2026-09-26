param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$OriginalDll
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
$workshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop\content\1061090'
$reviewedTextHash = '115FD1FB5884EED3E022541099AA7324432C3E2401B092DA9848366181C227E8'
if (!$OriginalDll) {
    $OriginalDll = Join-Path $workshop '3275249832\MoreTextOptions.dll'
    # A reviewed compatibility install replaces the subscribed DLL and retains
    # its original in this backup tree. Test that exact original, without
    # downgrading the live mod or accepting an unreviewed fixture.
    if ((Test-Path -LiteralPath $OriginalDll) -and (Get-FileHash -LiteralPath $OriginalDll).Hash -ne $reviewedTextHash) {
        $textBackupRoot = Join-Path $repo 'build/more-text-options-compat'
        if (Test-Path -LiteralPath $textBackupRoot) {
            $textOriginalBackup = Get-ChildItem -LiteralPath $textBackupRoot -Directory -Filter 'backup-*' |
                ForEach-Object { Join-Path $_.FullName 'MoreTextOptions.dll' } |
                Where-Object { (Test-Path -LiteralPath $_ -PathType Leaf) -and (Get-FileHash -LiteralPath $_).Hash -eq $reviewedTextHash } |
                Select-Object -First 1
            if ($textOriginalBackup) { $OriginalDll = $textOriginalBackup; Write-Host "[FIXTURE] Reviewed MoreTextOptions backup: $OriginalDll" }
        }
    }
}
if (!(Test-Path -LiteralPath $OriginalDll)) { Write-Host '[SKIP] MoreTextOptions integration fixture: mod not installed'; exit 0 }
if ((Get-FileHash -LiteralPath $OriginalDll).Hash -ne $reviewedTextHash) {
    throw 'Integration tests require the reviewed original MoreTextOptions DLL; pass -OriginalDll explicitly.'
}
$internal = Join-Path $repo 'build\jk-runtime\_INTERNAL'
Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $internal -Force
$tests = Join-Path $internal 'MoreTextOptionsTests.exe'
$sources = @(Get-ChildItem -LiteralPath "$PSScriptRoot\src" -Recurse -Filter '*.cs' | ForEach-Object FullName)
$refs = @("/reference:$GameDir\JumpKing.exe", "/reference:$GameDir\MonoGame.Framework.dll", "/reference:$GameDir\LanguageJK.dll", '/reference:System.Web.Extensions.dll', '/reference:System.Xml.Linq.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll')
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /optimize+ /target:exe /langversion:5 /main:JKRuntime.MoreTextOptionsTests "/out:$tests" $refs $sources "$PSScriptRoot\tests\MoreTextOptionsTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'MoreTextOptions tests failed to compile' }
$seen = @{}
foreach ($file in (Get-ChildItem -LiteralPath $workshop -Recurse -Filter '0Harmony.dll' | Sort-Object FullName)) {
    $version = [Reflection.AssemblyName]::GetAssemblyName($file.FullName).Version.ToString()
    if ($seen.ContainsKey($version) -or $version -notin @('2.2.2.0','2.3.3.0','2.3.5.0','2.3.6.0')) { continue }
    $seen[$version] = $file.FullName
    & $tests $file.FullName $OriginalDll
    if ($LASTEXITCODE -ne 0) { throw "MoreTextOptions integration failed: $version" }
}
if ($seen.Count -ne 4) { throw 'Expected all four installed Harmony versions for this compatibility audit.' }
& $tests $seen['2.3.5.0'] $OriginalDll 'ordering'
if ($LASTEXITCODE -ne 0) { throw 'Ordering refusal test failed' }
& $tests $seen['2.3.5.0'] $OriginalDll 'mixed' $seen['2.2.2.0']
if ($LASTEXITCODE -ne 0) { throw 'Mixed-engine refusal test failed' }
& $tests $seen['2.3.5.0'] $OriginalDll 'unknown'
if ($LASTEXITCODE -ne 0) { throw 'Unknown-build refusal test failed' }
