"""Parameterized haze/dirt cleanup for FX and aura frames.

Motivated by in-game evidence (player screenshots): FX frames carry
low-alpha haze far beyond their effect core, and the resonance_beacon
aura bleeds past the body. In-game these read as dirty cutouts.

Treatments (all reversible via backups in output/imagegen/_haze_backup/):

  A) outside-haze kill — alpha < OUTER_KEEP outside the solid-content
     bbox (expanded by MARGIN px). Real smoke columns (alpha >=
     OUTER_KEEP) survive; faint film dies.
  B) corner zeroing — follows from A (corners are outside the bbox).
  C) aura tighten (resonance_beacon only) — faint pixels (alpha <
     AURA_KEEP) farther than AURA_RADIUS px from the nearest solid
     pixel are zeroed. Keeps the identity glow hugging the body.
  D) core-less dissolve frames (fx_enemy_death_04..06) — no solid
     core, so bbox logic fails. Center-anchored: keep faint pixels
     within DISSOLVE_RADIUS of canvas center, zero beyond; also drop
     alpha < 24 globally.

Usage:
  python tools/cleanup_fx_haze.py --dry-run   (report only)
  python tools/cleanup_fx_haze.py             (apply, with backups)
"""

import argparse
import shutil
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ANIM = Path("E:/TD/Assets/Resources/Art/anim")
BACKUP = Path("E:/TD/output/imagegen/_haze_backup")

OUTER_KEEP = 100   # alpha below this, outside bbox+margin -> 0
MARGIN = 30        # bbox expansion in px
AURA_KEEP = 160    # beacon: faint alpha threshold (all glow counts)
AURA_NEAR = 14     # beacon: glow within this distance of solid body stays
AURA_DIM = 60      # beacon: glow beyond NEAR dimmed to 55%, beyond DIM zeroed
DISSOLVE_RADIUS = 430   # death dissolve frames: keep within this of center
DISSOLVE_MIN = 24       # death dissolve frames: alpha floor

# (file, mode) list — mode: 'outer' | 'aura' | 'dissolve'
TARGETS = [
    # enemy FX with heavy outside haze / corners
    *[(f"fx_enemy_hit_{i:02d}.png", "outer") for i in (3, 4)],
    *[(f"fx_boss_warning_{i:02d}.png", "outer") for i in (2, 3, 4, 6, 7)],
    *[(f"fx_burrow_ambush_{i:02d}.png", "outer") for i in (2, 3, 5, 6, 7)],
    *[(f"fx_attrition_siphon_{i:02d}.png", "outer") for i in range(8)],
    *[(f"fx_elite_pressure_{i:02d}.png", "outer") for i in range(10)],
    *[(f"fx_support_link_{i:02d}.png", "outer") for i in range(8)],
    *[(f"fx_mimic_shift_{i:02d}.png", "outer") for i in range(8)],
    *[(f"fx_spore_split_warning_{i:02d}.png", "outer") for i in (0, 3, 4, 5, 6, 7)],
    *[(f"fx_enemy_death_{i:02d}.png", "outer") for i in range(4)],
    *[(f"fx_enemy_death_{i:02d}.png", "dissolve") for i in (4, 5, 6)],
    # fire reels with corner remnants
    ("tower_frost_coil_fire_01.png", "outer"),
    ("tower_frost_coil_t3_fire_00.png", "outer"),
    ("tower_ember_flak_t3_fire_00.png", "outer"),
    ("tower_ember_flak_fire_00.png", "outer"),
    ("tower_cinder_mortar_fire_00.png", "outer"),
    ("tower_rail_lancer_t3_fire_00.png", "outer"),
    ("tower_siege_drill_t3_fire_02.png", "outer"),
    ("tower_siege_drill_t3_fire_01.png", "outer"),
    ("tower_resonance_beacon_fire_00.png", "outer"),
    # beacon aura tighten (identity glow stays, bleed goes)
    *[(f"tower_resonance_beacon_{i:02d}.png", "aura") for i in range(6)],
    *[(f"tower_resonance_beacon_t2_{i:02d}.png", "aura") for i in range(6)],
    ("tower_resonance_beacon_t3_fire_00.png", "aura"),
    ("tower_resonance_beacon_fire_02.png", "aura"),
]


def process(path: Path, mode: str) -> tuple[float, float]:
    """Returns (removed_pct_of_canvas, kept_faint_pct)."""
    arr = np.asarray(Image.open(path).convert("RGBA")).copy()
    a = arr[:, :, 3]
    before_faint = ((a > 8) & (a < 160)).sum()

    if mode == "outer":
        solid = a >= 160
        ys, xs = np.nonzero(solid)
        if len(xs):
            x0, x1 = max(0, xs.min()-MARGIN), min(1024, xs.max()+MARGIN)
            y0, y1 = max(0, ys.min()-MARGIN), min(1024, ys.max()+MARGIN)
            outside = np.ones_like(a, bool)
            outside[y0:y1, x0:x1] = False
            kill = outside & (a > 0) & (a < OUTER_KEEP)
            a[kill] = 0
    elif mode == "aura":
        # identity glow survives close to the body (<= AURA_NEAR px),
        # is dimmed to 55% further out, and cut entirely beyond AURA_DIM.
        solid = a >= 160
        dist = ndimage.distance_transform_edt(~solid)
        af = a.astype(np.float64)
        dim = (a > 0) & (a < AURA_KEEP) & (dist > AURA_NEAR)
        af[dim] *= 0.55
        kill = (a > 0) & (a < AURA_KEEP) & (dist > AURA_DIM)
        af[kill] = 0
        a = np.clip(af, 0, 255).astype(np.uint8)
    elif mode == "dissolve":
        yy, xx = np.mgrid[0:1024, 0:1024]
        r = np.sqrt((yy-512)**2 + (xx-512)**2)
        kill = ((a > 0) & (a < DISSOLVE_MIN)) | ((a > 0) & (r > DISSOLVE_RADIUS))
        a[kill] = 0

    arr[:, :, 3] = a
    removed = (before_faint - ((a > 8) & (a < 160)).sum()) / 1048576 * 100
    kept_faint = ((a > 8) & (a < 160)).sum() / 1048576 * 100
    Image.fromarray(arr).save(path)
    return removed, kept_faint


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    BACKUP.mkdir(parents=True, exist_ok=True)
    total_removed = 0.0
    for name, mode in TARGETS:
        p = ANIM / name
        if not p.exists():
            print(f"  {name:44s} MISSING")
            continue
        if args.dry_run:
            print(f"  [dry] {name:44s} mode={mode}")
            continue
        bak = BACKUP / name
        if not bak.exists():
            shutil.copy2(p, bak)
        removed, kept = process(p, mode)
        total_removed += removed
        print(f"  {name:44s} mode={mode:8s} haze-removed={removed:5.1f}% kept-faint={kept:5.1f}%")
    print(f"\ntotal haze removed: {total_removed:.1f}% of canvas area")
    return 0


if __name__ == "__main__":
    sys.exit(main())
