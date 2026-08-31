# Tower base-seam survey (2026-08-31 handover item 2)
#
# The 08-31 UI fit tour found a 1-2px bright seam where tower sprites meet the
# dark base plate (fixed on RailLancer only, commit f9985f4, via a 3px dark
# contact skirt). This script measures the same defect on every tower frame:
# bright, partially-transparent pixels in the last rows of each bottom-contact
# column read as a halo fringe over the dark plate.
#
# Calibrated against ground truth (RailLancer f9985f4 pre/post): the defect is
# partial-alpha pixels in the bottom contact rows (pre-fix 67-357 px, post-fix
# 0-78). Their colors are dark but read as a bright seam over the near-black
# plate, so fringe COUNT is the signal, not fringe luminance. The skirt works by
# painting 3 fresh opaque dark rows below the old bottom edge.
#
# Usage: python tools/_seam_survey.py [--calibrate]
import io
import subprocess
import sys
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parents[1]
ANIM = REPO / "Assets" / "Resources" / "Art" / "anim"
PREFIX = "f9985f4^"

# Frames f9985f4 treated (the confirmed-seam set).
RAIL_LANCER_FIXED = [
    *[f"tower_rail_lancer_{i:02d}" for i in range(6)],
    "tower_rail_lancer_t2_01", "tower_rail_lancer_t2_03", "tower_rail_lancer_t2_05",
    *[f"tower_rail_lancer_t3_{i:02d}" for i in range(6)],
]


def luminance(r, g, b):
    return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0


def analyze(im):
    im = im.convert("RGBA")
    w, h = im.size
    px = im.load()

    bottoms = []
    for x in range(w):
        b = -1
        for y in range(h - 1, -1, -1):
            if px[x, y][3] > 16:
                b = y
                break
        bottoms.append(b)

    valid = [x for x, b in enumerate(bottoms) if b >= 0]
    if not valid:
        return None
    gmax = max(bottoms[x] for x in valid)

    # Columns resting near the sprite's lowest point; frames whose contact
    # width is a sliver (recoil/fire poses) were excluded from the skirt
    # treatment too, so they are reported separately rather than judged.
    contact = [x for x in valid if bottoms[x] >= gmax - 6]
    narrow = len(contact) < w * 0.05

    fringe = bright = 0
    maxlum = 0.0
    for x in contact:
        b = bottoms[x]
        for y in range(max(0, b - 2), b + 1):
            r, g, bl, a = px[x, y]
            if 16 < a < 235:
                fringe += 1
                lum = luminance(r, g, bl)
                maxlum = max(maxlum, lum)
                if lum > 0.55:
                    bright += 1
    return {
        "narrow": narrow,
        "contact_cols": len(contact),
        "fringe": fringe,
        "bright": bright,
        "maxlum": round(maxlum, 2),
    }


def git_frame(stem):
    raw = subprocess.run(
        ["git", "show", f"{PREFIX}:Assets/Resources/Art/anim/{stem}.png"],
        cwd=REPO, capture_output=True).stdout
    return Image.open(io.BytesIO(raw)) if raw else None


def calibrate():
    print("calibration: RailLancer pre-fix (seam) vs post-fix (clean)")
    print(f"{'frame':34s} {'narrow':6s} {'fringe':>6s} {'bright':>6s} {'maxlum':>6s}")
    for stem in RAIL_LANCER_FIXED:
        pre = analyze(git_frame(stem))
        post = analyze(Image.open(ANIM / f"{stem}.png"))
        row = lambda tag, m: (f"{tag:17s}{str(m['narrow']):>6s}"
                              f"{m['fringe']:>7d}{m['bright']:>7d}{m['maxlum']:>7.2f}")
        print(f"{stem:34s}")
        print("  " + row("pre (seam)", pre))
        print("  " + row("post (clean)", post))


def survey():
    stems = sorted(p.stem for p in ANIM.glob("tower_*.png"))
    towers = {}
    for s in stems:
        base = s
        for tag in ("_t3", "_t2"):
            if base.endswith(tag) and not base.endswith(tag + "_fire"):
                base = base[: -len(tag)]
                break
        # strip trailing _NN and any _fire_NN
        parts = base.split("_")
        while parts and (parts[-1].isdigit() or (len(parts) > 2 and parts[-1] == "fire")):
            parts.pop()
        towers.setdefault("_".join(parts), []).append(s)

    print(f"{'tower':22s} {'idle/t2/t3 seam frames':>22s} {'fire':>5s} {'narrow':>6s}  worst non-fire frames (partial-alpha px)")
    flagged = {}
    for tower in sorted(towers):
        frames = sorted(towers[tower])
        seam = fire = narrow = 0
        worst = []
        for s in frames:
            m = analyze(Image.open(ANIM / f"{s}.png"))
            if m is None:
                continue
            if m["narrow"]:
                narrow += 1
                continue
            # 100px boundary: treated-but-clean RailLancer t3 idles carry
            # 72-78 px; confirmed-seam pre-fix frames start at 67 but the
            # visually-confirmed band tops out at 78, so 100 separates.
            if m["fringe"] >= 100:
                if "_fire_" in s:
                    fire += 1
                else:
                    seam += 1
                    worst.append((m["fringe"], s))
        worst.sort(reverse=True)
        flag = f"{seam}/{len([s for s in frames if '_fire_' not in s]) - narrow}"
        print(f"{tower:22s} {flag:>22s} {fire:>5d} {narrow:>6d}  "
              + ", ".join(f"{s}({b})" for b, s in worst[:4]))
        if seam:
            flagged[tower] = worst
    return flagged


if __name__ == "__main__":
    if "--calibrate" in sys.argv:
        calibrate()
    else:
        survey()
