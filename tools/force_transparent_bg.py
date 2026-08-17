"""Convert a PNG's background to fully transparent, preserving the foreground
subject. Outputs RGBA PNG.

Strategy:
  1. Read the image as RGB.
  2. Sample the 4 corners to estimate the background color.
  3. Compute per-pixel Chebyshev distance to the corner-averaged color.
  4. Build an alpha channel:
     - distance < tolerance: alpha 0 (fully transparent)
     - distance in [tolerance, tolerance+soft_range]: linear ramp
     - distance >= tolerance+soft_range: alpha 255 (fully opaque)
  5. Also explicitly zero out near-white pixels (R,G,B all > 230) regardless
     of distance -- catches white halos the model sometimes paints around
     subjects. These pixels are usually on the edge of the subject anyway,
     so the soft alpha is preserved.
  6. Save as RGBA PNG.

Usage:
    python force_transparent_bg.py <input.png> <output.png>
    python force_transparent_bg.py --dir <dir>  (process all .png in dir)
"""

import argparse
import sys
from pathlib import Path
import numpy as np
from PIL import Image


def force_transparent(input_path: Path, output_path: Path,
                      tolerance: int = 30, soft_range: int = 40,
                      white_cutoff: int = 235) -> dict:
    img = Image.open(input_path).convert("RGB")
    arr = np.array(img).astype(np.int32)
    h, w = arr.shape[:2]

    corners = np.array([
        arr[0, 0],
        arr[0, w - 1],
        arr[h - 1, 0],
        arr[h - 1, w - 1],
    ])
    bg = corners.mean(axis=0)
    bg_int = tuple(int(x) for x in bg)

    diff = np.abs(arr - bg)
    dist = diff.max(axis=2)

    alpha = np.clip((dist - tolerance) * 255 / max(1, soft_range), 0, 255).astype(np.uint8)

    # Also kill near-white pixels (catches white halos the model adds)
    very_bright = (arr > white_cutoff).all(axis=2)
    alpha[very_bright] = 0

    rgba = np.dstack([arr.astype(np.uint8), alpha])
    Image.fromarray(rgba, "RGBA").save(output_path, "PNG")

    return {
        "size": (w, h),
        "corner_bg": bg_int,
        "transparent_pct": float((alpha == 0).mean() * 100),
        "opaque_pct": float((alpha == 255).mean() * 100),
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input", nargs="?", type=Path)
    ap.add_argument("output", nargs="?", type=Path)
    ap.add_argument("--dir", dest="dir_path", type=Path,
                    help="process all .png in this directory (in-place overwrite)")
    ap.add_argument("--tolerance", type=int, default=30)
    ap.add_argument("--soft-range", type=int, default=40)
    ap.add_argument("--white-cutoff", type=int, default=235)
    args = ap.parse_args()

    if args.dir_path:
        files = sorted(args.dir_path.glob("*.png"))
        if not files:
            print(f"no png in {args.dir_path}", file=sys.stderr)
            return 1
        for f in files:
            try:
                stat = force_transparent(f, f, args.tolerance, args.soft_range, args.white_cutoff)
                print(f"  {f.name:<40} bg={stat['corner_bg']} "
                      f"trans={stat['transparent_pct']:.1f}% opaque={stat['opaque_pct']:.1f}%")
            except Exception as e:
                print(f"  {f.name}: FAIL {e}")
        return 0

    if not args.input or not args.output:
        ap.error("provide input and output, or use --dir")
    stat = force_transparent(args.input, args.output,
                             args.tolerance, args.soft_range, args.white_cutoff)
    print(f"  {args.output}  bg={stat['corner_bg']} "
          f"trans={stat['transparent_pct']:.1f}% opaque={stat['opaque_pct']:.1f}%")
    return 0


if __name__ == "__main__":
    sys.exit(main())
