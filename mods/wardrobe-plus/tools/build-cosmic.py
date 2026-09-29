"""Deterministic, toroidal pixel-space galaxy and star layers. Requires Pillow."""
import math
import random
from pathlib import Path
from PIL import Image

SIZE = 512
rng = random.Random(631904)
nebula = [[0.0, 0.0, 0.0] for _ in range(SIZE * SIZE)]
palettes = [(0.20, 0.46, 1.0), (0.65, 0.18, 1.0), (0.1, 0.8, 0.94), (0.92, 0.23, 0.58)]
for gy in range(16):
    for gx in range(16):
        cx, cy = (gx + rng.random()) * SIZE / 16, (gy + rng.random()) * SIZE / 16
        radius, angle = rng.uniform(10, 18), rng.random() * math.tau
        flatten, spin = rng.uniform(0.38, 0.85), rng.choice([-1, 1])
        tint = rng.choice(palettes)
        c, s = math.cos(angle), math.sin(angle)
        for y in range(math.floor(cy - radius), math.ceil(cy + radius) + 1):
            for x in range(math.floor(cx - radius), math.ceil(cx + radius) + 1):
                dx, dy = x - cx, y - cy
                u, v = (dx * c + dy * s) / radius, (-dx * s + dy * c) / (radius * flatten)
                r = math.hypot(u, v)
                if r > 1:
                    continue
                theta = math.atan2(v, u)
                arms = (0.5 + 0.5 * math.cos(theta * 2 + spin * r * 12)) ** 3
                falloff = max(0, 1 - r) ** 2
                dust = 0.72 + rng.random() * 0.28
                glow = (0.12 + arms * 1.25) * falloff * dust
                core = math.exp(-r * r * 140) * 0.95
                p = nebula[(y % SIZE) * SIZE + x % SIZE]
                for channel in range(3):
                    p[channel] += tint[channel] * glow + core * (1.0, 0.88, 0.68)[channel]

stars = [(0, 0, 0)] * (SIZE * SIZE)
for _ in range(2400):
    x, y = rng.randrange(SIZE), rng.randrange(SIZE)
    brightness = rng.randrange(80, 256)
    phase = rng.randrange(256)
    stars[y * SIZE + x] = (brightness, phase, 255 if brightness > 240 else 0)
    if brightness > 248:
        for dx, dy in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
            stars[((y + dy) % SIZE) * SIZE + (x + dx) % SIZE] = (46, phase, 0)

root = Path(__file__).resolve().parents[1] / "assets"
image = Image.new("RGB", (SIZE, SIZE))
image.putdata([tuple(round(min(1, v) * 255) for v in p) for p in nebula])
image.save(root / "cosmic-nebula.png", optimize=True)
image.putdata(stars)
image.save(root / "cosmic-stars.png", optimize=True)
