param([string]$DocumentationRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $DocumentationRoot) { $DocumentationRoot = $PSScriptRoot }
$docsRoot = (Resolve-Path -LiteralPath $DocumentationRoot).Path
$compiler = Join-Path $repoRoot 'build/mega-mapping-expansion/_INTERNAL/SceneCacheCompiler.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw 'Build MME first.' }
$output = Join-Path $repoRoot ('build/_work/mme-construction/docs/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$assets = Join-Path $docsRoot 'examples/minimal/mme-assets'
if (-not (Test-Path -LiteralPath $assets)) { $assets = Join-Path $docsRoot 'mme-assets' }
$pending = [Collections.Generic.Queue[string]]::new()
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in @('docs/README.md','README.md','sdk/README.md')) {
    $path = Join-Path $docsRoot $entry
    if (Test-Path -LiteralPath $path) { $pending.Enqueue($path) }
}
$complete = 0; $fragments = 0; $links = 0
function Check-Example([string]$Text,[string]$Name) {
    $root = Join-Path $output $Name
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    if (Test-Path -LiteralPath $assets) { Copy-Item -LiteralPath $assets -Destination $root -Recurse }
    $written = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($block in [regex]::Matches($Text,'(?ms)^```xml[ \t]+([^\r\n]+)\r?\n(.*?)^```[ \t]*\r?$')) {
        $relative = $block.Groups[1].Value.Trim()
        if ($relative -notmatch '^(mapping_settings\.xml|screens/mega-mapping-expansion/(transitions|collision-shapes)\.xml|ending/custom_[a-z_]+\.xml|fragments/[a-z0-9_.-]+\.xml)$' -or -not $written.Add($relative)) { throw "Invalid example path: $relative" }
        $target = Join-Path $root $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        [IO.File]::WriteAllText($target,$block.Groups[2].Value,[Text.UTF8Encoding]::new($false))
    }
    if ($written.Count -eq 0) { throw 'Empty complete example' }
    if (-not (Test-Path -LiteralPath (Join-Path $root 'mapping_settings.xml')) -and -not (Test-Path -LiteralPath (Join-Path $root 'screens/mega-mapping-expansion/transitions.xml'))) {
        [IO.File]::WriteAllText((Join-Path $root 'mapping_settings.xml'),'<MappingSettings version="1"/>')
    }
    & $compiler --validate $root
    if ($LASTEXITCODE -ne 0) { throw "Documentation example failed: $Name" }
}
while ($pending.Count -gt 0) {
    $document = $pending.Dequeue()
    if (-not $seen.Add($document)) { continue }
    $content = Get-Content -LiteralPath $document -Raw
    foreach ($link in [regex]::Matches($content,'\[[^\]\r\n]+\]\(([^)\r\n]+)\)')) {
        $target = $link.Groups[1].Value.Trim('<','>')
        if ($target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:') { continue }
        $parts = $target.Split('#',2)
        $path = if ($parts[0]) { [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $document) ([Uri]::UnescapeDataString($parts[0])))) } else { $document }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Broken link in ${document}: $target" }
        $links++
        if ($parts.Length -eq 2 -and $parts[1]) {
            $slugs = @([regex]::Matches((Get-Content -LiteralPath $path -Raw),'(?m)^#{1,6}\s+(.+?)\s*\r?$') | ForEach-Object { ($_.Groups[1].Value.ToLowerInvariant() -replace '[^\p{L}\p{N}_\-\s]','') -replace '\s','-' })
            if ($slugs -notcontains $parts[1]) { throw "Missing heading in ${document}: $target" }
        }
        if ($path.StartsWith($docsRoot+'\',[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetExtension($path) -eq '.md') { $pending.Enqueue($path) }
    }
    foreach ($group in [regex]::Matches($content,'(?ms)<!-- mme-example:start -->\s*(.*?)<!-- mme-example:end -->')) {
        $complete++; Check-Example $group.Groups[1].Value ([IO.Path]::GetFileNameWithoutExtension($document)+'-'+$complete)
    }
    $remaining = [regex]::Replace($content,'(?ms)<!-- mme-example:start -->.*?<!-- mme-example:end -->','')
    foreach ($block in [regex]::Matches($remaining,'(?ms)^```xml[ \t]+[^\r\n]+\r?\n.*?^```[ \t]*\r?$')) {
        $complete++; Check-Example $block.Value ([IO.Path]::GetFileNameWithoutExtension($document)+'-'+$complete)
    }
    foreach ($block in [regex]::Matches($content,'(?ms)^```xml-(example|fragment)[ \t]*\r?\n(.*?)^```[ \t]*\r?$')) {
        $settings = [Xml.XmlReaderSettings]::new(); $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit; $settings.XmlResolver = $null
        $reader = [Xml.XmlReader]::Create([IO.StringReader]::new($block.Groups[2].Value),$settings)
        try { $xml = [Xml.XmlDocument]::new(); $xml.XmlResolver = $null; $xml.Load($reader) } finally { $reader.Dispose() }
        $fragments++
    }
    foreach ($block in [regex]::Matches($content,'(?ms)^```xml-policy[ \t]*\r?\n(.*?)^```[ \t]*\r?$')) {
        $path = Join-Path $output ('policy-'+$fragments+'.xml'); [IO.File]::WriteAllText($path,$block.Groups[1].Value)
        & (Join-Path $repoRoot 'build/jk-runtime/_INTERNAL/RuntimeChecks.exe') JKRuntime.MapPolicyTests --validate $path
        if ($LASTEXITCODE -ne 0) { throw "Invalid policy: $document" }; $fragments++
    }
}
if ($complete -eq 0) { throw 'No complete construction examples were checked' }
$unreachable = @(Get-ChildItem -LiteralPath (Join-Path $docsRoot 'docs') -Filter '*.md' -File -Recurse | Where-Object { -not $seen.Contains($_.FullName) })
if ($unreachable.Count) { throw "Guides missing from handbook navigation: $($unreachable.FullName -join ', ')" }
$exampleRoots = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$exampleDirectory = Join-Path $docsRoot 'examples'
if (Test-Path -LiteralPath $exampleDirectory -PathType Container) {
    foreach ($entry in Get-ChildItem -LiteralPath $exampleDirectory -Filter 'mapping_settings.xml' -File -Recurse) {
        [void]$exampleRoots.Add($entry.DirectoryName)
    }
    foreach ($route in Get-ChildItem -LiteralPath $exampleDirectory -Filter 'transitions.xml' -File -Recurse) {
        [void]$exampleRoots.Add((Split-Path -Parent (Split-Path -Parent $route.DirectoryName)))
    }
}
foreach ($exampleRoot in $exampleRoots) {
    & $compiler --validate $exampleRoot
    if ($LASTEXITCODE -ne 0) { throw "Supplied example failed: $exampleRoot" }
}
Write-Host "[OK] Documentation: $($seen.Count) documents, $links links, $complete compiled examples, $fragments parsed fragments/policies, $($exampleRoots.Count) supplied scenes/routes."
Write-Host "[OUTPUT] $output"
