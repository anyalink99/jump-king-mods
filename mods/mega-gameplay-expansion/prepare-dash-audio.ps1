param([Parameter(Mandatory=$true)][string]$Source)
$ErrorActionPreference = 'Stop'
$expected = 'A76CBEDEFB84C911EEFEA5C49B3BD3789494FD4E4FA96CFF93840062FDAB39A0'
if ((Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash -ne $expected) {
    throw 'Expected Pixabay Whoosh Velocity 383019. See assets/audio/README.md.'
}
$output = Join-Path $PSScriptRoot 'assets/audio/air-dash-8bit.wav'
# Extract the attack and decay, remove leading silence, then fit one dash.
# Low-rate 8-bit processing is stored in 16-bit PCM for native XNA playback.
$filters = 'atrim=start=0.23:end=1.03,asetpts=PTS-STARTPTS,pan=mono|c0=0.5*c0+0.5*c1,atempo=2,atempo=1.333333,highpass=f=90,lowpass=f=4800,aresample=11025,acrusher=bits=8:mode=lin:mix=1,aresample=44100,apad,atrim=duration=0.3,afade=t=in:d=0.003,afade=t=out:st=0.245:d=0.055,volume=0.8'
& ffmpeg -hide_banner -loglevel error -y -i $Source -af $filters -ac 1 -ar 44100 -c:a pcm_s16le $output
if ($LASTEXITCODE -ne 0) { throw 'Air Dash audio conversion failed' }
Write-Host "[OK] Air Dash audio: $output"
