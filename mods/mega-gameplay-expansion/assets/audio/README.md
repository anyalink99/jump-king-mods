# Air Dash sound

`air-dash-8bit.wav` adapts **Whoosh Velocity** by **SoundReality**:
https://pixabay.com/sound-effects/film-special-effects-whoosh-velocity-383019/

Source retrieved September 7, 2026, under the Pixabay Content License:
https://pixabay.com/service/license-summary/

This edited sound is part of Mega Gameplay Expansion, not a standalone sound
library. The original MP3 is not included in the repository or Workshop payload.
Its SHA-256 is
`A76CBEDEFB84C911EEFEA5C49B3BD3789494FD4E4FA96CFF93840062FDAB39A0`.

The edit extracts 0.23–1.03 seconds and compresses time without shifting pitch.
Mono audio is high-pass filtered at 90 Hz, low-pass filtered at 4800 Hz,
resampled to 11025 Hz and quantized to 8 bits. The final file uses 44100 Hz
16-bit PCM for XNA compatibility. A 3 ms attack and 55 ms fade-out avoid hard
cuts. Duration is 300 ms; gain is 0.8 with headroom below full scale.

To reproduce with FFmpeg and the original download:

```powershell
.\mods\mega-gameplay-expansion\prepare-dash-audio.ps1 -Source path\to\whoosh-velocity-383019.mp3
```

Ordinary mod builds embed the prepared WAV and do not download or convert audio.
