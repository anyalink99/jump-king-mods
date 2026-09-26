param()

$ErrorActionPreference = 'Stop'
$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$Source = Join-Path $PSScriptRoot 'tools\FixedJumpMacro.cs'
$OutputDirectory = Join-Path `
    (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) `
    'build\subframe-charge\TOOLS'
$Output = Join-Path $OutputDirectory 'FixedJumpMacro.exe'

if (-not (Test-Path -LiteralPath $Compiler -PathType Leaf)) {
    throw "C# compiler is unavailable: $Compiler"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
& $Compiler /nologo /optimize+ /target:exe /langversion:5 `
    "/out:$Output" `
    $Source
if ($LASTEXITCODE -ne 0) {
    throw "Fixed Jump Macro compilation failed with exit code $LASTEXITCODE"
}

Write-Host "[OK] Fixed Jump Macro: $Output"
