param(
    [string]$RuntimePath,
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [string]$WorkshopDir = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\1061090'
)
$ErrorActionPreference = 'Stop'
$bindingRepo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$bindingBuild = Join-Path $bindingRepo 'build/jk-runtime/_INTERNAL/binding-discovery'
New-Item -ItemType Directory -Force -Path $bindingBuild | Out-Null
if (-not $RuntimePath) { $RuntimePath = Join-Path $bindingRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll' }
$RuntimePath = (Resolve-Path -LiteralPath $RuntimePath).Path
foreach ($bindingDependency in @('JumpKing.exe','MonoGame.Framework.dll','LanguageJK.dll','Steamworks.NET.dll','Content/JKMods/0Harmony.dll')) {
    Copy-Item -LiteralPath (Join-Path $GameDir $bindingDependency) -Destination $bindingBuild -Force
}
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo /nowarn:1685 /optimize+ /target:exe /langversion:5 "/out:$bindingBuild/BindingDiscoveryBenchmark.exe" "/reference:$bindingBuild/JumpKing.exe" "/reference:$bindingBuild/MonoGame.Framework.dll" "/reference:$bindingBuild/0Harmony.dll" (Join-Path $PSScriptRoot 'BindingDiscoveryBenchmark.cs')
if ($LASTEXITCODE -ne 0) { throw 'Binding benchmark compilation failed' }
& "$bindingBuild/BindingDiscoveryBenchmark.exe" $GameDir $WorkshopDir $RuntimePath
if ($LASTEXITCODE -ne 0) { throw 'Binding benchmark failed' }
