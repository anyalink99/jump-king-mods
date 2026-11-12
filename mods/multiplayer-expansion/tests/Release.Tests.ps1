param([Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $Package).Path
$handbooks = @(Get-ChildItem -LiteralPath (Join-Path $root 'docs') -Recurse -File -Filter '*.md')
$handbooks += Get-Item -LiteralPath (Join-Path $root 'README.md')
foreach ($handbook in $handbooks) {
    $body = [IO.File]::ReadAllText($handbook.FullName)
    foreach ($link in [regex]::Matches($body, '\]\(([^\s)]+\.md)(?:#[^\s)]*)?\)')) {
        $target = $link.Groups[1].Value
        if ($target -match '^[a-z]+:') { continue }
        $resolved = [IO.Path]::GetFullPath((Join-Path $handbook.DirectoryName $target))
        # cross-mod guides live in the repository; local chapters must ship together
        if ($resolved.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and -not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            throw "Missing packaged guide in $($handbook.Name): $target"
        }
    }
}
foreach ($name in @('MultiplayerExpansion.dll','MultiplayerExpansion.exe',
    'MultiplayerExpansion.exe.config','0Harmony.dll','stage-native.ps1','stage-lab.ps1',
    'docs/index.md','THIRD_PARTY_NOTICES.md','Harmony-LICENSE.txt','workshop-preview.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $name) -PathType Leaf)) { throw "Missing release input: $name" }
}
$version = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $root 'MultiplayerExpansion.dll')).Version
if ([Reflection.AssemblyName]::GetAssemblyName((Join-Path $root 'MultiplayerExpansion.exe')).Name -ne 'MultiplayerExpansion.Client') { throw 'Client bootstrap shadows the native SDK shell.' }
foreach ($name in @('MultiplayerExpansion.exe')) {
    if ([Reflection.AssemblyName]::GetAssemblyName((Join-Path $root $name)).Version -ne $version) { throw "Release version mismatch: $name" }
}
$preview = Get-Item -LiteralPath (Join-Path $root 'workshop-preview.png')
if ($preview.Length -ge 35000) { throw 'Workshop preview must be smaller than 35,000 bytes.' }
Add-Type -AssemblyName System.Drawing
$image = [Drawing.Image]::FromFile($preview.FullName)
try { if ($image.Width -ne 256 -or $image.Height -ne 256) { throw 'Workshop preview must be 256 x 256.' } }
finally { $image.Dispose() }
$forbidden = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.Name -match '\.(log|bak|tmp)$|^(interactions|appearance|opacity-profiles|actions|kick-binds|session-root|session|native-root)\.txt$|\.Module\.dll$|^MultiplayerExpansion\.World\.dll$' -or
    $_.FullName -match '[\\/](tools|client1|client2|install-backups)[\\/]'
})
if ($forbidden.Count) { throw "Personal settings or private build artifacts in release: $($forbidden.Name -join ', ')" }
Write-Host "[OK] Release $version, matching binaries, native staging, clean settings and $($preview.Length)-byte preview"
