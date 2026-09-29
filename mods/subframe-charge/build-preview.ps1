param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King'
)

$ErrorActionPreference = 'Stop'
$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$Source = Join-Path $PSScriptRoot 'tools\GenerateWorkshopPreview.cs'
$Optimizer = Join-Path $PSScriptRoot 'tools\optimize_workshop_preview.py'
$GeneratedBase = Join-Path $PSScriptRoot 'assets\workshop-preview-base.png'
$TitleFont = Join-Path $GameDir 'Content\font\ttf_litter_lover2_bold.ttf'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$InternalDir = Join-Path $RepoRoot 'build\subframe-charge\_INTERNAL'
$Generator = Join-Path $InternalDir 'GenerateWorkshopPreview.exe'
$RawOutput = Join-Path $InternalDir 'workshop-preview-source.png'
$Output = Join-Path $PSScriptRoot 'workshop-preview.png'
$Python = (Get-Command python -ErrorAction Stop).Source

foreach ($required in @(
    $Compiler,
    $GeneratedBase,
    $TitleFont,
    $Optimizer,
    $Python
)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Workshop preview input is unavailable: $required"
    }
}

New-Item -ItemType Directory -Force -Path $InternalDir | Out-Null
& $Compiler /nologo /optimize+ /target:exe /langversion:5 `
    "/out:$Generator" `
    /reference:System.Drawing.dll `
    $Source
if ($LASTEXITCODE -ne 0) {
    throw "Workshop preview generator failed with exit code $LASTEXITCODE"
}

& $Generator $GeneratedBase $TitleFont $RawOutput
if ($LASTEXITCODE -ne 0) {
    throw "Workshop preview generation failed with exit code $LASTEXITCODE"
}

& $Python $Optimizer $RawOutput $Output --max-bytes (34KB)
if ($LASTEXITCODE -ne 0) {
    throw "Workshop preview optimization failed with exit code $LASTEXITCODE"
}
