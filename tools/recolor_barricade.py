"""Recolor rail_barricade armor to the design-ruled vigil steel-blue.

Design ruling 8858544 (v13 task 1): main armor plates return to
vigil steel blue #5D8AA8 with volume gradient #46697F..#7FA8C4,
rust demoted to <=15% accents, amber demoted to functional lights,
yellow-black warning stripes and the T3 preheat-glow stay. Hue/sat
layer only - alpha and silhouette untouched, no regeneration.

Kept (never migrated):
  - yellow warning stripes   hue 40-70, sat > 0.42 (dilated 1px)
  - bright amber lights      hue 16-48, sat > 0.50, V > 200
  - specular whites          sat < 0.12, V > 235
  - T3 preheat glow cores    hue < 16 or > 344, sat > 0.40, V >= 160
                             (dilated 6px to keep the halo; the tube
                             hardware itself migrates like armor)
  - fire frames: the warm   hue 8-55, sat > 0.30, V > 120 flash body
    (whole-reel effect color, not armor)

Migration (everything else opaque):
  - hue   -> 207 (steel)
  - sat   -> 0.46 dark .. 0.32 bright (V-linked)
  - value -> piecewise lift so the median armor lands on the spec
             shadow tone: (9,64) (54,118) (118,158) (200,198) (255,255)
  - near-neutral grays keep sat <= 0.20 (steel tint, not blue paint)
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage

ANIM = Path("E:/TD/Assets/Resources/Art/anim")
TARGET_HUE = 207.0
V_KNOTS = np.array([9, 54, 118, 200, 255], float)
V_OUT = np.array([64, 118, 158, 198, 255], float)
FRAMES = [f"tower_rail_barricade_{i:02d}" for i in range(5)] \
       + [f"tower_rail_barricade_t2_{i:02d}" for i in range(4)] \
       + ["tower_rail_barricade_t3_00"] \
       + [f"tower_rail_barricade_fire_{i:02d}" for i in range(3)]


def rgb_to_hsv_np(rgb):  # rgb float [..,3] in 0..1
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx, mn = rgb.max(-1), rgb.min(-1)
    d = mx - mn
    h = np.zeros_like(mx)
    m = (mx == r) & (d > 1e-6); h[m] = ((g - b)[m] / d[m]) % 6
    m = (mx == g) & (d > 1e-6); h[m] = (b - r)[m] / d[m] + 2
    m = (mx == b) & (d > 1e-6); h[m] = (r - g)[m] / d[m] + 4
    return h * 60.0, np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0), mx


def hsv_to_rgb_np(h, s, v):  # h degrees
    c = v * s
    hp = (h % 360.0) / 60.0
    x = c * (1 - np.abs(hp % 2 - 1))
    z = np.zeros_like(c)
    r = np.select([hp < 1, hp < 2, hp < 3, hp < 4, hp < 5], [c, x, z, z, x], z + c)
    g = np.select([hp < 1, hp < 2, hp < 3, hp < 4, hp < 5], [x, c, c, x, z], z)
    b = np.select([hp < 1, hp < 2, hp < 3, hp < 4, hp < 5], [z, z, x, c, c], z)
    m = v - c
    return np.stack([r + m, g + m, b + m], -1)


def recolor(path: Path, fire_frame: bool, t3_frame: bool) -> dict:
    im = np.array(Image.open(path).convert("RGBA")).astype(np.float64)
    rgb = im[..., :3] / 255.0
    h, s, v = rgb_to_hsv_np(rgb)
    vd = v * 255.0

    keep = (s < 0.12) & (vd > 235)                       # speculars
    keep |= (h >= 40) & (h < 70) & (s > 0.42)            # warning stripes
    keep |= (h >= 16) & (h < 48) & (s > 0.50) & (vd > 200)  # amber lights
    if t3_frame:
        core = (((h < 16) | (h > 344)) & (s > 0.40) & (vd >= 160))
        keep |= ndimage.binary_dilation(core, iterations=6)   # preheat halos
    if fire_frame:
        keep |= (h >= 8) & (h < 55) & (s > 0.30) & (vd > 120)  # flash body

    migrate = (im[..., 3] > 0) & ~keep

    v_new = np.interp(vd, V_KNOTS, V_OUT) / 255.0
    s_new = np.clip(0.46 - np.clip((vd - 100) / 255.0 * 0.14, 0, 0.14), 0.30, 0.48)
    s_new = np.where(s < 0.12, np.minimum(s_new, 0.20), s_new)  # grays stay subtle

    out = im.copy()
    mm = migrate
    out[..., :3][mm] = (hsv_to_rgb_np(
        np.full_like(h[mm], TARGET_HUE), s_new[mm], v_new[mm]) * 255)
    Image.fromarray(out.astype(np.uint8), "RGBA").save(path)
    return {"migrated_px": int(mm.sum()),
            "kept_px": int((keep & (im[..., 3] > 0)).sum())}


if __name__ == "__main__":
    names = sys.argv[1:] or FRAMES
    for n in names:
        path = ANIM / f"{n}.png"
        if not path.exists():
            print(f"{n}: missing, skip")
            continue
        before = np.array(Image.open(path).convert("RGBA"))[..., 3].copy()
        r = recolor(path, fire_frame="_fire_" in n, t3_frame="_t3_" in n)
        after = np.array(Image.open(path).convert("RGBA"))[..., 3]
        ys, xs = np.where(after > 30)
        print(f"{n:34s} migrated {r['migrated_px']:7d}  kept {r['kept_px']:7d}  "
              f"alpha_identical={np.array_equal(before, after)}  "
              f"bbox x[{xs.min()},{xs.max()}] y[{ys.min()},{ys.max()}]")
