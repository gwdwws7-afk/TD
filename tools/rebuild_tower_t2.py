"""Rebuild tower T2 idle frames with protected body pixels.

Unlike fire reels (transient effect over the unchanged body), T2 is a
persistent body upgrade: 1-2 new modules bolted onto the base tower.
Strategy per frame i (00..05):

  1. Read AI-generated T2 frame  tower_{kind}_t2_{ii}.png
  2. Read the matching idle frame tower_{kind}_{ii}.png  (per-frame base,
     so the idle animation motion carries into the T2 reel)
  3. Per-pixel diff |AI - idle|:
       high diff + AI content  -> "module" pixels (the T2 upgrade layer)
       low diff                -> body -> restore ORIGINAL idle pixels
  4. Morphological cleanup on the module mask (open then close) so stray
     speckles don't survive and small module fragments stay coherent
  5. Composite: idle body + module layer (alpha-over), feathered 2px

Result: everything except the bolted-on modules is pixel-identical to the
base idle reel, which preserves the 70%-shared-silhouette design rule and
keeps the T3 jump visually exclusive. Run AFTER force_transparent_bg.py.
"""

import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter

SRC_DIR = Path("E:/TD/Assets/Resources/Art/anim")
DST_DIR = SRC_DIR  # overwrite in place
TARGET = 1024
DIFF_THRESH = 85   # |AI - idle| per channel above this = module pixel.
                    # Identity-color modules read >=90 against the dark
                    # body palette; AI body-repaint drift stays <=40, so
                    # 85 separates both directions with margin.
FEATHER = 2         # px mask feather where module meets body

FRAMES = range(6)

KINDS = [
    "rail_lancer", "cinder_mortar", "frost_coil", "arc_welder",
    "siege_drill", "ember_flak", "resonance_beacon", "grav_snare",
]


def extract_and_composite(ai_path: Path, idle_path: Path, out_path: Path,
                          threshold: int = DIFF_THRESH) -> float:
    ai = np.array(Image.open(ai_path).convert("RGBA").resize(
        (TARGET, TARGET), Image.LANCZOS))
    idle = np.array(Image.open(idle_path).convert("RGBA").resize(
        (TARGET, TARGET), Image.LANCZOS))

    ai_rgb = ai[..., :3].astype(np.int32)
    idle_rgb = idle[..., :3].astype(np.int32)
    diff = np.abs(ai_rgb - idle_rgb).max(axis=2)
    ai_content = ai[..., 3] > 30

    # Module pixels: either recolored well past body-palette jitter (the
    # spec highlights modules in tower identity colors), or extending
    # beyond the base silhouette (idle transparent there) — structural
    # add-ons like hydraulic legs are body-colored but protrude.
    idle_empty = idle[..., 3] <= 30
    raw_mask = ai_content & ((diff > threshold) | idle_empty)

    # Morphological cleanup via PIL: open (remove speckle), close (heal
    # pinholes inside modules)
    m = Image.fromarray((raw_mask * 255).astype(np.uint8))
    m = m.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))  # open
    m = m.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))  # close
    m = m.filter(ImageFilter.GaussianBlur(FEATHER))
    module = (np.array(m).astype(np.float64) / 255.0)[..., None]

    out = (ai.astype(np.float64) * module +
           idle.astype(np.float64) * (1.0 - module))
    out = np.clip(out, 0, 255).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(out_path)

    return float((module[..., 0] > 0.5).mean() * 100.0)


def main() -> int:
    only = sys.argv[1:] or KINDS
    rc = 0
    print(f"{'kind':<20s} {'frame':>5s} {'module%':>8s}")
    for kind in only:
        for i in FRAMES:
            ai = SRC_DIR / f"tower_{kind}_t2_{i:02d}.png"
            idle = SRC_DIR / f"tower_{kind}_{i:02d}.png"
            if not ai.exists():
                print(f"{kind:<20s} {i:02d} MISSING {ai.name}")
                rc = 1
                continue
            pct = extract_and_composite(ai, idle, ai)
            print(f"{kind:<20s} {i:02d} {pct:7.1f}")
            # sanity: module footprint should be modest (spec: 1-2 bolt-on
            # modules, ~70% silhouette shared). >45% means the AI redrew
            # the whole tower; flag for manual review.
            if pct > 45.0:
                print(f"  !! module layer {pct:.1f}% > 45% — body was likely redrawn, review this frame")
    return rc


if __name__ == "__main__":
    sys.exit(main())
