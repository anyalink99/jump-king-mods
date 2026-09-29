param(
    [Parameter(Mandatory = $true)][string]$AtlasPath,
    [int]$Port = 48723,
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$ModRoot = $PSScriptRoot
$Server = Join-Path $ModRoot 'tools\jetpack-editor\server.ps1'
$WebRoot = Join-Path $ModRoot 'tools\jetpack-editor\web'
$Atlas = (Resolve-Path -LiteralPath $AtlasPath).Path
if ([IO.Path]::GetExtension($Atlas) -ne '.png') { throw 'AtlasPath must point to an extracted King PNG sprite sheet.' }
$Poses = Join-Path $ModRoot 'assets\jetpack-poses.txt'
$Url = "http://127.0.0.1:$Port/"

foreach ($required in @($Server, $WebRoot, $Atlas, $Poses)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required editor input is missing: $required"
    }
}

try {
    Invoke-WebRequest -UseBasicParsing -Uri ($Url + 'api/poses') -TimeoutSec 1 | Out-Null
} catch {
    $Arguments = "-NoProfile -ExecutionPolicy Bypass " +
        "-File `"$Server`" -Port $Port " +
        "-WebRoot `"$WebRoot`" " +
        "-AtlasPath `"$Atlas`" " +
        "-PosePath `"$Poses`""
    Start-Process powershell.exe -ArgumentList $Arguments -WindowStyle Hidden | Out-Null

    $Deadline = (Get-Date).AddSeconds(10)
    do {
        Start-Sleep -Milliseconds 200
        try {
            Invoke-WebRequest -UseBasicParsing -Uri ($Url + 'api/poses') -TimeoutSec 1 | Out-Null
            $Ready = $true
        } catch {
            $Ready = $false
        }
    } while (-not $Ready -and (Get-Date) -lt $Deadline)

    if (-not $Ready) {
        throw 'Jetpack editor server did not start'
    }
}

if (-not $NoBrowser) {
    Start-Process $Url
}

Write-Host $Url
