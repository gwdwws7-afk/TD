"""Generate the prebattle formation panel widget kit (spec v1, 11 assets).

Mirrors generate_worldmap.py: prompts live here (source of truth), raws in
output/imagegen/_formation_raw/, import chain = force_transparent_bg +
band-crop postprocess into Assets/Resources/Art/UI/Formation/.

State variants (roster selected/locked, doctrine_on, difficulty_on) are
generated as img2img edits of their base card so composition stays
pixel-consistent - the T2 reel trick applied to UI cards.

Usage:
  python tools/generate_formation.py
  python tools/generate_formation.py --only roster_card_selected
  python tools/generate_formation.py --import-only   (manual chat-AI raws)
"""

import argparse
import os
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
DST = ROOT / "Assets/Resources/Art/UI/Formation"
RAW = ROOT / "output/imagegen/_formation_raw"
IMAGE_GEN = Path.home() / ".codex/skills/.system/imagegen/scripts/image_gen.py"
APIKEY = ROOT / "Assets/apikey.txt"

STYLE = ("2D game UI asset, flat card background, forged-iron railway dispatch "
         "console style, dark charcoal weathered steel with subtle teal "
         "instrument light and amber trim, inset slots and grooves for overlaid "
         "icons and text (content areas blank), riveted metal details, "
         "nine-slice friendly borders, hand-painted, transparent background, "
         "no text, no icons, no watermark")

# Heavier material language for assets that came back too clean/plastic on
# the first pass (roster cards, intel card). Anchored generation + these
# keywords per the wave-2 style review.
STYLE_HEAVY = ("heavy forged-iron texture with visible brushed metal grain, "
               "dark charcoal patina with rust streaks and soot weathering, "
               "chunky rivets along the corners and edges, teal instrument "
               "rim lighting on the inner grooves, thin amber edge trim, "
               "hand-painted industrial brushwork (not vector-flat, not "
               "glossy plastic, not a modern minimal UI button)")

ROSTER_BASE = ("tower roster card: a square inset groove on the left sized "
               "for a gem badge, a two-line text area on the right, a "
               "horizontal status strip groove along the bottom, neutral "
               "unlit state")
THREAT = ("wide threat banner strip: a circular alarm-emblem socket on the "
          "left, one long text groove to the right, weathered tapered ends")
INTEL = ("tall intel card: a title slot groove along the top, one large body "
         "area below, surface one step lighter than a dark command frame, "
         "riveted corner details")
HEADER = ("short horizontal forged-iron ornament bar with riveted ends and a "
          "blank center band for overlaid text")

# Assets re-anchored after the wave-2 style review: these generate as edits
# of the in-style reference (threat_strip + doctrine_plate_on raws) so the
# material language matches the approved assets instead of drifting clean.
STYLE_ANCHORED = ["roster_card_base", "intel_card"]

PROMPTS = {
    "roster_card_base":     ROSTER_BASE + ", " + STYLE + ", " + STYLE_HEAVY
                           + ", match the exact forged-iron material language of the reference",
    "roster_card_selected": "using the provided roster card as the exact base, keep composition, proportions and slots identical, light the edge trim amber and make the bottom status groove glow warm amber, ember-lit selected state, keep the heavy forged-iron weathered material",
    "roster_card_locked":   "using the provided roster card as the exact base, keep composition, proportions and slots identical, darken the whole card to an inert unpowered look, add a small padlock-shaped socket groove at the lower-right corner, keep the heavy forged-iron weathered material",
    "doctrine_plate_base":  ("doctrine doctrine nameplate: a circular emblem "
                            "socket on the left, a two-line text area on the "
                            "right, toggle-switch feel, neutral unlit state, "
                            + STYLE),
    "doctrine_plate_on":    "using the provided doctrine nameplate as the exact base, keep composition identical, light the emblem socket amber and add a soft amber edge glow, engaged state",
    "difficulty_plate_base": ("campaign difficulty nameplate: an indicator-lamp "
                             "socket on the left, a single text area on the "
                             "right, neutral unlit state, " + STYLE),
    "difficulty_plate_on":  "using the provided difficulty nameplate as the exact base, keep composition identical, light the indicator lamp with teal instrument light, engaged state",
    "threat_strip":         THREAT + ", " + STYLE,
    "intel_card":           INTEL + ", " + STYLE + ", " + STYLE_HEAVY
                           + ", match the exact forged-iron material language of the reference",
    "header_ornament":      HEADER + ", " + STYLE,
}

# asset -> (gen_size, target, mode, base_dependency)
SPECS = {
    "roster_card_base":      ("1536x1024", (512, 288), "band", None),
    "roster_card_selected":  ("1536x1024", (512, 288), "band", "roster_card_base"),
    "roster_card_locked":    ("1536x1024", (512, 288), "band", "roster_card_base"),
    "doctrine_plate_base":   ("1536x1024", (560, 170), "band", None),
    "doctrine_plate_on":     ("1536x1024", (560, 170), "band", "doctrine_plate_base"),
    "difficulty_plate_base": ("1536x1024", (560, 140), "band", None),
    "difficulty_plate_on":   ("1536x1024", (560, 140), "band", "difficulty_plate_base"),
    "threat_strip":          ("1536x1024", (1536, 192), "band", None),
    "intel_card":            ("1024x1536", (768, 1024), "band", None),
    "header_ornament":       ("1536x1024", (512, 96), "band", None),
}

ORDER = ["roster_card_base", "roster_card_selected", "roster_card_locked",
         "doctrine_plate_base", "doctrine_plate_on",
         "difficulty_plate_base", "difficulty_plate_on",
         "threat_strip", "intel_card", "header_ornament"]


def ensure_key():
    if not os.environ.get("OPENAI_API_KEY") and APIKEY.exists():
        os.environ["OPENAI_API_KEY"] = APIKEY.read_text(encoding="utf-8").strip()
    if not os.environ.get("OPENAI_API_KEY"):
        sys.exit("OPENAI_API_KEY missing (env and Assets/apikey.txt)")


def postprocess(raw: Path, out: Path, target, mode: str):
    im = Image.open(raw).convert("RGBA")
    if mode == "band":
        a = np.asarray(im)[:, :, 3]
        ys, xs = np.nonzero(a > 24)
        if len(xs):
            pad = 12
            im = im.crop((max(0, xs.min()-pad), max(0, ys.min()-pad),
                          min(im.width, xs.max()+pad), min(im.height, ys.max()+pad)))
        scale = target[1] / im.height
        im = im.resize((max(1, int(im.width * scale)), target[1]), Image.LANCZOS)
        if im.width > target[0]:
            x0 = (im.width - target[0]) // 2
            im = im.crop((x0, 0, x0 + target[0], target[1]))
        else:
            canvas = Image.new("RGBA", target, (0, 0, 0, 0))
            canvas.paste(im, ((target[0] - im.width) // 2, 0), im)
            im = canvas
    out.parent.mkdir(parents=True, exist_ok=True)
    im.save(out)


def build_style_reference() -> Path:
    """Compose the two approved in-style raws (threat_strip + doctrine_plate_on)
    into a material-language anchor. Assets that drifted clean on the first
    pass generate as edits of this reference (the worldmap collage trick)."""
    ref = RAW / "formation_style_reference.png"
    if ref.exists():
        return ref
    RAW.mkdir(parents=True, exist_ok=True)
    top_src = RAW / "threat_strip.png"
    bot_src = RAW / "doctrine_plate_on.png"
    if not top_src.exists() or not bot_src.exists():
        sys.exit("style anchor needs approved raws: threat_strip.png + doctrine_plate_on.png in _formation_raw")
    canvas = Image.new("RGB", (1536, 1024), (24, 22, 20))
    top = Image.open(top_src).convert("RGB")
    top.thumbnail((1400, 480), Image.LANCZOS)
    canvas.paste(top, ((1536 - top.width) // 2, 20))
    bot = Image.open(bot_src).convert("RGB")
    bot.thumbnail((1100, 440), Image.LANCZOS)
    canvas.paste(bot, ((1536 - bot.width) // 2, 540))
    canvas.save(ref)
    return ref


def gen_one(name: str, force: bool, import_only: bool) -> bool:
    gen_size, target, mode, base = SPECS[name]
    final = DST / f"{name}.png"
    raw = RAW / f"{name}.png"
    if final.exists() and not force:
        print(f"  {name}: exists, skip")
        return True

    if import_only:
        if not raw.exists():
            print(f"  {name}: no raw in {RAW} for import-only")
            return False
        print(f"  {name}: importing raw -> {target[0]}x{target[1]}")
    else:
        cmd = None
        if base is not None:
            base_raw = RAW / f"{base}.png"
            if not base_raw.exists():
                print(f"  {name}: base raw {base_raw.name} missing - generate it first")
                return False
            cmd = [sys.executable, str(IMAGE_GEN), "edit",
                   "--model", "gpt-image-1.5",
                   "--image", str(base_raw),
                   "--prompt", PROMPTS[name],
                   "--size", gen_size]
        elif name in STYLE_ANCHORED:
            cmd = [sys.executable, str(IMAGE_GEN), "edit",
                   "--model", "gpt-image-1.5",
                   "--image", str(build_style_reference()),
                   "--prompt", PROMPTS[name],
                   "--size", gen_size]
        else:
            cmd = [sys.executable, str(IMAGE_GEN), "generate",
                   "--model", "gpt-image-1.5",
                   "--prompt", PROMPTS[name],
                   "--size", gen_size]
        cmd += ["--background", "transparent", "--output-format", "png",
                "--out", str(raw), "--force"]
        print(f"  {name}: {'edit of ' + base if base else ('style-anchored' if name in STYLE_ANCHORED else 'generate')} -> {target[0]}x{target[1]}")
        r = subprocess.run(cmd, capture_output=True, text=True)
        if r.returncode != 0 or not raw.exists():
            tail = (r.stderr or r.stdout or "").strip().splitlines()[-3:]
            print(f"    FAILED: {' | '.join(tail)}")
            return False

    tmp = RAW / f"{name}_t.png"
    r2 = subprocess.run([sys.executable, str(ROOT / "tools/force_transparent_bg.py"),
                         str(raw), str(tmp)], capture_output=True, text=True)
    if r2.returncode == 0 and tmp.exists():
        raw = tmp
    postprocess(raw, final, target, mode)
    return True


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", nargs="*", choices=PROMPTS)
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--import-only", action="store_true")
    args = ap.parse_args()

    RAW.mkdir(parents=True, exist_ok=True)
    names = args.only if args.only else ORDER
    if args.only:
        args.force = True
    if not args.import_only:
        ensure_key()
    failed = [n for n in names if not gen_one(n, args.force, args.import_only)]
    if failed:
        print(f"\n{len(failed)} asset(s) failed: {', '.join(failed)} (re-run to resume)")
        return 1
    print(f"\nformation kit complete: {len(names)} asset(s) in {DST}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
