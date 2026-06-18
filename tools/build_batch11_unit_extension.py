from __future__ import annotations

import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ANIM_DIR = ROOT / "Assets" / "Resources" / "Art" / "anim"
MASTER_OUT_DIR = ROOT / "output" / "imagegen" / "batch11_units_masters"
CONTACT_SHEET_PATH = ROOT / "output" / "imagegen" / "batch11_units_contact_sheet.png"
LIVE_MASTER_DIR = ROOT / "output" / "imagegen" / "batch11_units_live"

SOURCE_DIRS = [
    ROOT / "output" / "imagegen" / "units_v4_cutout",
    ROOT / "output" / "imagegen" / "units_v4_cut",
    ROOT / "output" / "imagegen" / "units_v4_raw",
]

BASE_MASTERS = {
    "tower_rail_lancer": "tower_rail_lancer_master.png",
    "tower_cinder_mortar": "tower_cinder_mortar_master.png",
    "tower_frost_coil": "tower_frost_coil_master.png",
    "enemy_skitter_runner": "enemy_skitter_runner_master.png",
    "enemy_carapace_brute": "enemy_carapace_brute_master.png",
    "enemy_ash_swarm": "enemy_ash_swarm_master.png",
    "enemy_plated_spore": "enemy_plated_spore_master.png",
}

UNIT_SPECS = {
    "tower_rail_lancer": {
        "frames": 6,
        "motion": {"rot": 1.8, "tx": 5.0, "ty": 2.0, "scale": 0.018, "bright": 0.06},
        "existing": True,
    },
    "tower_cinder_mortar": {
        "frames": 6,
        "motion": {"rot": 1.4, "tx": 4.0, "ty": 2.0, "scale": 0.014, "bright": 0.09},
        "existing": True,
    },
    "tower_frost_coil": {
        "frames": 6,
        "motion": {"rot": 1.6, "tx": 4.0, "ty": 2.0, "scale": 0.017, "bright": 0.07},
        "existing": True,
    },
    "tower_arc_welder": {
        "frames": 6,
        "source": "tower_rail_lancer",
        "motion": {"rot": 2.0, "tx": 5.0, "ty": 2.0, "scale": 0.018, "bright": 0.08},
        "grade": {"sat": 1.26, "contrast": 1.12, "bright": 1.04, "tint": (0.62, 1.18, 1.10)},
        "motif": "arc",
    },
    "tower_siege_drill": {
        "frames": 6,
        "source": "tower_cinder_mortar",
        "motion": {"rot": 1.5, "tx": 4.2, "ty": 1.8, "scale": 0.013, "bright": 0.06},
        "grade": {"sat": 1.05, "contrast": 1.14, "bright": 0.98, "tint": (1.08, 0.94, 0.78)},
        "motif": "drill",
    },
    "tower_ember_flak": {
        "frames": 6,
        "source": "tower_cinder_mortar",
        "motion": {"rot": 1.8, "tx": 4.8, "ty": 2.2, "scale": 0.017, "bright": 0.12},
        "grade": {"sat": 1.20, "contrast": 1.10, "bright": 1.05, "tint": (1.16, 1.00, 0.78)},
        "motif": "flak",
    },
    "tower_resonance_beacon": {
        "frames": 6,
        "source": "tower_frost_coil",
        "motion": {"rot": 1.5, "tx": 3.6, "ty": 1.6, "scale": 0.015, "bright": 0.08},
        "grade": {"sat": 1.14, "contrast": 1.08, "bright": 1.03, "tint": (0.86, 1.20, 0.84)},
        "motif": "beacon",
    },
    "tower_grav_snare": {
        "frames": 6,
        "source": "tower_frost_coil",
        "motion": {"rot": 1.7, "tx": 3.2, "ty": 1.8, "scale": 0.014, "bright": 0.07},
        "grade": {"sat": 1.10, "contrast": 1.16, "bright": 0.99, "tint": (0.84, 0.92, 1.20)},
        "motif": "grav",
    },
    "enemy_skitter_runner": {
        "frames": 8,
        "motion": {"rot": 3.0, "tx": 9.0, "ty": 4.0, "scale": 0.012, "bright": 0.05},
        "existing": True,
    },
    "enemy_carapace_brute": {
        "frames": 6,
        "motion": {"rot": 2.1, "tx": 5.0, "ty": 3.0, "scale": 0.010, "bright": 0.04},
        "existing": True,
    },
    "enemy_ash_swarm": {
        "frames": 8,
        "motion": {"rot": 4.2, "tx": 6.0, "ty": 5.0, "scale": 0.016, "bright": 0.08},
        "existing": True,
    },
    "enemy_plated_spore": {
        "frames": 6,
        "motion": {"rot": 2.4, "tx": 6.0, "ty": 3.0, "scale": 0.012, "bright": 0.06},
        "existing": True,
    },
    "enemy_burrow_sapper": {
        "frames": 8,
        "source": "enemy_skitter_runner",
        "motion": {"rot": 3.3, "tx": 9.5, "ty": 4.2, "scale": 0.013, "bright": 0.05},
        "grade": {"sat": 1.18, "contrast": 1.12, "bright": 1.00, "tint": (1.18, 0.88, 0.74)},
        "motif": "burrow",
    },
    "enemy_ember_leech": {
        "frames": 6,
        "source": "enemy_plated_spore",
        "motion": {"rot": 2.2, "tx": 5.8, "ty": 2.8, "scale": 0.011, "bright": 0.07},
        "grade": {"sat": 1.20, "contrast": 1.10, "bright": 1.02, "tint": (1.20, 0.76, 0.74)},
        "motif": "leech",
    },
    "enemy_spore_carrier": {
        "frames": 6,
        "source": "enemy_plated_spore",
        "motion": {"rot": 2.0, "tx": 5.4, "ty": 2.4, "scale": 0.010, "bright": 0.06},
        "grade": {"sat": 1.14, "contrast": 1.08, "bright": 1.03, "tint": (0.92, 1.18, 0.80)},
        "motif": "carrier",
    },
    "enemy_rail_warden": {
        "frames": 6,
        "source": "enemy_carapace_brute",
        "motion": {"rot": 1.7, "tx": 4.8, "ty": 2.6, "scale": 0.010, "bright": 0.05},
        "grade": {"sat": 1.02, "contrast": 1.18, "bright": 0.98, "tint": (0.84, 0.94, 1.08)},
        "motif": "warden",
    },
    "enemy_cinder_glider": {
        "frames": 8,
        "source": "enemy_skitter_runner",
        "motion": {"rot": 3.6, "tx": 10.0, "ty": 4.4, "scale": 0.013, "bright": 0.06},
        "grade": {"sat": 1.22, "contrast": 1.10, "bright": 1.03, "tint": (1.24, 0.98, 0.72)},
        "motif": "glider",
    },
    "enemy_husk_titan": {
        "frames": 6,
        "source": "enemy_carapace_brute",
        "motion": {"rot": 1.4, "tx": 3.8, "ty": 2.2, "scale": 0.008, "bright": 0.03},
        "grade": {"sat": 0.90, "contrast": 1.22, "bright": 0.94, "tint": (0.86, 0.82, 0.78)},
        "motif": "titan",
    },
    "enemy_echo_mimic": {
        "frames": 8,
        "source": "enemy_ash_swarm",
        "motion": {"rot": 4.6, "tx": 6.6, "ty": 5.3, "scale": 0.016, "bright": 0.09},
        "grade": {"sat": 1.15, "contrast": 1.14, "bright": 1.01, "tint": (0.86, 0.82, 1.26)},
        "motif": "mimic",
    },
    "enemy_furnace_matriarch": {
        "frames": 6,
        "source": "enemy_carapace_brute",
        "motion": {"rot": 1.2, "tx": 3.4, "ty": 2.0, "scale": 0.008, "bright": 0.04},
        "grade": {"sat": 1.16, "contrast": 1.22, "bright": 0.98, "tint": (1.18, 0.78, 0.72)},
        "motif": "matriarch",
    },
}

TOWER_IDS = [
    "tower_rail_lancer",
    "tower_cinder_mortar",
    "tower_frost_coil",
    "tower_arc_welder",
    "tower_siege_drill",
    "tower_ember_flak",
    "tower_resonance_beacon",
    "tower_grav_snare",
]


def resolve_existing_master(unit_id: str) -> Path:
    if unit_id in BASE_MASTERS:
        filename = BASE_MASTERS[unit_id]
        for source_dir in SOURCE_DIRS:
            path = source_dir / filename
            if path.exists():
                return path

    for source_dir in SOURCE_DIRS:
        path = source_dir / f"{unit_id}_master.png"
        if path.exists():
            return path

    raise FileNotFoundError(f"missing master source for {unit_id}")


def load_rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def apply_color_grade(image: Image.Image, sat: float, contrast: float, bright: float, tint: tuple[float, float, float]) -> Image.Image:
    graded = ImageEnhance.Color(image).enhance(sat)
    graded = ImageEnhance.Contrast(graded).enhance(contrast)
    graded = ImageEnhance.Brightness(graded).enhance(bright)

    arr = np.array(graded).astype(np.float32)
    arr[..., 0] *= tint[0]
    arr[..., 1] *= tint[1]
    arr[..., 2] *= tint[2]
    arr = np.clip(arr, 0, 255).astype(np.uint8)
    return Image.fromarray(arr, "RGBA")


def stamp_on_canvas(image: Image.Image, scale: float = 1.0, rotate: float = 0.0, offset: tuple[int, int] = (0, 0)) -> Image.Image:
    w, h = image.size
    sw = max(8, int(round(w * scale)))
    sh = max(8, int(round(h * scale)))
    resized = image.resize((sw, sh), Image.Resampling.LANCZOS)
    if abs(rotate) > 0.001:
        resized = resized.rotate(rotate, resample=Image.Resampling.BICUBIC, expand=True)

    canvas = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    px = int((1024 - resized.width) * 0.5 + offset[0])
    py = int((1024 - resized.height) * 0.5 + offset[1])
    canvas.alpha_composite(resized, (px, py))
    return canvas


def draw_motif(image: Image.Image, motif: str) -> Image.Image:
    draw = ImageDraw.Draw(image, "RGBA")
    c = 512

    if motif == "arc":
        for i in range(4):
            r = 180 + (i * 30)
            draw.arc((c - r, c - r, c + r, c + r), 208, 330, fill=(122, 230, 255, 156), width=6)
            draw.arc((c - r, c - r, c + r, c + r), 26, 142, fill=(106, 214, 252, 132), width=5)
    elif motif == "drill":
        draw.polygon(((512, 290), (622, 502), (512, 734), (402, 502)), fill=(208, 176, 132, 70), outline=(234, 206, 160, 146))
        for i in range(6):
            y = 350 + (i * 58)
            draw.line((470, y, 554, y + 24), fill=(222, 194, 152, 110), width=3)
    elif motif == "flak":
        for i in range(5):
            rr = 138 + (i * 34)
            a = max(24, 120 - (i * 18))
            draw.ellipse((c - rr, c - rr, c + rr, c + rr), outline=(255, 170, 106, a), width=4)
        draw.ellipse((c - 46, c - 46, c + 46, c + 46), fill=(255, 214, 164, 76))
    elif motif == "beacon":
        for i in range(6):
            rr = 108 + (i * 44)
            draw.ellipse((c - rr, c - rr, c + rr, c + rr), outline=(152, 236, 172, max(18, 128 - i * 18)), width=4)
    elif motif == "grav":
        for i in range(5):
            rr = 118 + (i * 40)
            draw.ellipse((c - rr, c - rr, c + rr, c + rr), outline=(148, 162, 255, max(18, 116 - i * 16)), width=4)
        for ang in (0, 90, 180, 270):
            rad = math.radians(ang)
            x = c + math.cos(rad) * 260
            y = c + math.sin(rad) * 260
            draw.ellipse((x - 22, y - 22, x + 22, y + 22), fill=(162, 182, 255, 80))
    elif motif == "burrow":
        for i in range(4):
            draw.line((330 + (i * 80), 760, 360 + (i * 80), 866), fill=(144, 82, 52, 140), width=5)
        draw.arc((290, 300, 740, 740), 220, 324, fill=(238, 174, 128, 110), width=6)
    elif motif == "leech":
        draw.ellipse((406, 410, 618, 622), fill=(255, 132, 118, 56))
        for i in range(5):
            rr = 90 + (i * 34)
            draw.ellipse((c - rr, c - rr, c + rr, c + rr), outline=(255, 118, 102, max(18, 96 - i * 14)), width=3)
    elif motif == "carrier":
        pods = ((380, 390), (648, 410), (380, 638), (654, 640))
        for px, py in pods:
            draw.ellipse((px - 50, py - 50, px + 50, py + 50), fill=(170, 236, 132, 58), outline=(210, 250, 178, 116), width=3)
    elif motif == "warden":
        for i in range(4):
            rr = 156 + (i * 30)
            draw.ellipse((c - rr, c - rr, c + rr, c + rr), outline=(160, 196, 255, max(24, 120 - i * 22)), width=5)
        draw.line((332, 512, 692, 512), fill=(192, 224, 255, 110), width=4)
    elif motif == "glider":
        draw.polygon(((512, 312), (762, 512), (512, 708), (262, 512)), fill=(255, 174, 106, 44), outline=(255, 208, 138, 136))
        draw.line((282, 512, 742, 512), fill=(242, 196, 132, 106), width=4)
    elif motif == "titan":
        for i in range(6):
            rr = 180 + (i * 22)
            draw.arc((c - rr, c - rr, c + rr, c + rr), 180, 358, fill=(202, 176, 152, max(18, 108 - i * 14)), width=5)
    elif motif == "mimic":
        for i in range(5):
            rr = 126 + (i * 40)
            draw.ellipse((c - rr, c - rr, c + rr, c + rr), outline=(172, 142, 242, max(18, 116 - i * 16)), width=4)
            draw.ellipse((c - rr + 20, c - rr + 20, c + rr - 20, c + rr - 20), outline=(146, 210, 240, max(12, 92 - i * 14)), width=2)
    elif motif == "matriarch":
        draw.ellipse((388, 388, 636, 636), fill=(255, 126, 102, 72))
        crown = ((512, 264), (602, 378), (720, 448), (640, 542), (708, 688), (512, 772), (316, 688), (384, 542), (304, 448), (422, 378))
        draw.polygon(crown, outline=(255, 194, 140, 160), fill=(255, 148, 114, 28))

    return image.filter(ImageFilter.GaussianBlur(0.35))


def build_master(unit_id: str, cache: dict[str, Image.Image]) -> Image.Image:
    live_master_path = LIVE_MASTER_DIR / f"{unit_id}_master.png"
    if live_master_path.exists():
        return load_rgba(live_master_path)

    spec = UNIT_SPECS[unit_id]
    if spec.get("existing"):
        path = resolve_existing_master(unit_id)
        return load_rgba(path)

    source_id = spec["source"]
    source_master = cache[source_id]
    graded = apply_color_grade(
        source_master,
        sat=spec["grade"]["sat"],
        contrast=spec["grade"]["contrast"],
        bright=spec["grade"]["bright"],
        tint=spec["grade"]["tint"],
    )
    stamped = stamp_on_canvas(graded, scale=1.0, rotate=0.0)
    with_motif = draw_motif(stamped, spec["motif"])
    return with_motif


def render_anim_frame(base: Image.Image, frame_index: int, frame_count: int, motion: dict, tier_boost: bool = False) -> Image.Image:
    phase = (frame_index / max(1, frame_count)) * math.tau

    scale = 1.0 + (motion["scale"] * math.sin(phase))
    rot = motion["rot"] * math.sin(phase + 0.35)
    tx = motion["tx"] * math.sin(phase)
    ty = motion["ty"] * math.cos((2.0 * phase) + 0.2)
    bright = 1.0 + (motion["bright"] * math.sin(phase + 1.1))

    if tier_boost:
        scale += 0.018
        bright += 0.06

    w, h = base.size
    sw = max(8, int(w * scale))
    sh = max(8, int(h * scale))
    scaled = base.resize((sw, sh), Image.Resampling.LANCZOS)
    rotated = scaled.rotate(rot, resample=Image.Resampling.BICUBIC, expand=True)
    rotated = ImageEnhance.Brightness(rotated).enhance(bright)

    canvas = Image.new("RGBA", base.size, (0, 0, 0, 0))
    px = int((w - rotated.width) * 0.5 + tx)
    py = int((h - rotated.height) * 0.5 + ty)
    canvas.alpha_composite(rotated, (px, py))
    return canvas


def add_tier3_pass(frame: Image.Image, tower_id: str, frame_index: int, frame_count: int) -> Image.Image:
    phase = (frame_index / max(1, frame_count)) * math.tau
    pulse = 0.5 + (0.5 * math.sin(phase))

    accent_color = {
        "tower_rail_lancer": (144, 226, 255),
        "tower_cinder_mortar": (255, 178, 120),
        "tower_frost_coil": (164, 238, 255),
        "tower_arc_welder": (132, 242, 255),
        "tower_siege_drill": (240, 198, 146),
        "tower_ember_flak": (255, 188, 128),
        "tower_resonance_beacon": (176, 246, 180),
        "tower_grav_snare": (170, 184, 255),
    }.get(tower_id, (200, 220, 255))

    upgraded = frame.copy()
    draw = ImageDraw.Draw(upgraded, "RGBA")
    c = 512

    for i in range(3):
        rr = 176 + (i * 34) + int(pulse * 12)
        alpha = max(24, 134 - (i * 28))
        draw.ellipse(
            (c - rr, c - rr, c + rr, c + rr),
            outline=(accent_color[0], accent_color[1], accent_color[2], alpha),
            width=4,
        )

    boosted = ImageEnhance.Contrast(upgraded).enhance(1.06)
    boosted = ImageEnhance.Brightness(boosted).enhance(1.05)
    glow = boosted.filter(ImageFilter.GaussianBlur(3.0))
    glow = ImageEnhance.Brightness(glow).enhance(1.08)
    composite = ImageChops.screen(boosted, glow)
    return composite


def ensure_frame(path: Path, image: Image.Image, force: bool = False) -> bool:
    if path.exists() and not force:
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)
    return True


def make_contact_sheet(masters: dict[str, Image.Image]) -> None:
    cols = 5
    tile_w = 290
    tile_h = 300
    keys = sorted(masters.keys())
    rows = math.ceil(len(keys) / cols)
    sheet = Image.new("RGBA", (cols * tile_w, rows * tile_h), (10, 14, 20, 255))

    for idx, key in enumerate(keys):
        thumb = masters[key].resize((248, 248), Image.Resampling.LANCZOS)
        card = Image.new("RGBA", (tile_w, tile_h), (18, 24, 30, 255))
        card.alpha_composite(thumb, (21, 12))
        draw = ImageDraw.Draw(card, "RGBA")
        draw.rectangle((0, 264, tile_w, tile_h), fill=(15, 19, 24, 255))
        draw.text((12, 272), key, fill=(208, 220, 232, 255))

        x = (idx % cols) * tile_w
        y = (idx // cols) * tile_h
        sheet.alpha_composite(card, (x, y))

    CONTACT_SHEET_PATH.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(CONTACT_SHEET_PATH)


def main() -> None:
    ANIM_DIR.mkdir(parents=True, exist_ok=True)
    MASTER_OUT_DIR.mkdir(parents=True, exist_ok=True)

    masters: dict[str, Image.Image] = {}

    # Build masters in dependency order.
    for unit_id in UNIT_SPECS.keys():
        master = build_master(unit_id, masters)
        masters[unit_id] = master
        master_path = MASTER_OUT_DIR / f"{unit_id}_master.png"
        master.save(master_path)

    generated = 0
    skipped = 0

    for unit_id, spec in UNIT_SPECS.items():
        frame_count = int(spec["frames"])
        motion = spec["motion"]
        base = masters[unit_id]
        is_tower = unit_id.startswith("tower_")
        existing = bool(spec.get("existing", False))
        should_generate_base = not existing

        for i in range(frame_count):
            frame = render_anim_frame(base, i, frame_count, motion)
            if should_generate_base:
                out = ANIM_DIR / f"{unit_id}_{i:02d}.png"
                if ensure_frame(out, frame):
                    generated += 1
                else:
                    skipped += 1

            if is_tower:
                t3_frame = add_tier3_pass(frame, unit_id, i, frame_count)
                t3_out = ANIM_DIR / f"{unit_id}_t3_{i:02d}.png"
                if ensure_frame(t3_out, t3_frame, force=True):
                    generated += 1
                else:
                    skipped += 1

    make_contact_sheet(masters)

    print(f"masters={len(masters)} saved to {MASTER_OUT_DIR}")
    print(f"frames generated={generated} skipped={skipped} in {ANIM_DIR}")
    print(f"contact sheet: {CONTACT_SHEET_PATH}")


if __name__ == "__main__":
    main()
