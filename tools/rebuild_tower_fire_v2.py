"""Rebuild tower fire frames with true pixel-identical body.

Strategy:
  1. Read AI-generated fire frame (already on disk in Assets/Resources/Art/anim/)
  2. Read the original tower idle (Assets/Resources/Art/anim/tower_{kind}_00.png)
  3. Per-pixel diff |AI - idle|: high diff = "effect" pixels, low diff = "body-like"
  4. Mask out the body-like pixels, keep only the effect layer
  5. Composite effect layer on top of the original idle (alpha-over)
     - Frame 00: effect opacity 0.6 (pre-fire, faint)
     - Frame 01: effect opacity 1.0 (peak) + idle body shifted down 2.4%
     - Frame 02: effect opacity 0.7 (afterglow)
  6. Save 1024x1024 RGBA PNG

Result: tower body is the EXACT same pixels as tower_{kind}_00.png in all
3 frames (just shifted in frame 01). Only the fire effect layer varies.
"""

import sys
from pathlib import Path
import numpy as np
from PIL import Image

SRC_DIR  = Path("E:/TD/Assets/Resources/Art/anim")
DST_DIR  = SRC_DIR  # overwrite in place
TARGET   = 1024
DIFF_THRESH = 150  # |AI - idle| per channel, > this is "effect" pixel
                    # (150 keeps only the brightest, most saturated effect
                    #  pixels; everything below is treated as body. This
                    #  protects body pixels from being overwritten by
                    #  AI's slightly-redrawn body color.)
SHIFT_PCT = 0.024  # 2.4% down on frame 01

KIND_FX = {
    "rail_lancer":      "blue-white plasma streak",
    "cinder_mortar":    "orange cannon smoke",
    "frost_coil":       "cyan-white ice burst",
    "arc_welder":       "blue-white electric arc",
    "siege_drill":      "golden drill sparks",
    "ember_flak":       "orange-red flak flame",
    "resonance_beacon": "green pulse halo",
    "grav_snare":       "blue-purple gravity ripple",
}

OPACITY = {0: 0.6, 1: 1.0, 2: 0.7}


def extract_and_composite(ai_path: Path, idle_path: Path, out_path: Path,
                          frame: int, threshold: int = DIFF_THRESH):
    ai = np.array(Image.open(ai_path).convert("RGBA").resize(
        (TARGET, TARGET), Image.LANCZOS))
    idle = np.array(Image.open(idle_path).convert("RGBA").resize(
        (TARGET, TARGET), Image.LANCZOS))

    # Effect = AI pixels that differ from idle (and AI had content there)
    ai_rgb = ai[..., :3].astype(np.int32)
    idle_rgb = idle[..., :3].astype(np.int32)
    diff = np.abs(ai_rgb - idle_rgb).max(axis=2)
    had_content = ai[..., 3] > 30
    effect_mask = had_content & (diff > threshold)

    # Build the effect layer
    effect_layer = np.zeros((TARGET, TARGET, 4), dtype=np.uint8)
    effect_layer[effect_mask, :3] = ai[effect_mask, :3]
    opacity = OPACITY[frame]
    effect_layer[..., 3] = (effect_mask.astype(np.float32) * opacity * 255).astype(np.uint8)

    # Body: original idle, possibly shifted down for frame 01
    body = idle.copy()
    if frame == 1:
        shift_y = int(TARGET * SHIFT_PCT)  # 24 px @ 1024
        shifted = np.zeros_like(idle)
        if shift_y > 0:
            shifted[shift_y:] = idle[:-shift_y]
        body = shifted

    # Composite: effect over body (alpha-over)
    body_a = body[..., 3:4].astype(np.float32) / 255.0
    effect_a = effect_layer[..., 3:4].astype(np.float32) / 255.0
    out_a = effect_a + body_a * (1.0 - effect_a)
    out_rgb = (effect_layer[..., :3].astype(np.float32) * effect_a
               + body[..., :3].astype(np.float32) * body_a * (1.0 - effect_a))
    out_rgb = np.where(out_a > 1e-6, out_rgb / np.maximum(out_a, 1e-6), 0)

    out = np.dstack([out_rgb.astype(np.uint8),
                     (out_a[..., 0] * 255).astype(np.uint8)])
    Image.fromarray(out, "RGBA").save(out_path, "PNG")

    # Stats
    eff_pct = effect_mask.mean() * 100
    final_trans = (out[..., 3] < 5).mean() * 100
    return eff_pct, final_trans


def process_tier(tier_suffix: str):
    """Process either t1 (empty suffix) or t3 (_t3 suffix)."""
    label = "t1" if not tier_suffix else "t3"
    print(f"\n=== tier '{label}' ===")
    for kind in KIND_FX.keys():
        idle_path = SRC_DIR / f"tower_{kind}{tier_suffix}_00.png"
        if not idle_path.exists():
            print(f"  MISSING idle: {idle_path}")
            continue
        for frame in range(3):
            ai_path = SRC_DIR / f"tower_{kind}{tier_suffix}_fire_{frame:02d}.png"
            out_path = ai_path
            eff_pct, trans_pct = extract_and_composite(
                ai_path, idle_path, out_path, frame)
            tag = f"{kind}{tier_suffix}"
            print(f"  {tag:<24} fire_{frame:02d}  effect={eff_pct:5.2f}%  "
                  f"trans={trans_pct:5.2f}%")


def main():
    for tier in ("", "_t3"):
        process_tier(tier)


if __name__ == "__main__":
    main()
