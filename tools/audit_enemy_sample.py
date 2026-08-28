"""C-2 enemy sample acceptance audit (design ruling: expansion-visual-identities-v1.md SS2-bu).

Per enemy kind, checks:
  1. Standard frame audit  - fog/edge-touch/alpha per idle/death frame
  2. Death-chain continuity vs idle_00 (enemy-specific chain risk):
     per stage, silhouette retention + centroid drift (grounded death,
     not a teleporting new creature) + area collapse trend
  3. Hue family band       - saturated-pixel hue share vs the enemy's
     ruled family band (design: family band first, silhouette second,
     FX third; tower-style 12-hue exclusivity does NOT apply)
  4. FX presence           - behavior readability frames are a spec item

Usage: python tools/audit_enemy_sample.py cinder_husk rail_splitter
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image
import colorsys

ANIM = Path("E:/TD/Assets/Resources/Art/anim")

# kind -> (family band hue range, identity accent hue, fx prefixes)
SPEC = {
    "cinder_husk":  ((0, 40), "bright orange joints #E8842A (hue ~30)", "fx_ember_pile"),
    "rail_splitter": ((8, 45), "steel-bright wedge head accents", "fx_speed_streak"),
    "acid_blister": ((60, 120), "yellow-green #C8E06A", "fx_acid"),
    "forge_dragoon": ((0, 40), "cold-steel shield #7FA8C4 accents", "fx_shield"),
    "ember_strider": ((0, 40), "leg-joint ember orange", "fx_mark"),
    "echo_brood": ((240, 290), "echo violet family 240-270", "fx_echo"),
}


def hue_stats(arr, sat_min=0.30, v_min=0.35):
    px = arr[arr[:, :, 3] > 200][:, :3].astype(float) / 255
    if len(px) < 100:
        return None
    hsv = np.array([colorsys.rgb_to_hsv(r, g, b) for r, g, b in px[:: max(1, len(px) // 5000)]])
    h, s, v = hsv[:, 0] * 360, hsv[:, 1], hsv[:, 2] * 255
    vivid = (s > sat_min) & (v > v_min)
    return h[vivid], vivid.mean() * 100


def frame_audit(path):
    a = np.array(Image.open(path).convert("RGBA"))
    al = a[:, :, 3]
    faint = ((al > 10) & (al <= 200)).mean() * 100
    ys, xs = np.where(al > 30)
    if not len(xs):
        return dict(flag="EMPTY")
    touch = xs.min() < 6 or xs.max() > 1017 or ys.min() < 6 or ys.max() > 1017
    return dict(faint=faint, touch=touch, bbox=(int(xs.min()), int(xs.max()), int(ys.min()), int(ys.max())),
                mask=al > 100, arr=a, flag="OK" if (faint <= 12 and not touch) else "FLAG")


def death_continuity(kind):
    idle = frame_audit(ANIM / f"enemy_{kind}_00.png")
    if "mask" not in idle:
        print(f"  idle_00 missing/empty")
        return
    im = idle["mask"]
    iy, ix = np.where(im)
    i_bot, i_cx = iy.max(), (ix.min() + ix.max()) / 2
    prev = im
    for st in range(4):
        p = ANIM / f"enemy_{kind}_death_{st:02d}.png"
        if not p.exists():
            print(f"  death_{st:02d}: MISSING")
            continue
        d = frame_audit(p)
        if "mask" not in d:
            print(f"  death_{st:02d}: EMPTY")
            continue
        dm = d["mask"]
        ret = (im & dm).sum() / max(im.sum(), 1) * 100          # silhouette retention vs idle
        prog = (prev & dm).sum() / max(prev.sum(), 1) * 100     # stage-to-stage continuity
        dy, dx = np.where(dm)
        bot_drift = abs(int(dy.max()) - int(i_bot))
        cx_drift = abs((dx.min() + dx.max()) / 2 - i_cx)
        area = dm.sum() / max(im.sum(), 1) * 100
        # Stage 2 of 4 may be the ruled "burst" beat (cinder_husk spec:
        # stricken -> collapse -> burst -> ember pile) - burst drops
        # silhouette retention by design; require locale instead.
        burst = st == 2
        if burst:
            verdict = "OK" if prog >= 10 and area <= 85 and cx_drift <= 320 and d["flag"] == "OK" else "REVIEW"
        else:
            verdict = "OK" if (ret >= 25 or area <= 60) and prog >= 45 and bot_drift <= 40 and d["flag"] == "OK" else "REVIEW"
        print(f"  death_{st:02d}: retain {ret:5.1f}% prog {prog:5.1f}% area {area:5.1f}% "
              f"bottom_drift {bot_drift}px cx_drift {cx_drift:4.0f}px faint {d.get('faint', -1):4.1f} -> {verdict}")
        prev = dm


def audit(kind):
    print(f"=== enemy_{kind}")
    band, accent, fx_pref = SPEC[kind]
    for i in range(8):
        p = ANIM / f"enemy_{kind}_{i:02d}.png"
        if not p.exists():
            print(f"  idle_{i:02d}: MISSING")
            continue
        r = frame_audit(p)
        hs = hue_stats(r.get("arr", np.zeros((4, 4, 4))))
        band_share = ((hs[0] >= band[0]) & (hs[0] < band[1])).mean() * 100 if hs is not None else -1
        print(f"  idle_{i:02d}: {r['flag']:4s} faint {r.get('faint', -1):4.1f}% touch {r.get('touch')} "
              f"family_band {band_share:4.0f}% vivid {hs[1] if hs else 0:4.1f}%")
    print("  death chain vs idle_00:")
    death_continuity(kind)
    fx = sorted(p.name for p in ANIM.glob(f"{fx_pref}*.png"))
    print(f"  behavior FX ({fx_pref}*): {len(fx)} files {fx[:4]}")


if __name__ == "__main__":
    for k in sys.argv[1:] or SPEC[:2]:
        audit(k)
