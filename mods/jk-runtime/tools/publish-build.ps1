function Publish-RuntimeBuild {
    param([string]$BuildRoot, [string]$CandidateDll, [string]$CandidateSdk)
    $root = [IO.Path]::GetFullPath($BuildRoot).TrimEnd('\') + '\'
    $targetDll = Join-Path $BuildRoot 'UPLOAD_TO_WORKSHOP\JKRuntime.dll'
    $targetSdk = Join-Path $BuildRoot 'SDK'
    $backupRoot = Join-Path (Split-Path $CandidateDll -Parent) ('previous-' + [Guid]::NewGuid().ToString('N'))
    foreach ($path in @($CandidateDll, $CandidateSdk, $targetDll, $targetSdk, $backupRoot)) {
        if (-not [IO.Path]::GetFullPath($path).StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Publication path escapes build root: $path"
        }
    }
    if (-not (Test-Path -LiteralPath $CandidateDll -PathType Leaf) -or -not (Test-Path -LiteralPath $CandidateSdk -PathType Container)) {
        throw 'Verified DLL and SDK candidates are required.'
    }
    New-Item -ItemType Directory -Path $backupRoot | Out-Null
    New-Item -ItemType Directory -Force -Path (Split-Path $targetDll -Parent) | Out-Null
    $savedSdk = Join-Path $backupRoot 'SDK'
    $movedOldSdk = $false
    $movedNewSdk = $false
    try {
        if (Test-Path -LiteralPath $targetSdk) {
            Move-Item -LiteralPath $targetSdk -Destination $savedSdk
            $movedOldSdk = $true
        }
        Move-Item -LiteralPath $CandidateSdk -Destination $targetSdk
        $movedNewSdk = $true
        # The Workshop DLL changes in one filesystem operation, only after validation.
        if (Test-Path -LiteralPath $targetDll -PathType Leaf) {
            [IO.File]::Replace($CandidateDll, $targetDll, (Join-Path $backupRoot 'JKRuntime.dll'))
        } else {
            [IO.File]::Move($CandidateDll, $targetDll)
        }
    } catch {
        $failure = $_
        if ($movedNewSdk) { Move-Item -LiteralPath $targetSdk -Destination $CandidateSdk }
        if ($movedOldSdk) { Move-Item -LiteralPath $savedSdk -Destination $targetSdk }
        throw $failure
    }
}
