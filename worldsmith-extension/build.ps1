param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$WorldsmithDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King Workshop',
    [switch]$DownloadedPackageChecks,
    [switch]$SkipPortablePromotion
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
$buildRoot = Join-Path $repo 'build/_work/worldsmith-extension'
$outputRoot = Join-Path $repo 'build/worldsmith-extension'
$package = Join-Path $buildRoot ('package-' + [Guid]::NewGuid().ToString('N'))
$portable = Join-Path $outputRoot 'PORTABLE'
$harmony = Join-Path $repo 'mods/mega-mapping-expansion/lib/0Harmony.dll'
$mapping = Join-Path $repo 'build/mega-mapping-expansion/_INTERNAL'
foreach ($file in @($csc,$harmony,"$WorldsmithDir/JKWorldsmith.exe","$WorldsmithDir/Steamworks.NET.dll","$mapping/SceneCacheCompiler.exe")) {
    if (-not (Test-Path -LiteralPath $file)) { throw "Missing build input: $file. Build mega-mapping-expansion first and provide -WorldsmithDir for the editor." }
}
New-Item -ItemType Directory -Force -Path $package,"$package/mapping" | Out-Null
$refs = @('/r:System.Drawing.dll','/r:System.Xml.Linq.dll','/r:System.ServiceModel.dll','/r:System.Xaml.dll',"/r:$WorldsmithDir/Steamworks.NET.dll","/r:$harmony")
foreach ($name in @('PresentationFramework','PresentationCore','WindowsBase')) { $refs += "/r:$framework/WPF/$name.dll" }
$updateRefs = @('/r:System.Web.Extensions.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
$refs += $updateRefs
$sources = @(Get-ChildItem "$PSScriptRoot/src" -Recurse -File -Filter '*.cs' | Where-Object FullName -ne (Join-Path $PSScriptRoot 'src/Bootstrap/Launcher.cs') | Sort-Object FullName | ForEach-Object FullName)
$boundedLog = Join-Path $repo 'mods/jk-runtime/sdk/BoundedTextLog.cs'
$sources += $boundedLog
& $csc /nologo /preferreduilang:en-US /utf8output /target:library /platform:x64 /langversion:5 /optimize+ "/out:$package/WorldsmithExtension.Engine.dll" $refs $sources
if ($LASTEXITCODE -ne 0) { throw 'Engine compilation failed' }
$launcherSources = @(
    'Updates/Updates.cs',
    'Infrastructure/WorkerProcess.cs',
    'Bootstrap/Launcher.cs',
    'Infrastructure/Files.cs',
    'Building/Layout.cs',
    'Bootstrap/InterfaceLanguage.cs',
    'Bootstrap/ReleaseInfo.cs'
) | ForEach-Object { Join-Path "$PSScriptRoot/src" $_ }
& $csc /nologo /preferreduilang:en-US /utf8output /target:winexe /platform:x64 /langversion:5 /optimize+ "/out:$package/WorldsmithExtension.exe" /r:System.Windows.Forms.dll /r:System.Xml.Linq.dll $updateRefs $launcherSources
if ($LASTEXITCODE -ne 0) { throw 'Launcher compilation failed' }
& $csc /nologo /preferreduilang:en-US /utf8output /target:exe /platform:x64 /langversion:5 /optimize+ "/out:$package/WorldsmithExtension.Worker.exe" /r:System.Windows.Forms.dll /r:System.Xml.Linq.dll $updateRefs $launcherSources
if ($LASTEXITCODE -ne 0) { throw 'Worker compilation failed' }
Copy-Item -LiteralPath $harmony -Destination $package
Copy-Item -LiteralPath "$PSScriptRoot/WorldsmithExtension.exe.config" -Destination $package
Copy-Item -LiteralPath "$PSScriptRoot/WorldsmithExtension.exe.config" -Destination "$package/WorldsmithExtension.Worker.exe.config"
Copy-Item -LiteralPath "$mapping/SceneCacheCompiler.exe","$mapping/MegaMappingApi.dll","$mapping/JKRuntime.dll" -Destination "$package/mapping"
Copy-Item -LiteralPath "$PSScriptRoot/README.md" -Destination $package
Copy-Item -LiteralPath "$PSScriptRoot/CHANGELOG.md" -Destination $package
if (Test-Path "$PSScriptRoot/docs") { Copy-Item -LiteralPath "$PSScriptRoot/docs" -Destination $package -Recurse }
Copy-Item -LiteralPath "$repo/mods/mega-mapping-expansion/THIRD_PARTY_NOTICES.md" -Destination "$package/mapping"
if (Test-Path "$PSScriptRoot/THIRD_PARTY_NOTICES.md") { Copy-Item -LiteralPath "$PSScriptRoot/THIRD_PARTY_NOTICES.md" -Destination $package }
$tests = Join-Path $buildRoot 'WorldsmithExtensionTests.exe'
$coreSources = @(
    'Infrastructure/Files.cs',
    'Building/Layout.cs',
    'Bootstrap/InterfaceLanguage.cs',
    'Bootstrap/ReleaseInfo.cs',
    'Building/Collision.cs',
    'Workshop/Publisher.cs',
    'Infrastructure/Operations.cs',
    'Infrastructure/ErrorDetails.cs',
    'Workshop/PublishSession.cs',
    'Building/BuildPlan.cs',
    'Project/ModInstallation.cs',
    'Project/TestBuild.cs',
    'Native/NativeMembers.cs',
    'Building/BuildReceipt.cs',
    'Infrastructure/WorkerProcess.cs',
    'Building/ModPackage.cs',
    'Project/ProjectFormat.cs',
    'Project/PackedAssets.cs',
    'Updates/Updates.cs',
    'News/NewsFeed.cs',
    'Workshop/WorkshopLibrary.cs'
) | ForEach-Object { Join-Path "$PSScriptRoot/src" $_ }
$testSources = Get-ChildItem "$PSScriptRoot/tests" -Filter '*.cs' | Where-Object Name -ne 'NativeContracts.cs' | ForEach-Object FullName
$coreSources += $boundedLog
$coreSources += Join-Path $PSScriptRoot 'src/Infrastructure/GeneratedHistory.cs'
& $csc /nologo /preferreduilang:en-US /utf8output /target:exe /platform:x64 /langversion:5 "/out:$tests" /r:System.Drawing.dll /r:System.Xml.Linq.dll /r:System.ServiceModel.dll "/r:$WorldsmithDir/Steamworks.NET.dll" $updateRefs $coreSources $testSources
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
if (-not (Test-Path "$buildRoot/Steamworks.NET.dll") -or (Get-FileHash "$buildRoot/Steamworks.NET.dll").Hash -ne (Get-FileHash "$WorldsmithDir/Steamworks.NET.dll").Hash) { Copy-Item -LiteralPath "$WorldsmithDir/Steamworks.NET.dll" -Destination $buildRoot -Force }
& $tests
if ($LASTEXITCODE -ne 0) { throw 'Focused checks failed' }
& "$package/WorldsmithExtension.Worker.exe" --worldsmith $WorldsmithDir --check
if ($LASTEXITCODE -ne 0) { throw 'Worldsmith patch contracts failed' }
$nativeTests = Join-Path $buildRoot 'NativeContracts.exe'
if (-not (Test-Path "$buildRoot/0Harmony.dll") -or (Get-FileHash "$buildRoot/0Harmony.dll").Hash -ne (Get-FileHash $harmony).Hash) { Copy-Item -LiteralPath $harmony -Destination $buildRoot -Force }
$nativeTestSources = @((Join-Path $PSScriptRoot 'tests/NativeContracts.cs')) + @(Get-ChildItem "$PSScriptRoot/tests/native" -File -Filter '*.cs' | ForEach-Object FullName)
& $csc /nologo /preferreduilang:en-US /utf8output /target:exe /platform:x64 /langversion:5 "/out:$nativeTests" /r:System.Drawing.dll /r:System.ServiceModel.dll /r:System.Xaml.dll "/r:$framework/WPF/PresentationFramework.dll" "/r:$framework/WPF/PresentationCore.dll" "/r:$framework/WPF/WindowsBase.dll" "/r:$harmony" $nativeTestSources
if ($LASTEXITCODE -ne 0) { throw 'Native test compilation failed' }
& $nativeTests $WorldsmithDir $package
if ($LASTEXITCODE -ne 0) { throw 'Installed editor behavior checks failed' }
if ($DownloadedPackageChecks) {
    & "$PSScriptRoot/tests/DownloadedPackage.Tests.ps1" -Package $package -WorldsmithDir $WorldsmithDir
    if (-not $?) { throw 'Downloaded package launch checks failed' }
}
$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo("$package/WorldsmithExtension.exe").ProductVersion
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[a-z0-9.]+)?$') { throw 'Invalid release version' }
$release = Join-Path $buildRoot ('release-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $release | Out-Null
$zip = Join-Path $release ("WorldsmithExtension-$version-win-x64.zip")
Compress-Archive -Path "$package/*" -DestinationPath $zip
& python "$PSScriptRoot/verify-package.py" $zip
if ($LASTEXITCODE -ne 0) { throw 'Portable package validation failed' }
$checksum = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + (Split-Path $zip -Leaf)
[System.IO.File]::WriteAllText("$zip.sha256", $checksum + "`n", [System.Text.Encoding]::ASCII)
if ($SkipPortablePromotion) {
    Write-Host "[OK] Validated candidate: $package"
    Write-Host "[OK] Archive: $zip"
    return
}
if (Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq (Join-Path $portable 'WorldsmithExtension.exe') }) { throw "Close the running portable editor before replacing its package. Validated candidate: $package; release: $zip" }
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
if (Test-Path -LiteralPath $portable) { Move-Item -LiteralPath $portable -Destination (Join-Path $buildRoot ('previous-' + [Guid]::NewGuid().ToString('N'))) }
Move-Item -LiteralPath $package -Destination $portable
$latestRelease = Join-Path $outputRoot 'RELEASE'
if (Test-Path -LiteralPath $latestRelease) { Move-Item -LiteralPath $latestRelease -Destination (Join-Path $buildRoot ('previous-release-' + [Guid]::NewGuid().ToString('N'))) }
New-Item -ItemType Directory -Path $latestRelease | Out-Null
Copy-Item -LiteralPath $zip,"$zip.sha256" -Destination $latestRelease
Write-Host "[OK] Portable extension: $portable"
Write-Host "[OK] Latest archive: $latestRelease"
Write-Host "[OK] Archive: $zip"
