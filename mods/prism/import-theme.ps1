param([Parameter(Mandatory=$true)][string]$SongDirectory, [string]$Beatmap)
$ErrorActionPreference = 'Stop'
$prismRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$prismOutput = Join-Path $prismRepo 'build/prism/LOCAL_THEME/event-horizon'
$prismMaps = @(Get-ChildItem -LiteralPath $SongDirectory -Filter '*.osu' -File)
if ($Beatmap) { $prismMap = Get-Item -LiteralPath (Join-Path $SongDirectory $Beatmap) }
elseif ($prismMaps.Count -eq 1) { $prismMap = $prismMaps[0] }
else { throw 'Choose a .osu filename with -Beatmap.' }
$prismAudioLine = Get-Content -LiteralPath $prismMap.FullName | Where-Object { $_ -match '^AudioFilename:' } | Select-Object -First 1
if (-not $prismAudioLine) { throw 'Beatmap has no AudioFilename' }
$prismAudio = [IO.Path]::GetFullPath((Join-Path $SongDirectory ($prismAudioLine.Substring($prismAudioLine.IndexOf(':') + 1).Trim())))
$prismRoot = (Resolve-Path -LiteralPath $SongDirectory).Path.TrimEnd('\') + '\'
if (-not $prismAudio.StartsWith($prismRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Audio path leaves the selected song folder' }
New-Item -ItemType Directory -Force $prismOutput | Out-Null
& ffmpeg -hide_banner -loglevel error -y -i $prismAudio -ar 44100 -ac 2 -c:a pcm_s16le (Join-Path $prismOutput 'music.wav')
if ($LASTEXITCODE -ne 0) { throw 'Audio conversion failed' }
Copy-Item -LiteralPath $prismMap.FullName -Destination (Join-Path $prismOutput 'score.osu') -Force
@{ AudioSource = $prismAudio; BeatmapSource = $prismMap.FullName; Imported = [DateTime]::UtcNow.ToString('o'); Files = @(Get-ChildItem $prismOutput -File | Where-Object Extension -in '.wav','.osu' | ForEach-Object { @{Name=$_.Name; SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash} }) } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $prismOutput 'import.json') -Encoding UTF8
Write-Host "[OK] Local Event Horizon music and chart: $prismOutput"
