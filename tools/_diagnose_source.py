"""Diagnose why the source files are too dynamic to hit LUFS targets."""
import wave
import numpy as np
import pyloudnorm as pyln
from pathlib import Path

meter = pyln.Meter(44100)

SOURCES = {
    "boss_spawn":       "E:/TD/output/audio/sfx/combat/boss_spawn.wav",
    "window_open":      "E:/TD/output/audio/sfx/resonance/window_open.wav",
    "fire_arc_welder":  "E:/TD/output/audio/sfx/towers/fire_arc_welder.wav",
    "fire_frost_coil":  "E:/TD/output/audio/sfx/towers/fire_frost_coil.wav",
    "tower_place":      "E:/TD/output/audio/sfx/ui/tower_place.wav",
}

for name, src in SOURCES.items():
    with wave.open(src, "rb") as wf:
        sr = wf.getframerate()
        n = wf.getnframes()
        ch = wf.getnchannels()
        raw = wf.readframes(n)
    s = np.frombuffer(raw, dtype="<i2").astype(np.float64) / 32768.0
    if ch > 1:
        s = s.reshape(-1, ch).mean(axis=1)
    dur = n / sr
    win = sr  # 1s window
    step = sr // 2
    pk_overall = 20 * np.log10(np.max(np.abs(s)) + 1e-9)
    print(f"\n=== {name} (src: {dur:.1f}s, peak={pk_overall:+.2f} dBFS) ===")
    print("  1s-window peak/LUFS scan (every 0.5s):")
    rows = []
    for i in range(0, max(1, len(s) - win), step):
        seg = s[i:i + win]
        if seg.shape[0] < int(0.4 * sr):
            continue
        pk = 20 * np.log10(np.max(np.abs(seg)) + 1e-9)
        try:
            lufs = meter.integrated_loudness(seg.reshape(-1, 1))
        except Exception:
            lufs = float("nan")
        t = i / sr
        rows.append((t, pk, lufs))
    for t, pk, lufs in rows[:20]:
        print(f"    t={t:5.1f}s  peak={pk:+6.2f} dBFS  LUFS={lufs:+7.2f}")
    if len(rows) > 20:
        print(f"    ... ({len(rows) - 20} more rows)")
