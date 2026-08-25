"""Full production driver for the content expansion (visual identities v1).

Batches C-1 (4 towers x24 frames + projectile/impact/UI), C-2 (6 enemies
idle 8 + death 4 + behavior FX 6), C-3 (4 bosses x10 frames + per-boss
warning FX 10 + phase FX 6 + portraits x5 bosses), D (enemy death reels).
All generation chains reuse the proven techniques: per-frame bases,
tier chaining (idle -> t2 -> t3), fire-as-overlay, style anchors.

Resumable per asset; raws in output/imagegen/_expansion_raw/.
  python tools/generate_expansion.py --batch towers
  python tools/generate_expansion.py --batch enemies
  python tools/generate_expansion.py --batch bosses
  python tools/generate_expansion.py --batch portraits
  python tools/generate_expansion.py --only tower_slag_burner_00 ...
  ... --import-only  (manual chat-AI route)
"""

import argparse
import os
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ANIM = ROOT / "Assets/Resources/Art/anim"
COMBAT = ROOT / "Assets/Resources/Art/Combat/P11"
UIP11 = ROOT / "Assets/Resources/Art/UI/P11"
BOSSDIR = ROOT / "Assets/Resources/Art/Boss"
RAW = ROOT / "output/imagegen/_expansion_raw"
IMAGE_GEN = Path.home() / ".codex/skills/.system/imagegen/scripts/image_gen.py"
APIKEY = ROOT / "Assets/apikey.txt"

TOWER_STYLE = ("2D game sprite, single tower, hand-painted, dark industrial "
               "ember-belt style, weathered charcoal and rust-brown riveted "
               "metal, glowing amber energy details, fully transparent "
               "background, centered, three-quarter view, no text, no watermark")
ENEMY_STYLE = ("2D game sprite, single enemy creature, hand-painted, dark "
               "organic-industrial ember-belt style, muted carapace tones with "
               "ember accents, fully transparent background, centered side "
               "view, no text, no watermark")
BOSS_STYLE = ("2D game sprite, single colossal boss, hand-painted, dark "
              "industrial ember-belt style, massive silhouette with glowing "
              "ember core as the only bright source, fully transparent "
              "background, centered, no text, no watermark")
FX_STYLE = ("2D game VFX sprite, hand-painted glowing energy effect, dark "
            "ember-belt palette with saturated core glow, transparent "
            "background, centered, no text, no watermark")

TOWERS = {
    "slag_burner": dict(
        color="slag-red #D64545",
        body="low-slung rail-mounted furnace wagon with a long spray-lance angled 30 degrees up, back-mounted furnace hump with swept-back chimney, small pilot flame always lit",
        t2="add twin spray nozzles and a side slag-tank with a glowing red-hot molten level window",
        t3="transform into a rolling smelting kiln: triple rotating lance barrels, enlarged furnace body, ground slag halo beneath",
        projectile="flat molten-slag fire tongue with dripping embers",
        impact="splashing slag pool with scorch ring",
    ),
    "salvage_derrick": dict(
        color="salvage-green #7FC86E",
        body="rail-mounted lattice boom crane with a magnetic grapple claw reaching forward and a counterweight block at the rear, cables with subtle sag",
        t2="add a secondary jib arm and a sorting hopper with a green glowing salvage window",
        t3="transform into a gantry with dual arms, topped by a salvage beacon orb pulsing green light",
        projectile="hooked claw shot on a taut cable",
        impact="bounty vortex pulling glints of scrap inward",
    ),
    "rail_barricade": dict(
        color="hazard steel-blue #5D8AA8 with yellow-black hazard stripes",
        body="tracked armored rail vehicle with a wedge plow blade up front and a roof searchlight, diagonal hazard stripes on the hull",
        t2="add side-deploying wing gates and a crumple-buffer honeycomb layer",
        t3="transform into a full-width double-hull barricade: two vehicles riveted side by side, detonation preheat pipes glowing dull red",
        projectile=None,
        impact="ramming dust storm with metal shards",
    ),
    "long_rail_cannon": dict(
        color="deep-space violet #6C5CE7",
        body="extra-long twin-rail cannon on a heavy anchored base, multi-stage accelerator rings along the rails, rear grounding stakes with cables, barrel angled 15 degrees up",
        t2="add two more accelerator rings and a loading magazine drum",
        t3="transform with rails extending beyond frame edge, violet lightning arcing permanently along the rails",
        projectile="piercing violet plasma spike",
        impact="line-puncture ionization trail, blue-violet",
    ),
}

ENEMIES = {
    "cinder_husk": dict(
        body="shambling charred humanoid husk, ember cracks glowing at the joints, dragging gait",
        fx="ember_pile", fx_desc="small pile of glowing embers left on the ground, warm orange glow, embers fading over time"),
    "rail_splitter": dict(
        body="flat segmented worm slithering along a rail, wedge-shaped head, segment plates catching light",
        fx="speed_streak", fx_desc="blue-white speed streak trail behind a fast mover, straight-line dash emphasis"),
    "acid_blister": dict(
        body="bloated translucent acid sac creature, yellow-green fluid with visible bubbles inside, slow crawl",
        fx="acid_burst", fx_desc="green acid splash cloud with corrosion ring spreading outward"),
    "forge_dragoon": dict(
        body="heavy armored rider construct on stumpy legs, layered shield plates, forge-red seams between plates",
        fx="shield_shatter", fx_desc="layered shield breaking apart in three sequential stages, metal shards flying"),
    "ember_strider": dict(
        body="tall thin fast bipedal strider with long legs, ember trail underfoot, faint mark-sensitive runes on the body",
        fx="marked_ignite", fx_desc="red targeting flare igniting over a marked creature, damage-amp glow"),
    "echo_brood": dict(
        body="cluster of small insect creatures moving as one blob, ghostly afterimage duplicates trailing behind",
        fx="echo_split", fx_desc="ghostly duplicate splitting off from a dying creature, fading echo copies"),
}

BOSSES = {
    "containermaw": dict(
        body="colossal container-handling beast, port-crane monster with a jaw that holds shipping containers, hydraulic limbs, hazard markings",
        phase="armor plates breaking off in stages, containers smashing down to lock a build cell",
    ),
    "junction_tyrant": dict(
        body="switch-point warlord construct wielding a turnout-lever scepter, split-bodied silhouette hinting its division ability",
        phase="body splitting into two identical halves each walking a separate lane",
    ),
    "kiln_custodian": dict(
        body="kiln golem with ever-thickening slag crust armor, furnace core visible through vents",
        phase="slag armor pulsing thicker layer by layer, then venting purge",
    ),
    "echo_harbinger": dict(
        body="amorphous herald core with floating weaponized afterimages mimicking tower armaments",
        phase="mimicry swap flash then a cleansing shockwave clearing its debuffs",
    ),
    "furnace_matriarch": dict(
        body="moving fortress matriarch: wide squat heavy armor on six legs or tracks, furnace core the only bright source, two peelable shell layers readable as phase thresholds",
        phase=None,  # portrait-only (existing boss, reels already shipped)
    ),
}

IDLE_MOTION = ("next idle animation frame of the exact same subject: keep "
               "body, silhouette, position and ground contact identical, only "
               "subtle mechanical sway, drifting steam or flickering lights")
DEATH_STAGES = [
    "the death moment: destruction just beginning, pose continuous with the living frame",
    "destruction mid-progress, body breaking apart, embers scattering",
    "near-wreck, slumped geometry, dimming glow",
    "final wreckage freeze, dark inert remains, faint dying embers",
]
FIRE_STAGES = [
    "firing wind-up: energy gathering at the muzzle, faint glow",
    "peak discharge: full muzzle flash with bright projectile leaving, slight recoil",
    "afterglow: smoke and fading embers settling",
]
BOSS_WARN = ("boss warning telegraph for this specific boss: expanding alarm "
             "ring with hazard chevrons in the boss's identity color, rising "
             "sparks, frame {i} of 10 building to a peak flash")

# ── registry ─────────────────────────────────────────────────────────────
A = []  # dicts: name, batch, prompt, gen, target, dst, mode, dep

def add(name, batch, prompt, gen, target, dst, mode, dep=None):
    A.append(dict(name=name, batch=batch, prompt=prompt, gen=gen,
                  target=target, dst=dst, mode=mode, dep=dep))

for k, d in TOWERS.items():
    base = f"{d['body']}, accent glow in {d['color']}, {TOWER_STYLE}"
    add(f"tower_{k}_00", "towers", base, "1024x1024", (1024,1024), ANIM, "direct")
    for i in range(1, 6):
        add(f"tower_{k}_{i:02d}", "towers", IDLE_MOTION, "1024x1024", (1024,1024), ANIM, "direct", f"tower_{k}_{i-1:02d}")
    for i in range(6):
        add(f"tower_{k}_t2_{i:02d}", "towers",
            f"upgrade this exact tower to its tier-2 form: {d['t2']}, modest mid-tier enhancement in {d['color']}",
            "1024x1024", (1024,1024), ANIM, "direct", f"tower_{k}_{i:02d}")
    for i in range(6):
        add(f"tower_{k}_t3_{i:02d}", "towers",
            f"final tier transformation: {d['t3']}, dramatic full upgrade",
            "1024x1024", (1024,1024), ANIM, "direct", f"tower_{k}_t2_{i:02d}")
    for i, stage in enumerate(FIRE_STAGES):
        add(f"tower_{k}_fire_{i:02d}", "towers",
            f"firing effect on this exact tower, body unchanged: {stage}, effect in {d['color']}",
            "1024x1024", (1024,1024), ANIM, "direct", f"tower_{k}_00")
    for i, stage in enumerate(FIRE_STAGES):
        add(f"tower_{k}_t3_fire_{i:02d}", "towers",
            f"firing effect on this exact tier-3 tower, body unchanged: {stage}, effect in {d['color']}",
            "1024x1024", (1024,1024), ANIM, "direct", f"tower_{k}_t3_00")
    if d["projectile"]:
        add(f"projectile_{k}", "towers", d["projectile"] + ", " + FX_STYLE, "1024x1024", (128,128), COMBAT, "small")
    add(f"impact_{k}", "towers", d["impact"] + ", " + FX_STYLE, "1024x1024", (128,128), COMBAT, "small")
    add(f"tower_{k}", "towers",
        f"gem-badge UI icon for the {k.replace('_',' ')} tower, round riveted metal badge with glowing {d['color']} core, blank core center for overlaid text",
        "1024x1024", (128,128), UIP11, "small")

for k, d in ENEMIES.items():
    base = f"{d['body']}, {ENEMY_STYLE}"
    add(f"enemy_{k}_00", "enemies", base, "1024x1024", (1024,1024), ANIM, "direct")
    for i in range(1, 8):
        add(f"enemy_{k}_{i:02d}", "enemies", IDLE_MOTION, "1024x1024", (1024,1024), ANIM, "direct", f"enemy_{k}_{i-1:02d}")
    for i, st in enumerate(DEATH_STAGES):
        add(f"enemy_{k}_death_{i:02d}", "enemies",
            f"death sequence of this exact creature, {st}, {ENEMY_STYLE}",
            "1024x1024", (1024,1024), ANIM, "direct", f"enemy_{k}_00")
    for i in range(6):
        frac = ["faint start", "building", "peak", "early fade", "late fade", "final wisp"][i]
        add(f"fx_{d['fx']}_{i:02d}", "enemies",
            f"{d['fx_desc']}, animation stage: {frac}, {FX_STYLE}",
            "1024x1024", (1024,1024), ANIM, "direct")

for k, d in BOSSES.items():
    if d.get("body") and k != "furnace_matriarch":
        base = f"{d['body']}, {BOSS_STYLE}"
        add(f"boss_{k}_00", "bosses", base, "1024x1024", (1024,1024), ANIM, "direct")
        for i in range(1, 10):
            add(f"boss_{k}_{i:02d}", "bosses", IDLE_MOTION, "1024x1024", (1024,1024), ANIM, "direct", f"boss_{k}_{i-1:02d}")
        for i in range(10):
            add(f"fx_boss_warning_{k}_{i:02d}", "bosses",
                BOSS_WARN.format(i=i) + f", {BOSS_STYLE}", "1024x1024", (1024,1024), ANIM, "direct")
        if d["phase"]:
            for i in range(6):
                frac = ["trigger flash", "buildup", "peak", "resolve", "settle", "residue"][i]
                add(f"fx_phase_{k}_{i:02d}", "bosses",
                    f"phase-change effect: {d['phase']}, stage: {frac}, {FX_STYLE}",
                    "1024x1024", (1024,1024), ANIM, "direct")
    # portraits (all five bosses)
    add(f"boss_{k}_portrait", "portraits",
        f"bust portrait of the {k.replace('_',' ')} boss: {d['body']}, ember core the only bright source, upper-weighted composition, {BOSS_STYLE}",
        "1024x1024", (2048,2048), BOSSDIR, "direct")
    add(f"boss_{k}_fullbody", "portraits",
        f"full-body battle pose of the {k.replace('_',' ')} boss: {d['body']}, {BOSS_STYLE}",
        "1024x1024", (2048,2048), BOSSDIR, "direct")
    add(f"boss_{k}_icon", "portraits",
        f"iconic silhouette emblem of the {k.replace('_',' ')} boss, readable at small size, {BOSS_STYLE}",
        "1024x1024", (512,512), BOSSDIR, "direct")

BY_NAME = {a["name"]: a for a in A}

# ── execution ─────────────────────────────────────────────────────────────

def ensure_key():
    if not os.environ.get("OPENAI_API_KEY") and APIKEY.exists():
        os.environ["OPENAI_API_KEY"] = APIKEY.read_text(encoding="utf-8").strip()
    if not os.environ.get("OPENAI_API_KEY"):
        sys.exit("OPENAI_API_KEY missing (env and Assets/apikey.txt)")


def postprocess(raw: Path, out: Path, target, mode: str):
    im = Image.open(raw).convert("RGBA")
    # fog guard (proven in the formation r2 lesson)
    arr = np.array(im)  # writable copy (np.asarray is read-only under newer numpy/PIL)
    a = arr[:, :, 3]
    ys, xs = np.nonzero(a >= 160)
    if len(xs) and im.width > 64:
        m = np.ones(a.shape, bool)
        y0, y1 = max(0, ys.min()-16), min(a.shape[0], ys.max()+16)
        x0, x1 = max(0, xs.min()-16), min(a.shape[1], xs.max()+16)
        m[y0:y1, x0:x1] = False
        aa = arr[:, :, 3].copy()
        aa[m & (aa > 0)] = 0
        arr[:, :, 3] = aa
        im = Image.fromarray(arr)
    if mode == "small":
        im = im.resize(target, Image.LANCZOS)
    elif mode == "direct" and im.size != target:
        im = im.resize(target, Image.LANCZOS)
    out.parent.mkdir(parents=True, exist_ok=True)
    im.save(out)


def gen_one(asset: dict, force: bool, import_only: bool) -> bool:
    final = asset["dst"] / f"{asset['name']}.png"
    raw = RAW / f"{asset['name']}.png"
    if final.exists() and not force:
        return True
    if import_only:
        if not raw.exists():
            print(f"  {asset['name']}: no raw for import-only")
            return False
    else:
        dep_raw = RAW / f"{asset['dep']}.png" if asset["dep"] else None
        if asset["dep"] and not dep_raw.exists():
            print(f"  {asset['name']}: dependency raw {asset['dep']} missing")
            return False
        if asset["dep"]:
            cmd = [sys.executable, str(IMAGE_GEN), "edit", "--model", "gpt-image-1.5",
                   "--image", str(dep_raw), "--prompt", asset["prompt"], "--size", asset["gen"]]
        else:
            cmd = [sys.executable, str(IMAGE_GEN), "generate", "--model", "gpt-image-1.5",
                   "--prompt", asset["prompt"], "--size", asset["gen"]]
        cmd += ["--background", "transparent", "--output-format", "png",
                "--out", str(raw), "--force"]
        r = subprocess.run(cmd, capture_output=True, text=True)
        if r.returncode != 0 or not raw.exists():
            tail = (r.stderr or r.stdout or "").strip().splitlines()[-3:]
            print(f"  {asset['name']}: FAILED {' | '.join(tail)}")
            return False
    tmp = RAW / f"{asset['name']}_t.png"
    r2 = subprocess.run([sys.executable, str(ROOT / "tools/force_transparent_bg.py"),
                         str(raw), str(tmp)], capture_output=True, text=True)
    if r2.returncode == 0 and tmp.exists():
        raw = tmp
    postprocess(raw, final, asset["target"], asset["mode"])
    print(f"  {asset['name']}: ok")
    return True


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--batch", choices=["towers", "enemies", "bosses", "portraits"])
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--import-only", action="store_true")
    args = ap.parse_args()
    RAW.mkdir(parents=True, exist_ok=True)

    jobs = A
    if args.batch:
        jobs = [a for a in A if a["batch"] == args.batch]
    if args.only:
        args.force = True
        jobs = [BY_NAME[n] for n in args.only]
    if not args.import_only:
        ensure_key()
    failed = [a["name"] for a in jobs if not gen_one(a, args.force, args.import_only)]
    print(f"\n{len(jobs) - len(failed)}/{len(jobs)} ok"
          + (f"; failed: {', '.join(failed)}" if failed else ""))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
