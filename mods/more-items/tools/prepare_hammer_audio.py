"""Rebuild the user-provided impacts. Requires ffmpeg, numpy and scipy."""
from pathlib import Path
import subprocess
import wave

import numpy as np
from scipy.signal import resample_poly, butter, sosfilt

ROOT = Path(__file__).resolve().parents[1] / "assets" / "hammer"


def convert(source, name, end):
    raw = subprocess.check_output([
        "ffmpeg", "-v", "error", "-i", str(ROOT / "source" / source),
        "-f", "f32le", "-ac", "1", "-ar", "24000", "-",
    ])
    signal = np.frombuffer(raw, dtype="<f4").astype(np.float64)
    # Detect the main impact above the background and keep 3 ms of pre-roll.
    onset = np.flatnonzero(np.abs(signal) > np.max(np.abs(signal)) * 0.05)[0]
    start = max(0, onset - 72)
    signal = signal[start:round(end * 24000)]
    signal = sosfilt(butter(2, 100, "highpass", fs=24000, output="sos"), signal)
    # Bandlimited 11.025 kHz, then sample-and-hold at the native output rate.
    signal = resample_poly(signal, 147, 320)
    signal *= 0.82 / np.max(np.abs(signal))
    attack, tail = round(0.001 * 11025), round(0.018 * 11025)
    signal[:attack] *= np.linspace(0, 1, attack)
    signal[-tail:] *= np.linspace(1, 0, tail)
    # Signed 8-bit quantization; PCM16 is the storage container required by
    # MonoGame's decoder. No dither: a silent tail stays silent.
    pcm = np.repeat(np.rint(signal * 127).astype(np.int16) * 256, 4).astype("<i2")
    target = ROOT / (name + "-8bit.wav")
    with wave.open(str(target), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(44100)
        output.writeframes(pcm.tobytes())
    print(f"{target.name}: source {start / 24000:.4f}..{end:.4f}s, "
          f"{len(pcm) / 44100:.3f}s, peak {abs(pcm.astype(int)).max() / 32768:.3f}")


convert("freesound_community-086372_hit-wood-42549.mp3", "hammer-wood", 0.460)
# Exclude the separate handling/rebound noises starting around 0.65 seconds.
convert("freesound_community-concrete-hit-2-83782.mp3", "hammer-stone", 0.640)
