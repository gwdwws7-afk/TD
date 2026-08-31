"""Fill generation-artifact diamond voids inside unit sprites.

Some AI generations left rhombus-shaped transparent holes in the middle
of bodies (user-visible as 'missing diamond' in-game). Fix strategy:

  1. detect enclosed interior holes with diamond signature
     (central location, bbox fill 0.35-0.65, size > 2500px)
  2. sibling transplant: if another frame of the same reel has body
     pixels covering the hole region (aligned by body-centroid offset),
     copy RGB+alpha from there with a 3px feather - keeps the
     hand-painted look and reel-consistent texture
  3. otherwise iterative boundary-fill inpaint (no cv2 dependency):
     grow content inward from the hole edge by repeated masked median
     filtering until the hole closes

Usage: python tools/fill_diamond_holes.py <frame> [frame ...]
       python tools/fill_diamond_holes.py --scan     (report only)
"""
import glob
import os
import re
import sys
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

ANIM = "Assets/Resources/Art/anim"
MIN_SIZE = 2500
FILL_LO, FILL_HI = 0.35, 0.65


def find_holes(al):
    body = al > 60
    if body.sum() < 5000:
        return []
    filled = ndimage.binary_fill_holes(body)
    holes = filled & ~body
    lab, n = ndimage.label(holes)
    out = []
    H, W = al.shape
    for i in range(1, n + 1):
        comp = lab == i
        size = int(comp.sum())
        if size < MIN_SIZE:
            continue
        ys, xs = np.where(comp)
        h, w = ys.max() - ys.min() + 1, xs.max() - xs.min() + 1
        fill = size / (h * w)
        cy, cx = ys.mean() / H, xs.mean() / W
        if 0.25 < cx < 0.75 and 0.25 < cy < 0.75 and FILL_LO <= fill <= FILL_HI:
            out.append(dict(mask=comp, size=size, fill=round(fill, 2)))
    return out


def reel_siblings(name):
    base = re.sub(r"_\d+$", "", name)
    return [os.path.basename(p)[:-4] for p in glob.glob(os.path.join(ANIM, base + "_*.png"))
            if "death" not in p and "fire" not in p and os.path.basename(p)[:-4] != name]


def body_centroid(al):
    ys, xs = np.where(al > 100)
    return (int(xs.mean()), int(ys.mean())) if len(xs) else (512, 512)


def transplant(frame, hole):
    """Try copying the hole region from a sibling frame; returns True on success."""
    al = np.array(Image.open(os.path.join(ANIM, frame + ".png")).convert("RGBA"))[:, :, 3]
    cx0, cy0 = body_centroid(al)
    for sib in reel_siblings(frame):
        sp = os.path.join(ANIM, sib + ".png")
        if not os.path.exists(sp):
            continue
        sim = np.array(Image.open(sp).convert("RGBA"))
        sal = sim[:, :, 3]
        cx1, cy1 = body_centroid(sal)
        dx, dy = cx0 - cx1, cy0 - cy1
        ys, xs = np.where(hole["mask"])
        sx, sy = xs + dx, ys + dy
        if sx.min() < 0 or sy.min() < 0 or sx.max() >= 1024 or sy.max() >= 1024:
            continue
        if (sal[sy, sx] > 100).mean() < 0.92:      # sibling must be solid there
            continue
        im = np.array(Image.open(os.path.join(ANIM, frame + ".png")).convert("RGBA"))
        # feathered paste
        feather = ndimage.binary_dilation(hole["mask"], iterations=3)
        soft = ndimage.gaussian_filter(feather.astype(float), 2.0) > 0.35
        src = np.roll(np.roll(sim, dy, axis=0), dx, axis=1)
        m = soft[..., None]
        im = (im * (1 - m) + src * m).astype(np.uint8)
        Image.fromarray(im, "RGBA").save(os.path.join(ANIM, frame + ".png"))
        return sib
    return None


def inpaint_hole(frame, hole):
    p = os.path.join(ANIM, frame + ".png")
    im = np.array(Image.open(p).convert("RGBA")).astype(np.float64)
    m = hole["mask"]
    # grow inward from boundary with median filtering until closed
    region = ndimage.binary_dilation(m, iterations=6)
    work = im.copy()
    todo = m.copy()
    guard = 0
    while todo.any() and guard < 400:
        edge = todo & ndimage.binary_erosion(~todo, iterations=1) == False
        edge = todo & ~ndimage.binary_erosion(todo)
        work_img = Image.fromarray(np.clip(work, 0, 255).astype(np.uint8), "RGBA")
        med = np.array(work_img.filter(ImageFilter.MedianFilter(5))).astype(np.float64)
        work[edge] = med[edge]
        todo[edge] = False
        guard += 1
    # smooth the filled zone
    zone = ndimage.binary_dilation(m, iterations=2)
    wimg = Image.fromarray(np.clip(work, 0, 255).astype(np.uint8), "RGBA")
    blur = np.array(wimg.filter(ImageFilter.GaussianBlur(4))).astype(np.float64)
    blend = ndimage.gaussian_filter(m.astype(float), 3.0)[..., None]
    work = work * (1 - blend) + blur * blend
    out = np.clip(work, 0, 255).astype(np.uint8)
    out[:, :, 3] = np.maximum(out[:, :, 3], np.where(m, 255, 0).astype(np.uint8))
    Image.fromarray(out, "RGBA").save(p)


def process(frame):
    p = os.path.join(ANIM, frame + ".png")
    al = np.array(Image.open(p).convert("RGBA"))[:, :, 3]
    holes = find_holes(al)
    if not holes:
        return None
    results = []
    for h in holes:
        sib = transplant(frame, h)
        if sib:
            results.append(f"transplant<-{sib}({h['size']}px)")
        else:
            inpaint_hole(frame, h)
            results.append(f"inpaint({h['size']}px)")
    # verify
    al2 = np.array(Image.open(p).convert("RGBA"))[:, :, 3]
    left = find_holes(al2)
    return results, len(left)


if __name__ == "__main__":
    if sys.argv[1] == "--scan":
        for p in sorted(glob.glob(ANIM + "/tower_*.png") + glob.glob(ANIM + "/enemy_*.png")):
            n = os.path.basename(p)[:-4]
            if "fire" in n:
                continue
            hs = find_holes(np.array(Image.open(p).convert("RGBA"))[:, :, 3])
            if hs:
                print(f"{n:44s} {[h['size'] for h in hs]}")
        sys.exit(0)
    for frame in sys.argv[1:]:
        r = process(frame)
        print(frame, r if r else "no diamond holes")
