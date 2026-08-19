"""Generate the 48-frame tower T2 batch (8 towers x 6 idle frames).

Red line (per design/spec/tower-t2-visual-spec-v1.md + session plan v6):
frame _t2_ii is generated FROM tower_{kind}_ii.png as the img2img base —
each tower uses 6 different bases so the idle motion phase carries into
the T2 reel. Do NOT generate everything from frame 00.

Pipeline per frame:
  1. image_gen.py edit (gpt-image-1.5, 1024x1024, transparent png)
       raw output -> output/imagegen/_t2_raw/
  2. force_transparent_bg.py -> Assets/Resources/Art/anim/ final slot
  3. (after all frames) rebuild_tower_t2.py composites module layers
     over the original idle pixels (body protection)

Resumable: existing raw outputs are skipped unless --force.
API key: OPENAI_API_KEY env, falling back to Assets/apikey.txt.
Network note: needs direct reachability of api.openai.com (the openai
SDK honors http_proxy/https_proxy if a local proxy is available).

Usage:
  python tools/generate_tower_t2.py --dry-run
  python tools/generate_tower_t2.py                    # all 48
  python tools/generate_tower_t2.py --kinds rail_lancer frost_coil
  python tools/generate_tower_t2.py --skip-composite   # generation only
"""

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ANIM = ROOT / "Assets/Resources/Art/anim"
RAW = ROOT / "output/imagegen/_t2_raw"
IMAGE_GEN = Path.home() / ".codex/skills/.system/imagegen/scripts/image_gen.py"
APIKEY = ROOT / "Assets/apikey.txt"

MODULES = {
    "rail_lancer":      ("an extended twin-rail barrel and a side-mounted loading ratchet",
                          "steel-blue #408ADB"),
    "cinder_mortar":    ("a secondary barrel and an armored magazine shroud",
                          "burnt-orange #D46930"),
    "frost_coil":       ("a ring of heat-sink fins and a second coil winding",
                          "cyan #4FC2E8"),
    "arc_welder":       ("two floating electrode arms and a grounded cable",
                          "teal #38CCC2"),
    "siege_drill":      ("a thickened drill head and hydraulic stabilizer legs",
                          "gold #D6A638"),
    "ember_flak":       ("an ammo-link drum and a muzzle brake",
                          "orange-red #EE6933"),
    "resonance_beacon": ("a ring antenna array and a small relay orb",
                          "green #69CE73"),
    "grav_snare":       ("a phase ring and anchoring claws",
                          "blue-violet #7384E3"),
}

PROMPT_TMPL = (
    "using the provided tower sprite as the exact base, keep the tower "
    "body, pose, proportions and ground contact pixel-identical, upgrade "
    "it to a mid-tier version by adding {modules}: new parts rendered in "
    "{color} accent color with subtle emissive glow, existing structures "
    "unchanged, modest enhancement level (this is tier 2 of 3, the final "
    "tier stays far more dramatic), 2D game sprite, hand-painted, dark "
    "industrial ember-belt style, fully transparent background, PNG "
    "alpha, no text, no watermark"
)


def ensure_key():
    if not os.environ.get("OPENAI_API_KEY") and APIKEY.exists():
        os.environ["OPENAI_API_KEY"] = APIKEY.read_text(encoding="utf-8").strip()
    if not os.environ.get("OPENAI_API_KEY"):
        sys.exit("OPENAI_API_KEY missing (env and Assets/apikey.txt)")


def gen_one(kind: str, frame: int, force: bool) -> bool:
    raw_out = RAW / f"tower_{kind}_t2_{frame:02d}.png"
    if raw_out.exists() and not force:
        print(f"  {kind} {frame:02d}: raw exists, skip")
    else:
        base = ANIM / f"tower_{kind}_{frame:02d}.png"
        if not base.exists():
            print(f"  {kind} {frame:02d}: BASE MISSING {base.name}")
            return False
        modules, color = MODULES[kind]
        cmd = [
            sys.executable, str(IMAGE_GEN), "edit",
            "--model", "gpt-image-1.5",
            "--image", str(base),
            "--prompt", PROMPT_TMPL.format(modules=modules, color=color),
            "--size", "1024x1024", "--background", "transparent",
            "--output-format", "png",
            "--out", str(raw_out), "--force",
        ]
        print(f"  {kind} {frame:02d}: base={base.name} -> {raw_out.name}")
        r = subprocess.run(cmd, capture_output=True, text=True)
        if r.returncode != 0 or not raw_out.exists():
            tail = (r.stderr or r.stdout or "").strip().splitlines()[-3:]
            print(f"    FAILED: {' | '.join(tail)}")
            return False
    # post: transparent bg -> final slot
    final = ANIM / f"tower_{kind}_t2_{frame:02d}.png"
    r = subprocess.run([sys.executable, str(ROOT / "tools/force_transparent_bg.py"),
                        str(raw_out), str(final)], capture_output=True, text=True)
    if r.returncode != 0:
        print(f"    transparent-bg FAILED: {r.stderr.strip()[-200:]}")
        return False
    return True


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--kinds", nargs="*", choices=MODULES, default=list(MODULES))
    ap.add_argument("--frames", type=int, nargs="*", default=list(range(6)))
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--skip-composite", action="store_true",
                    help="skip the final rebuild_tower_t2.py pass")
    args = ap.parse_args()

    RAW.mkdir(parents=True, exist_ok=True)
    if args.dry_run:
        for kind in args.kinds:
            for i in args.frames:
                base = ANIM / f"tower_{kind}_{i:02d}.png"
                print(f"[dry] tower_{kind}_t2_{i:02d}.png  base={base.name}  exists={base.exists()}")
        return 0

    ensure_key()
    failed = []
    for kind in args.kinds:
        for i in args.frames:
            if not gen_one(kind, i, args.force):
                failed.append(f"tower_{kind}_t2_{i:02d}")
    if failed:
        print(f"\n{len(failed)} frame(s) failed: {', '.join(failed)}")
        print("re-run to resume (completed frames are skipped)")
        return 1

    if not args.skip_composite:
        print("\n-- compositing (body protection) --")
        return subprocess.run([sys.executable,
                               str(ROOT / "tools/rebuild_tower_t2.py")]
                              + args.kinds).returncode
    return 0


if __name__ == "__main__":
    sys.exit(main())
