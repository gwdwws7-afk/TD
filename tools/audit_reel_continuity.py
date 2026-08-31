"""Reel continuity audit for every frame-animation series in the game.

Groups anim/ frames into reels (tower idle/t2/t3/fire/t3_fire, enemy
idle/death, fx_* prefixes) and measures every consecutive pair plus the
loop closure (last -> first) for looping reel types:

  mask_diff   XOR of body masks / area       - silhouette jump
  cx/cy drift centroid movement (px)         - teleporting subject
  area_change solid-area delta (%)           - pop in/out
  rgb_delta   mean body-pixel color change   - palette flicker

Bounds are reel-type aware:
  loop  (idle/t2/t3)     subtle sway:  mask_diff <= 14%, drift <= 40px,
                          area_change <= 18%, rgb_delta <= 26
  burst (fire/t3_fire)   flash allowed: mask_diff <= 45%, drift <= 70px
  death (death reels)    collapse:      mask_diff <= 55%, drift <= 120px
  fx                      effect beat:   mask_diff <= 60%
Missing frames inside a reel are flagged as GAP.
"""
import glob
import os
import re
import sys
from collections import defaultdict
import numpy as np
from PIL import Image

ANIM = "Assets/Resources/Art/anim"
BOUNDS = {
    "loop":  dict(md=14, drift=40, area=18, rgb=26),
    "burst": dict(md=45, drift=70, area=45, rgb=40),
    "death": dict(md=55, drift=120, area=60, rgb=45),
    "fx":    dict(md=60, drift=150, area=80, rgb=50),
}


def frames_of(pattern):
    out = {}
    for p in glob.glob(os.path.join(ANIM, pattern)):
        m = re.search(r"_(\d+)\.png$", p)
        if m:
            out[int(m.group(1))] = p
    return out


def metrics(path_a, path_b):
    a = np.array(Image.open(path_a).convert("RGBA"))
    b = np.array(Image.open(path_b).convert("RGBA"))
    if a.shape != b.shape:
        return None
    am, bm = a[:, :, 3] > 100, b[:, :, 3] > 100
    union = am | bm
    md = (am ^ bm).sum() / max(union.sum(), 1) * 100
    def cent(m):
        ys, xs = np.where(m)
        return (xs.mean(), ys.mean()) if len(xs) else (0, 0)
    ax, ay = cent(am); bx, by = cent(bm)
    drift = ((bx - ax) ** 2 + (by - ay) ** 2) ** 0.5
    area = abs(bm.sum() - am.sum()) / max(am.sum(), 1) * 100
    both = am & bm
    rgb = float(np.abs(a[:, :, :3][both].astype(int) - b[:, :, :3][both].astype(int)).mean()) if both.any() else 999
    return dict(md=md, drift=drift, area=area, rgb=rgb)


def audit_reel(name, frames, kind, loops):
    if len(frames) < 2:
        return
    b = BOUNDS[kind]
    idxs = sorted(frames)
    gaps = [i for i in range(idxs[0], idxs[-1] + 1) if i not in frames]
    if gaps:
        print(f"GAP   {name:44s} missing frames {gaps}")
    pairs = [(i, i + 1) for i in idxs[:-1]]
    if loops and idxs[-1] != idxs[0]:
        pairs.append((idxs[-1], idxs[0], "loop-closure"))
    for pair in pairs:
        loop_tag = ""
        if len(pair) == 3:
            pair, loop_tag = pair[:2], f" [{pair[2]}]"
        i, j = pair
        m = metrics(frames[i], frames[j])
        if m is None:
            print(f"BROKEN {name}_{i}->{j}: size mismatch")
            continue
        bad = (m["md"] > b["md"] or m["drift"] > b["drift"] or m["area"] > b["area"] or m["rgb"] > b["rgb"])
        if bad or "-v" in sys.argv:
            print(f"{'JUMP' if bad else 'ok  '} {name:40s} {i:02d}->{j:02d}{loop_tag:14s} "
                  f"mask {m['md']:5.1f}% drift {m['drift']:5.1f}px area {m['area']:5.1f}% rgb {m['rgb']:5.1f}")


if __name__ == "__main__":
    reels = []
    for p in glob.glob(os.path.join(ANIM, "tower_*_00.png")):
        n = os.path.basename(p)[:-6].rstrip("_")
        base = re.sub(r"_(fire|t2|t3)$", "", n) if re.search(r"_(fire|t2|t3)$", n) else None
    # explicit grouping
    kinds = set()
    for p in glob.glob(os.path.join(ANIM, "tower_*.png")):
        m = re.match(r"tower_([a-z_]+?)_(t2_|t3_)?(t3_fire_|fire_)?\d+\.png$", os.path.basename(p))
        if m:
            kinds.add(m.group(1))
    for k in sorted(kinds):
        reels.append((f"tower_{k}", f"tower_{k}_*.png", "loop", True, fr"^tower_{k}_\d+"))
        reels.append((f"tower_{k}_t2", f"tower_{k}_t2_*.png", "loop", True, None))
        reels.append((f"tower_{k}_t3", f"tower_{k}_t3_*.png", "loop", True, None))
        reels.append((f"tower_{k}_fire", f"tower_{k}_fire_*.png", "burst", False, None))
        reels.append((f"tower_{k}_t3_fire", f"tower_{k}_t3_fire_*.png", "burst", False, None))
    ekinds = set()
    for p in glob.glob(os.path.join(ANIM, "enemy_*.png")):
        m = re.match(r"enemy_([a-z_]+?)_(death_)?\d+\.png$", os.path.basename(p))
        if m and "_" not in m.group(1).rstrip("_"):
            pass
        m2 = re.match(r"(enemy_[a-z_]+?)_\d+\.png$", os.path.basename(p))
        if m2 and "death" not in m2.group(1):
            ekinds.add(m2.group(1))
    for k in sorted(ekinds):
        reels.append((k, f"{k}_*.png", "loop", True, None))
        reels.append((f"{k}_death", f"{k}_death_*.png", "death", False, None))
    fxkinds = set()
    for p in glob.glob(os.path.join(ANIM, "fx_*.png")):
        m = re.match(r"(fx_[a-z_]+?)_\d+\.png$", os.path.basename(p))
        if m:
            fxkinds.add(m.group(1))
    for k in sorted(fxkinds):
        reels.append((k, f"{k}_*.png", "fx", False, None))

    for name, pattern, kind, loops, _ in reels:
        # enemy idle glob must not swallow death frames
        pat = pattern.replace("*", "[0-9]" * 2 if kind == "loop" and name.startswith("enemy") else "*")
        if kind == "loop" and name.startswith("enemy"):
            fr = {i: p for i, p in ((int(re.search(r'_(\d+)\.png$', p).group(1)), p)
                   for p in glob.glob(os.path.join(ANIM, pattern)) if "death" not in p)}
        else:
            fr = {}
            for p in glob.glob(os.path.join(ANIM, pattern)):
                m = re.search(r"_(\d+)\.png$", p)
                if m and "death" not in p or kind in ("death",):
                    if m and (kind == "death" or "death" not in p):
                        fr[int(m.group(1))] = p
        if fr:
            audit_reel(name, fr, kind, loops)
    print("\naudit complete")
