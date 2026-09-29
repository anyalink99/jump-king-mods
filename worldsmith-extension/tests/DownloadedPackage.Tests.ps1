param(
    [Parameter(Mandatory=$true)][string]$Package,
    [Parameter(Mandatory=$true)][string]$WorldsmithDir
)
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path (Split-Path -Parent $Package) ('download-test-' + [Guid]::NewGuid().ToString('N'))
Copy-Item -LiteralPath $Package -Destination $testRoot -Recurse
$marked = @(Get-ChildItem -LiteralPath $testRoot -Recurse -File | Where-Object Extension -in @('.exe','.dll'))
foreach ($file in $marked) {
    Set-Content -LiteralPath $file.FullName -Stream Zone.Identifier -Value "[ZoneTransfer]`r`nZoneId=3" -Encoding ASCII
}

function Invoke-DownloadCheck([string]$Name, [bool]$ExpectBlocked) {
    $errorLog = Join-Path $testRoot ($Name + '-' + $ExpectBlocked + '.stderr.txt')
    $outputLog = Join-Path $testRoot ($Name + '-' + $ExpectBlocked + '.stdout.txt')
    $process = Start-Process -FilePath (Join-Path $testRoot $Name) -ArgumentList @('--worldsmith', ('"' + $WorldsmithDir + '"'), '--check') -WindowStyle Hidden -Wait -PassThru -RedirectStandardError $errorLog -RedirectStandardOutput $outputLog
    $details = Get-Content -LiteralPath $errorLog -Raw
    if ($ExpectBlocked) {
        if ($process.ExitCode -eq 0 -or $details -notmatch '0x80131515') {
            throw "The Internet-zone control did not reproduce the reported load failure: $details"
        }
    } elseif ($process.ExitCode -ne 0) {
        throw "Downloaded launcher failed: $Name ($($process.ExitCode)). $details"
    }
}

# Reproduce the old configuration's failure in a separate process first
$workerConfig = Join-Path $testRoot 'WorldsmithExtension.Worker.exe.config'
$original = [IO.File]::ReadAllText($workerConfig)
$withoutOptIn = $original -replace '<loadFromRemoteSources\s+enabled="true"\s*/>', ''
[IO.File]::WriteAllText($workerConfig, $withoutOptIn)
try { Invoke-DownloadCheck 'WorldsmithExtension.Worker.exe' $true }
finally { [IO.File]::WriteAllText($workerConfig, $original) }

Invoke-DownloadCheck 'WorldsmithExtension.Worker.exe' $false
Invoke-DownloadCheck 'WorldsmithExtension.exe' $false
foreach ($file in $marked) {
    if ((Get-Content -LiteralPath $file.FullName -Stream Zone.Identifier -Raw) -notmatch 'ZoneId=3') {
        throw "Download metadata changed: $($file.Name)"
    }
}
Write-Host '[OK] Internet-zone download reproduced; GUI and worker patch checks pass with download metadata retained.'
