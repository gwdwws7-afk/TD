"""Force a PNG's background to pure black, preserving the foreground subject.

Strategy:
  1. Sample the 4 image corners. Average to get the background color.
  2. Build a mask of "background-ish" pixels: pixels close to that color
     in RGB and with low saturation.
  3. Also include very light pixels (>220) regardless of corner color
     (catches white halos the model sometimes paints around subjects).
  4. Set those pixels to pure #000000.
  5. Save as a new file.

Usage:
    python force_black_bg.py <input.png> <output.png> [--tolerance 25]
"""

import sys
import argparse
from pathlib import Path
import numpy as np
from PIL import Image


def force_black(input_path: Path, output_path: Path, tolerance: int = 25):
    img = Image.open(input_path).convert("RGB")
    arr = np.array(img)
    h, w = arr.shape[:2]

    # Sample corners to estimate background color
    corners = np.array([
        arr[0, 0],
        arr[0, w - 1],
        arr[h - 1, 0],
        arr[h - 1, w - 1],
    ])
    bg = corners.mean(axis=0)
    print(f"  corner avg bg: {tuple(int(x) for x in bg)}")

    # Distance from each pixel to the corner-averaged background color
    diff = np.abs(arr.astype(np.int32) - bg.astype(np.int32))
    dist = diff.max(axis=2)  # use max channel diff for speed

    # Pixel is "background" if close to corner color OR very bright
    near_bg = dist < tolerance
    very_bright = (arr > 220).all(axis=2)
    bg_mask = near_bg | very_bright

    pct = bg_mask.mean() * 100
    print(f"  background-like pixels: {pct:.1f}%")

    out = arr.copy()
    out[bg_mask] = [0, 0, 0]
    Image.fromarray(out).save(output_path, "PNG")
    print(f"  saved -> {output_path}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input", type=Path)
    ap.add_argument("output", type=Path)
    ap.add_argument("--tolerance", type=int, default=25)
    args = ap.parse_args()
    force_black(args.input, args.output, args.tolerance)


if __name__ == "__main__":
    main()
