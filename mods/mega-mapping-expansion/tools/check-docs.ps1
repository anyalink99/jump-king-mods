param([string]$DocumentationRoot)
$ErrorActionPreference = 'Stop'
$modRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $modRoot '..\..')).Path
if (-not $DocumentationRoot) { $DocumentationRoot = $modRoot }
$docsRoot = (Resolve-Path -LiteralPath $DocumentationRoot).Path
$internal = Join-Path $repoRoot 'build\mega-mapping-expansion\_INTERNAL'
$compiler = Join-Path $internal 'SceneCacheCompiler.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'Build Mega Mapping Expansion first to create SceneCacheCompiler.exe.'
}
$output = Join-Path $internal ('doc-checks-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null
$pending = [Collections.Generic.Queue[string]]::new()
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$pending.Enqueue((Join-Path $docsRoot 'docs/index.md'))
$examples = 0
$links = 0
while ($pending.Count -gt 0) {
    $document = $pending.Dequeue()
    if (-not $seen.Add($document)) { continue }
    if (-not (Test-Path -LiteralPath $document -PathType Leaf)) { throw "Missing documentation: $document" }
    $content = Get-Content -LiteralPath $document -Raw
    # Traverse handbook-local Markdown links, including newly linked chapters.
    foreach ($link in [regex]::Matches($content, '\[[^\]\r\n]+\]\(([^)\r\n]+)\)')) {
        $target = $link.Groups[1].Value.Trim('<','>')
        if ($target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:') { continue }
        $parts = $target.Split('#', 2)
        $path = if ($parts[0]) {
            [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $document) ([Uri]::UnescapeDataString($parts[0]))))
        } else { $document }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Broken link in ${document}: $target" }
        $links++
        if ($parts.Length -gt 1 -and $parts[1]) {
            $headings = [regex]::Matches((Get-Content -LiteralPath $path -Raw), '(?m)^#{1,6}\s+(.+?)\s*\r?$')
            $slugs = @($headings | ForEach-Object {
                ($_.Groups[1].Value.ToLowerInvariant() -replace '[^\p{L}\p{N}_\-\s]', '') -replace '\s', '-'
            })
            if ($slugs -notcontains $parts[1]) { throw "Missing heading target in ${document}: $target" }
        }
        if ($path.StartsWith($docsRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
            [IO.Path]::GetExtension($path) -eq '.md') { $pending.Enqueue($path) }
    }
    # Scene fences are complete documents; ending fences name the native target file.
    foreach ($block in [regex]::Matches($content, '(?ms)^```xml-policy[ \t]*\r?\n(.*?)^```\s*$')) {
        $examples++
        $policyPath = Join-Path $output ('policy-' + $examples + '.xml')
        [IO.File]::WriteAllText($policyPath, $block.Groups[1].Value, [Text.UTF8Encoding]::new($false))
        & (Join-Path $repoRoot 'build/jk-runtime/_INTERNAL/RuntimeChecks.exe') JKRuntime.MapPolicyTests --validate $policyPath
        if ($LASTEXITCODE -ne 0) { throw "Policy documentation failed validation: $document" }
    }
    foreach ($block in [regex]::Matches($content, '(?ms)^```xml(?:[ \t]+(custom_[a-z_]+\.xml))?[ \t]*\r?\n(.*?)^```\s*$')) {
        $examples++
        $exampleRoot = Join-Path $output ([IO.Path]::GetFileNameWithoutExtension($document) + '-' + $examples)
        $sceneDir = Join-Path $exampleRoot 'props\mega-mapping-expansion'
        New-Item -ItemType Directory -Path $sceneDir -Force | Out-Null
        $endingFile = $block.Groups[1].Value
        $xml = $block.Groups[2].Value
        $settings = [Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $textReader = [IO.StringReader]::new($xml)
        $reader = [Xml.XmlReader]::Create($textReader, $settings)
        try {
            $parsed = [Xml.XmlDocument]::new()
            $parsed.XmlResolver = $null
            $parsed.Load($reader)
            if (-not $endingFile -and $parsed.DocumentElement.Name -ne 'MegaMapping') { throw "XML example must be a complete scene or named ending tree: $document" }
        } finally { $reader.Dispose(); $textReader.Dispose() }
        if ($endingFile) {
            $endingDir = Join-Path $exampleRoot 'ending'
            New-Item -ItemType Directory -Path $endingDir | Out-Null
            [IO.File]::WriteAllText((Join-Path $endingDir $endingFile), $xml, [Text.UTF8Encoding]::new($false))
            & $compiler --validate $exampleRoot
        } else {
            [IO.File]::WriteAllText((Join-Path $sceneDir 'scene.xml'), $xml, [Text.UTF8Encoding]::new($false))
            & $compiler $exampleRoot (Join-Path $sceneDir 'scene.mmgfx')
        }
        if ($LASTEXITCODE -ne 0) { throw "Documentation XML failed compilation: $document (example $examples)" }
    }
}
if ($examples -eq 0) { throw 'No complete XML examples were found in the handbook.' }
Write-Host "[OK] Documentation: $($seen.Count) chapters, $links local links, $examples compiled XML examples."
Write-Host "[OUTPUT] $output"
