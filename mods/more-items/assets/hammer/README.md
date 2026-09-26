# Hammer impacts

The two files in `source/` were supplied by the mod author for this mod. The filenames
identify the supplied Freesound Community downloads; no additional license or
attribution metadata was provided. Keep the originals for reproducible editing.

Run `python mods/more-items/tools/prepare_hammer_audio.py` from the repository root
with ffmpeg, numpy and scipy installed. Normal mod builds embed the checked-in
WAVs and do not require these audio tools.

| Contact | Input | Source excerpt | Output |
| --- | --- | --- | --- |
| Wooden shaft | `freesound_community-086372_hit-wood-42549.mp3` | 0.2256–0.4600 s | 234 ms |
| Stone head | `freesound_community-concrete-hit-2-83782.mp3` | 0.5010–0.6400 s | 139 ms |

Trims retain 3 ms before the detected attack. The stone excerpt excludes the
later handling/rebound noises. Processing removes low-frequency rumble, resamples
to 11,025 Hz, sets peak headroom, fades the endpoints, quantizes to signed 8-bit
amplitude and holds each sample for four output samples. The mono 44,100 Hz WAV
uses a PCM16 container for MonoGame compatibility; its samples retain exactly
the 8-bit quantization. Files are embedded in the module; no loose game assets
or edits to the game's original sounds are needed.
