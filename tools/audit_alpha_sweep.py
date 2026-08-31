"""Repo-wide alpha quality sweep for all game art.

Every PNG under Assets/Resources/Art is classified:
  opaque-by-design  - no transparency at all (map surfaces, full-frame
                      backgrounds, branding) -> PASS, out of scope
  clean             - transparent asset, corners clear, fog low
  FOG               - faint alpha (10-200) covers > 8% of canvas
  CORNER            - corner pixels non-zero (dirty bleed into margins)
  EDGE              - solid content touches the canvas border
  SPECK             - isolated opaque specks far from the main body
  EMPTY             - fully transparent / broken

Usage: python tools/audit_alpha_sweep.py [--fix]
  --fix applies the proven body-anchored defog + speck cleanup to FOG/
  SPECK files in place (alpha only, RGB untouched) and re-reports.
"""
import glob
import sys
import os
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = "Assets/Resources/Art"

def analyze(path):
    try:
        im = Image.open(path)
    except Exception:
        return "BROKEN", {}
    if im.mode != "RGBA":
        return "opaque", {}                      # no alpha channel
    a = np.array(im.convert("RGBA"))
    al = a[:, :, 3]
    if (al >= 250).all():
        return "opaque", {}                      # fully opaque by design
    if (al <= 2).all():
        return "EMPTY", {}
    m = {}
    h, w = al.shape
    corners = [al[:12, :12].max(), al[:12, -12:].max(), al[-12:, :12].max(), al[-12:, -12:].max()]
    m["corner_max"] = int(max(corners))
    m["faint_pct"] = float(((al > 10) & (al <= 200)).mean() * 100)
    m["solid_pct"] = float((al > 200).mean() * 100)
    ys, xs = np.where(al > 30)
    if not len(xs):
        return "EMPTY", m
    m["touch"] = bool(xs.min() < 4 or xs.max() > w - 5 or ys.min() < 4 or ys.max() > h - 5)
    # specks: solid components disconnected from the main body by > 30px
    solid = al > 150
    lab, n = ndimage.label(solid)
    if n > 1:
        sizes = ndimage.sum(solid, lab, range(1, n + 1))
        main = np.argmax(sizes) + 1
        main_mask = lab == main
        near = ndimage.binary_dilation(main_mask, iterations=30)
        speck_px = int(sum(sizes[i - 1] for i in range(1, n + 1) if i != main and not (near & (lab == i)).any()))
        m["speck_px"] = speck_px
    else:
        m["speck_px"] = 0
    issues = []
    if m["corner_max"] > 40:
        issues.append("CORNER")
    if m["faint_pct"] > 8:
        issues.append("FOG")
    if m["touch"] and m["solid_pct"] > 3:
        issues.append("EDGE")
    if m["speck_px"] > 400:
        issues.append("SPECK")
    return ("clean" if not issues else "+".join(issues)), m


def fix(path):
    """Body-anchored defog + speck removal (alpha only)."""
    im = np.array(Image.open(path).convert("RGBA"))
    al = im[:, :, 3]
    solid = al > 150
    lab, n = ndimage.label(solid)
    if not n:
        return False
    sizes = ndimage.sum(solid, lab, range(1, n + 1))
    main = np.argmax(sizes) + 1
    body = lab == main
    keep = ndimage.binary_dilation(ndimage.binary_closing(body, iterations=3), iterations=8)
    # keep secondary components that touch the body halo (limbs/modules)
    for i in range(1, n + 1):
        if i == main:
            continue
        comp = lab == i
        if (ndimage.binary_dilation(comp, iterations=12) & body).any():
            keep |= comp
    im[:, :, 3] = np.where(keep, al, 0)
    Image.fromarray(im, "RGBA").save(path)
    return True


if __name__ == "__main__":
    do_fix = "--fix" in sys.argv
    files = sorted(glob.glob(ROOT + "/**/*.png", recursive=True))
    buckets = {}
    flagged = []
    for p in files:
        status, m = analyze(p)
        buckets.setdefault(status, []).append(p)
        if status not in ("clean", "opaque"):
            flagged.append((status, p, m))
    for status in sorted(buckets, key=lambda s: -len(buckets[s])):
        print(f"{status:24s} {len(buckets[status]):4d}")
    print(f"{'TOTAL':24s} {len(files):4d}")
    print("\n--- flagged detail ---")
    for status, p, m in sorted(flagged, key=lambda x: (x[0], x[1])):
        print(f"{status:16s} {os.path.relpath(p, ROOT):58s} "
              f"faint {m.get('faint_pct', -1):5.1f}% corner {m.get('corner_max', -1):3d} "
              f"speck {m.get('speck_px', 0):6d} edge {m.get('touch', False)}")
    if do_fix:
        fixable = [p for s, p, m in flagged if ("FOG" in s or "SPECK" in s)]
        print(f"\n--fix: processing {len(fixable)} files")
        for p in fixable:
            fix(p)
        print("re-auditing fixed files:")
        still = 0
        for p in fixable:
            status, m = analyze(p)
            if status not in ("clean",):
                print(f"  STILL {status:12s} {os.path.relpath(p, ROOT)}")
                still += 1
        print(f"fixed clean: {len(fixable) - still}/{len(fixable)}")
