from __future__ import annotations

import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ANIM_DIR = ROOT / "Assets" / "Resources" / "Art" / "anim"
LIVE_MASTER_DIR = ROOT / "output" / "imagegen" / "batch14_enemy_mechanic_fx_live"
MASTER_OUT_DIR = ROOT / "output" / "imagegen" / "batch14_enemy_mechanic_fx_masters"
CONTACT_SHEET_PATH = ROOT / "output" / "imagegen" / "batch14_enemy_mechanic_fx_contact_sheet.png"

MASTER_SEARCH_DIRS = [
    LIVE_MASTER_DIR,
    ROOT / "output" / "imagegen" / "batch14_enemy_mechanic_fx_cut",
    ROOT / "output" / "imagegen" / "batch14_enemy_mechanic_fx_raw",
]

FX_SPECS = {
    "fx_attrition_siphon": {
        "master": "fx_attrition_siphon_master.png",
        "frames": 8,
        "target_fill": 0.74,
    },
    "fx_support_link": {
        "master": "fx_support_link_master.png",
        "frames": 8,
        "target_fill": 0.74,
    },
    "fx_elite_pressure": {
        "master": "fx_elite_pressure_master.png",
        "frames": 10,
        "target_fill": 0.76,
    },
}


def load_rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def resolve_master_path(filename: str) -> Path:
    for source_dir in MASTER_SEARCH_DIRS:
        candidate = source_dir / filename
        if candidate.exists():
            return candidate
    raise FileNotFoundError(f"missing master: {filename}")


def center_master(master: Image.Image, target_fill: float) -> Image.Image:
    alpha = master.split()[3]
    bbox = alpha.getbbox()
    if bbox is None:
        return Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))

    cropped = master.crop(bbox)
    max_side = max(1, cropped.width, cropped.height)
    scale = (1024 * max(0.1, min(0.95, target_fill))) / float(max_side)
    resized = cropped.resize(
        (
            max(8, int(round(cropped.width * scale))),
            max(8, int(round(cropped.height * scale))),
        ),
        Image.Resampling.LANCZOS,
    )

    canvas = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    x = (1024 - resized.width) // 2
    y = (1024 - resized.height) // 2
    canvas.alpha_composite(resized, (x, y))
    return canvas


def apply_alpha(image: Image.Image, alpha_mul: float) -> Image.Image:
    arr = np.array(image).astype(np.float32)
    arr[..., 3] *= max(0.0, alpha_mul)
    arr = np.clip(arr, 0, 255).astype(np.uint8)
    return Image.fromarray(arr, "RGBA")


def transform_layer(base: Image.Image, scale: float, rotate: float, bright: float, alpha_mul: float, blur: float = 0.0) -> Image.Image:
    w, h = base.size
    sw = max(8, int(round(w * scale)))
    sh = max(8, int(round(h * scale)))
    resized = base.resize((sw, sh), Image.Resampling.LANCZOS)
    rotated = resized.rotate(rotate, resample=Image.Resampling.BICUBIC, expand=True)
    lit = ImageEnhance.Brightness(rotated).enhance(max(0.01, bright))
    if blur > 0.01:
        lit = lit.filter(ImageFilter.GaussianBlur(blur))
    lit = apply_alpha(lit, alpha_mul)

    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = (w - lit.width) // 2
    py = (h - lit.height) // 2
    canvas.alpha_composite(lit, (px, py))
    return canvas


def render_attrition_frame(base: Image.Image, frame_index: int, frame_count: int) -> Image.Image:
    phase = (frame_index / max(1, frame_count)) * math.tau
    t = frame_index / max(1, frame_count - 1)
    pulse = 0.5 + (0.5 * math.sin(phase * 1.5))

    core = transform_layer(base, 0.84 + (pulse * 0.14), math.sin(phase) * 8.0, 1.08 + (pulse * 0.12), 0.92 - (t * 0.18), blur=0.24)
    siphon = transform_layer(base, 1.08 + (pulse * 0.18), -math.sin(phase * 1.2) * 6.2, 0.94, 0.56 - (t * 0.16), blur=4.8)
    frame = ImageChops.screen(core, siphon)

    draw = ImageDraw.Draw(frame, "RGBA")
    ring = 186 + int(pulse * 38)
    ring_a = max(0, int(146 * (0.90 - (t * 0.28))))
    draw.ellipse((512 - ring, 512 - ring, 512 + ring, 512 + ring), outline=(255, 158, 122, ring_a), width=5)
    return frame


def render_support_frame(base: Image.Image, frame_index: int, frame_count: int) -> Image.Image:
    phase = (frame_index / max(1, frame_count)) * math.tau
    t = frame_index / max(1, frame_count - 1)
    pulse = 0.5 + (0.5 * math.sin(phase * 1.45))

    core = transform_layer(base, 0.84 + (pulse * 0.16), math.sin(phase) * 6.0, 1.10 + (pulse * 0.08), 0.92 - (t * 0.20), blur=0.20)
    shield = transform_layer(base, 1.10 + (pulse * 0.18), 0.0, 0.98, 0.58 - (t * 0.20), blur=4.8)
    frame = ImageChops.screen(core, shield)

    draw = ImageDraw.Draw(frame, "RGBA")
    for i in range(3):
        rr = 176 + (i * 66) + int(pulse * 18)
        alpha = max(0, int((138 - (i * 28)) * (0.92 - (t * 0.30))))
        draw.ellipse((512 - rr, 512 - rr, 512 + rr, 512 + rr), outline=(146, 216, 255, alpha), width=4)
    return frame


def render_elite_frame(base: Image.Image, frame_index: int, frame_count: int) -> Image.Image:
    phase = (frame_index / max(1, frame_count)) * math.tau
    t = frame_index / max(1, frame_count - 1)
    pulse = 0.5 + (0.5 * math.sin(phase * 1.8))

    core = transform_layer(base, 0.90 + (pulse * 0.16), math.sin(phase) * 7.5, 1.12 + (pulse * 0.10), 0.92 - (t * 0.20), blur=0.26)
    pressure = transform_layer(base, 1.12 + (pulse * 0.24), math.sin(phase * 0.9) * 5.0, 1.00, 0.56 - (t * 0.18), blur=5.2)
    frame = ImageChops.screen(core, pressure)

    draw = ImageDraw.Draw(frame, "RGBA")
    ring = 212 + int(pulse * 40)
    ring_a = max(0, int(160 * (0.94 - (t * 0.30))))
    draw.ellipse((512 - ring, 512 - ring, 512 + ring, 512 + ring), outline=(255, 196, 122, ring_a), width=6)

    spike_a = max(0, int(138 * (0.90 - (t * 0.34))))
    for i in range(10):
        ang = math.radians((i * 36) + (pulse * 18))
        outer = 394
        inner = 306
        left = ang + math.radians(8)
        right = ang - math.radians(8)
        p0 = (512 + math.cos(ang) * outer, 512 + math.sin(ang) * outer)
        p1 = (512 + math.cos(left) * inner, 512 + math.sin(left) * inner)
        p2 = (512 + math.cos(right) * inner, 512 + math.sin(right) * inner)
        draw.polygon((p0, p1, p2), fill=(255, 168, 102, spike_a))
    return frame


def make_contact_sheet(masters: dict[str, Image.Image]) -> None:
    keys = sorted(masters.keys())
    tile_w = 330
    tile_h = 352
    cols = 3
    rows = math.ceil(len(keys) / cols)
    sheet = Image.new("RGBA", (cols * tile_w, rows * tile_h), (10, 14, 20, 255))

    for idx, key in enumerate(keys):
        thumb = masters[key].resize((286, 286), Image.Resampling.LANCZOS)
        card = Image.new("RGBA", (tile_w, tile_h), (18, 24, 32, 255))
        card.alpha_composite(thumb, (22, 14))
        draw = ImageDraw.Draw(card, "RGBA")
        draw.rectangle((0, 310, tile_w, tile_h), fill=(14, 18, 26, 255))
        draw.text((12, 320), key, fill=(210, 224, 236, 255))

        x = (idx % cols) * tile_w
        y = (idx // cols) * tile_h
        sheet.alpha_composite(card, (x, y))

    CONTACT_SHEET_PATH.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(CONTACT_SHEET_PATH)


def main() -> None:
    ANIM_DIR.mkdir(parents=True, exist_ok=True)
    MASTER_OUT_DIR.mkdir(parents=True, exist_ok=True)

    masters: dict[str, Image.Image] = {}
    for fx_id, spec in FX_SPECS.items():
        source = resolve_master_path(spec["master"])
        centered = center_master(load_rgba(source), target_fill=float(spec["target_fill"]))
        masters[fx_id] = centered
        centered.save(MASTER_OUT_DIR / spec["master"])

    generated = 0
    for fx_id, spec in FX_SPECS.items():
        base = masters[fx_id]
        frames = int(spec["frames"])
        for i in range(frames):
            if fx_id == "fx_attrition_siphon":
                frame = render_attrition_frame(base, i, frames)
            elif fx_id == "fx_support_link":
                frame = render_support_frame(base, i, frames)
            else:
                frame = render_elite_frame(base, i, frames)

            out = ANIM_DIR / f"{fx_id}_{i:02d}.png"
            frame.save(out)
            generated += 1

    make_contact_sheet(masters)
    print(f"masters={len(masters)} saved to {MASTER_OUT_DIR}")
    print(f"fx frames generated={generated} in {ANIM_DIR}")
    print(f"contact sheet: {CONTACT_SHEET_PATH}")


if __name__ == "__main__":
    main()
