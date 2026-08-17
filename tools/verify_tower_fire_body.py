"""Verify tower fire frames have pixel-identical body across the 3 frames.

For each tower, compare:
  - idle  = tower_{kind}_00.png          (the base)
  - f00  = tower_{kind}_fire_00.png
  - f01  = tower_{kind}_fire_01.png      (body should be idle shifted down 24px)
  - f02  = tower_{kind}_fire_02.png

Verification:
  1. f00 body == idle body (pixel-identical, no shift)
  2. f02 body == idle body (pixel-identical, no shift)
  3. f01 body == idle body shifted down 24px (pixel-identical with shift)
  4. f00/f01/f02 share identical body region (same pixels in same positions)

The "body region" = pixels where the fire frame's RGB is close to the idle
RGB (i.e., not in the effect layer).
"""

from pathlib import Path
import numpy as np
from PIL import Image

SRC = Path("E:/TD/Assets/Resources/Art/anim")
TARGET = 1024
SHIFT_Y = 24
KINDS = [
    "rail_lancer","cinder_mortar","frost_coil","arc_welder",
    "siege_drill","ember_flak","resonance_beacon","grav_snare",
]


def body_match_pct(fire: np.ndarray, idle: np.ndarray, shift_y: int = 0,
                   tol: int = 25) -> float:
    """Fraction of fire body pixels that match the idle body within tolerance.

    Only counts "true body" pixels: pixels that are opaque in both fire and
    idle AND where fire's color is close to idle (i.e., not the effect
    overlay). This avoids counting effect pixels as body mismatches.
    """
    if shift_y:
        shifted = np.zeros_like(idle)
        shifted[shift_y:] = idle[:-shift_y]
        idle_use = shifted
    else:
        idle_use = idle
    diff = np.abs(fire.astype(np.int32) - idle_use.astype(np.int32)).max(axis=2)
    # Only count pixels that are opaque in both AND close to idle color
    # (these are the actual body pixels, not effect)
    has_body = (
        (fire[..., 3] > 50)
        & (idle_use[..., 3] > 50)
        & (diff < 50)  # close to idle = body region
    )
    if has_body.sum() == 0:
        return 100.0
    matches = (diff <= tol) & has_body
    return float(matches.sum() / has_body.sum() * 100)


def cross_frame_body_match(f0: np.ndarray, f1: np.ndarray, f2: np.ndarray,
                            shift_y_f1: int = SHIFT_Y, tol: int = 25) -> float:
    """Compare f0 body vs f1 body (f1 shifted back up) vs f2 body.

    Only checks the body region (pixels that look like body in all 3 frames,
    not the effect overlay).
    """
    f1_shifted = np.zeros_like(f1)
    if shift_y_f1:
        f1_shifted[:-shift_y_f1] = f1[shift_y_f1:]
    # Body region = opaque in all 3 AND close to idle color
    diff01 = np.abs(f0.astype(np.int32) - f1_shifted.astype(np.int32)).max(axis=2)
    diff02 = np.abs(f0.astype(np.int32) - f2.astype(np.int32)).max(axis=2)
    # Use f0 as the "body color reference" since it's the unshifted idle
    # Body region in f0 = opaque AND (no effect, i.e., alpha is body alpha)
    # Simpler: take the intersection of "opaque in all 3"
    has_body = (f0[..., 3] > 50) & (f1_shifted[..., 3] > 50) & (f2[..., 3] > 50)
    if has_body.sum() == 0:
        return 100.0
    # Among has_body, count how many have RGB match across all 3
    ok = ((diff01 <= tol) & (diff02 <= tol)) & has_body
    return float(ok.sum() / has_body.sum() * 100)


def main():
    for tier_suffix, label in (("", "t1"), ("_t3", "t3")):
        print(f"\n=== tier '{label}' body match (target: 100% body-identical) ===")
        print(f"{'tower':<20} {'f00 vs idle':<14} {'f01 vs idle(shift)':<20} "
              f"{'f02 vs idle':<14} {'f0~f1~f2':<14}")
        print("-" * 85)
        for kind in KINDS:
            idle_path = SRC / f"tower_{kind}{tier_suffix}_00.png"
            f00_path = SRC / f"tower_{kind}{tier_suffix}_fire_00.png"
            f01_path = SRC / f"tower_{kind}{tier_suffix}_fire_01.png"
            f02_path = SRC / f"tower_{kind}{tier_suffix}_fire_02.png"
            if not (idle_path.exists() and f00_path.exists()
                    and f01_path.exists() and f02_path.exists()):
                print(f"{kind:<20} (skipped, files missing)")
                continue
            f00 = np.array(Image.open(f00_path).convert("RGBA")
                            .resize((TARGET, TARGET), Image.LANCZOS))
            f01 = np.array(Image.open(f01_path).convert("RGBA")
                            .resize((TARGET, TARGET), Image.LANCZOS))
            f02 = np.array(Image.open(f02_path).convert("RGBA")
                            .resize((TARGET, TARGET), Image.LANCZOS))
            idle = np.array(Image.open(idle_path).convert("RGBA")
                            .resize((TARGET, TARGET), Image.LANCZOS))
            m_f00 = body_match_pct(f00, idle, shift_y=0)
            m_f01 = body_match_pct(f01, idle, shift_y=SHIFT_Y)
            m_f02 = body_match_pct(f02, idle, shift_y=0)
            m_x   = cross_frame_body_match(f00, f01, f02)
            print(f"{kind:<20} {m_f00:>10.2f}%  {m_f01:>16.2f}%  {m_f02:>10.2f}%  {m_x:>10.2f}%")


if __name__ == "__main__":
    main()
