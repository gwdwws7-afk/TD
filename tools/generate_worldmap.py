"""Generate the campaign world-map art batch (spec v2, plan v9).

Two waves (the world map gates wave 2 - landmarks must be painted against
the terrain the map establishes):

  wave 1: world_map_bg        (1 asset)
  wave 2: landmarks L01..L20, node badges x5, seal pips x2,
          region plate, meta entry button, meta panel frame,
          meta node slot, campaign title plate, rail strip

gpt-image size constraints: square/landscape gens at 1024x1024 or
1536x1024, then post-process (16:9 crop + LANCZOS upscale for the bg,
content-band crop for wide assets, downscale for small assets).

Resumable per asset; raws in output/imagegen/_worldmap_raw/.
API key: OPENAI_API_KEY env or Assets/apikey.txt (same contract as
generate_tower_t2.py; needs api.openai.com reachability or a proxy in
http_proxy/https_proxy).

Usage:
  python tools/generate_worldmap.py --wave 1
  python tools/generate_worldmap.py --wave 2
  python tools/generate_worldmap.py --wave 2 --only landmark_L01 node_available
"""

import argparse
import os
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
DST = ROOT / "Assets/Resources/Art/UI/Campaign"
RAW = ROOT / "output/imagegen/_worldmap_raw"
IMAGE_GEN = Path.home() / ".codex/skills/.system/imagegen/scripts/image_gen.py"
APIKEY = ROOT / "Assets/apikey.txt"

STYLE = ("2D game art, hand-painted, dark industrial post-apocalyptic "
         "ember-belt style, weathered charcoal and rust-brown metal with "
         "glowing amber-orange accents, subtle teal energy details, "
         "painterly texture with soft volumetric glow, no text, no watermark")

BG_PROMPT = (
    "top-down hand-painted fantasy-industrial world map, five natural "
    "terrain regions connected by one winding main railway line: cold-grey "
    "rail junction plains with signal towers, ash-desert with depot cranes, "
    "ochre slot canyon with trestle bridges, dark-red volcanic kiln basin "
    "with slag flows, near-black derelict terminus with one last signal "
    "lamp; natural region transitions via river valleys and ridges, mid-low "
    "contrast center, warm light on the journey corridor, corner vignette, "
    "each region keeps breathing room for four level anchors, " + STYLE
)

REGION_PALETTES = {
    "grayline junction plains": "cold-grey weathered steel, slate concrete, thin teal signal lights",
    "ashfall depot": "warm ash-greys, drifting ember motes, rust-orange cranes",
    "split switch canyon": "ochre and rust-red rock strata, dark timber trestles",
    "hollow kiln basin": "dark volcanic basalt, glowing kiln-red slag, soot-stained iron",
    "last ember terminus": "near-black derelict structures, one warm amber signal glow",
}

REGIONS = {
    "L01": "grayline junction plains: signal tower", "L02": "grayline junction plains: locomotive shed",
    "L03": "grayline junction plains: water tower",   "L04": "grayline junction plains: inspection pit",
    "L05": "ashfall depot: loading crane",            "L06": "ashfall depot: ore silo",
    "L07": "ashfall depot: garage hall",              "L08": "ashfall depot: watch silo",
    "L09": "split switch canyon: trestle bridge",     "L10": "split switch canyon: suspension bridge",
    "L11": "split switch canyon: sentry tower",       "L12": "split switch canyon: tunnel mouth",
    "L13": "hollow kiln basin: blast furnace",        "L14": "hollow kiln basin: pipe bridge",
    "L15": "hollow kiln basin: ore cart tipper",      "L16": "hollow kiln basin: chimney cluster",
    "L17": "last ember terminus: terminus hall",      "L18": "last ember terminus: great lamp tower",
    "L19": "last ember terminus: carriage depot",     "L20": "last ember terminus: memorial monument",
}

WAVE1 = {"world_map_bg": BG_PROMPT}

WAVE2 = {
    **{f"landmark_{lid}":
       f"small landmark vignette for a tower-defense level set in {theme.split(':')[0]} "
       f"({REGION_PALETTES[theme.split(':')[0]]}): "
       f"a distinct {theme.split(':')[1]}, silhouette-readable at small size, "
       + STYLE + ", fully transparent background"
       for lid, theme in REGIONS.items()},
    "node_available": "round heavy metal badge with riveted rim and an amber glowing railway-signal core, " + STYLE + ", transparent background",
    "node_cleared":   "round heavy metal badge with riveted rim and a green sealed-stamp glowing core, " + STYLE + ", transparent background",
    "node_locked":    "round heavy metal badge with riveted rim, dark unlit core with a small padlock and chain, " + STYLE + ", transparent background",
    "node_boss":      "oversized round heavy metal badge with hazard stripes and a red glowing beast-skull core, " + STYLE + ", transparent background",
    "node_selected":  "thin gold highlight ring, hollow center, subtle ember sparks on the rim, " + STYLE + ", transparent background",
    "seal_pip":        "small neutral metal seal medallion, unlit, ready for tinting, " + STYLE + ", transparent background",
    "seal_pip_empty":  "small dark empty seal socket groove, " + STYLE + ", transparent background",
    "region_plate":    "wide horizontal forged-iron nameplate strip with amber trim and rivets, gently weathered, nine-slice friendly, " + STYLE + ", transparent background",
    "meta_entry_button": "square forged-iron button with an amber ember-residue emblem (crystal shard in a cog), " + STYLE + ", transparent background",
    "meta_panel_frame": "large UI panel frame, forged-iron border with amber trim, top currency strip zone, four horizontal upgrade-line rows inside, bottom bar, blank content areas, nine-slice friendly, " + STYLE + ", transparent background",
    "meta_node_slot":  "small hexagonal upgrade node slot, neutral metal, unlit, ready for tinting, " + STYLE + ", transparent background",
    "campaign_title_plate": "wide horizontal title nameplate, forged iron with amber trim, blank center for overlaid text, " + STYLE + ", transparent background",
    "path_rail_strip": "horizontal luminous rail track strip, twin rails with a faintly glowing groove between them, medium brightness ready for tinting, tileable left-right, " + STYLE + ", transparent background",
}

# post-process per asset: (gen_size, target, mode)
#  mode 'bg': 16:9 crop + upscale; 'small': downscale; 'band': content-crop + fit height; 'direct': as-is
SPECS = {
    "world_map_bg": ("1536x1024", (2048, 1152), "bg"),
    **{f"landmark_{lid}": ("1024x1024", (384, 384), "small") for lid in REGIONS},
    "node_available": ("1024x1024", (512, 512), "small"),
    "node_cleared":   ("1024x1024", (512, 512), "small"),
    "node_locked":    ("1024x1024", (512, 512), "small"),
    "node_boss":      ("1024x1024", (640, 640), "small"),
    "node_selected":  ("1024x1024", (512, 512), "small"),
    "seal_pip":        ("1024x1024", (128, 128), "small"),
    "seal_pip_empty":  ("1024x1024", (128, 128), "small"),
    "region_plate":    ("1536x1024", (1024, 192), "band"),
    "meta_entry_button": ("1024x1024", (384, 384), "small"),
    "meta_panel_frame":  ("1536x1024", (1536, 1024), "direct"),
    "meta_node_slot":    ("1024x1024", (192, 192), "small"),
    "campaign_title_plate": ("1536x1024", (1536, 180), "band"),
    "path_rail_strip": ("1536x1024", (1024, 128), "band"),
}


def ensure_key():
    if not os.environ.get("OPENAI_API_KEY") and APIKEY.exists():
        os.environ["OPENAI_API_KEY"] = APIKEY.read_text(encoding="utf-8").strip()
    if not os.environ.get("OPENAI_API_KEY"):
        sys.exit("OPENAI_API_KEY missing (env and Assets/apikey.txt)")


def postprocess(raw: Path, out: Path, target, mode: str, transparent: bool):
    im = Image.open(raw).convert("RGBA")
    if mode == "bg":
        w, h = im.size
        th = int(w * 9 / 16)
        top = max(0, (h - th) // 2 - int(h * 0.04))  # bias slightly up
        im = im.crop((0, top, w, top + th)).resize(target, Image.LANCZOS)
    elif mode == "small":
        im = im.resize(target, Image.LANCZOS)
    elif mode == "band":
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


def build_reference() -> Path:
    """Compose the five painted map surfaces into a 1536x1024 journey-layout
    guide so the world map generates as an img2img anchored to the existing
    terrain language (acceptance requires matching the map_surface set)."""
    ref = RAW / "world_map_reference.png"
    if ref.exists():
        return ref
    RAW.mkdir(parents=True, exist_ok=True)
    maps = ["grayline_junction", "ashfall_depot", "split_switch_canyon",
            "hollow_kiln_basin", "last_ember_terminus"]
    canvas = Image.new("RGB", (1536, 1024), (30, 28, 26))
    # snake layout: 3 top, 2 bottom (journey reading order)
    slots = [(0, 0), (512, 0), (1024, 0), (896, 512), (384, 512)]
    for mid, (x, y) in zip(maps, slots):
        src = ROOT / f"Assets/Resources/Art/map_surface_{mid}_16x9.png"
        im = Image.open(src).convert("RGB")
        im = im.resize((500, 281), Image.LANCZOS)  # 16:9 thumb in a 512 slot
        canvas.paste(im, (x + 6, y + 6))
    canvas.save(ref)
    return ref


def gen_one(name: str, prompt: str, force: bool, import_only: bool = False) -> bool:
    gen_size, target, mode = SPECS[name]
    transparent = name != "world_map_bg"
    final = DST / f"{name}.png"
    raw = RAW / f"{name}.png"
    if final.exists() and not force:
        print(f"  {name}: exists, skip")
        return True
    if import_only:
        # manual route: the raw was generated elsewhere (any chat AI) and
        # dropped into _worldmap_raw/{name}.png - only post-process it.
        if not raw.exists():
            print(f"  {name}: no raw in {RAW} for import-only")
            return False
        print(f"  {name}: importing raw -> {target[0]}x{target[1]} ({mode})")
    else:
        cmd = [sys.executable, str(IMAGE_GEN), "generate",
               "--model", "gpt-image-1.5",
               "--prompt", prompt,
               "--size", gen_size]
        if name == "world_map_bg":
            # img2img against the terrain reference (style + region anchor)
            cmd = [sys.executable, str(IMAGE_GEN), "edit",
                   "--model", "gpt-image-1.5",
                   "--image", str(build_reference()),
                   "--prompt", BG_PROMPT + " - transform this reference collage of "
                   "the five region terrains into one continuous painted world map, "
                   "keeping each region's palette and material language",
                   "--size", gen_size]
        if transparent:
            cmd += ["--background", "transparent", "--output-format", "png"]
        cmd += ["--out", str(raw), "--force"]
        print(f"  {name}: gen {gen_size} -> {target[0]}x{target[1]} ({mode})")
        r = subprocess.run(cmd, capture_output=True, text=True)
        if r.returncode != 0 or not raw.exists():
            tail = (r.stderr or r.stdout or "").strip().splitlines()[-3:]
            print(f"    FAILED: {' | '.join(tail)}")
            return False
    if transparent:
        tmp = RAW / f"{name}_t.png"
        r2 = subprocess.run([sys.executable, str(ROOT / "tools/force_transparent_bg.py"),
                             str(raw), str(tmp)], capture_output=True, text=True)
        if r2.returncode == 0 and tmp.exists():
            raw = tmp
    postprocess(raw, final, target, mode, transparent)
    return True


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--wave", type=int, choices=(1, 2))
    ap.add_argument("--only", nargs="*", help="re-run specific assets regardless of wave")
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--import-only", action="store_true",
                    help="skip the API: post-process raws already placed in "
                         "_worldmap_raw/{name}.png (manual chat-AI route)")
    args = ap.parse_args()

    RAW.mkdir(parents=True, exist_ok=True)
    if args.wave is None and not args.only:
        sys.exit("specify --wave 1|2 or --only <assets>")
    jobs = dict(WAVE1) if args.wave == 1 else dict(WAVE2)
    if args.only:
        args.force = True
        jobs = {k: v for k, v in {**WAVE1, **WAVE2}.items() if k in args.only}

    if not args.import_only:
        ensure_key()
    failed = []
    for name, prompt in jobs.items():
        if not gen_one(name, prompt, args.force, args.import_only):
            failed.append(name)
    if failed:
        print(f"\n{len(failed)} asset(s) failed: {', '.join(failed)} (re-run to resume)")
        return 1
    print(f"\nwave {args.wave} complete: {len(jobs)} asset(s) in {DST}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
