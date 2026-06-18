from __future__ import annotations

import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ANIM_DIR = ROOT / "Assets" / "Resources" / "Art" / "anim"
LIVE_MASTER_DIR = ROOT / "output" / "imagegen" / "batch12_fx_live"
MASTER_OUT_DIR = ROOT / "output" / "imagegen" / "batch12_fx_masters"
CONTACT_SHEET_PATH = ROOT / "output" / "imagegen" / "batch12_fx_contact_sheet.png"

MASTER_SEARCH_DIRS = [
    LIVE_MASTER_DIR,
    ROOT / "output" / "imagegen" / "batch12_fx_cut",
    ROOT / "output" / "imagegen" / "batch12_fx_raw",
]

FX_SPECS = {
    "fx_enemy_hit": {
        "master": "fx_enemy_hit_master.png",
        "frames": 6,
        "target_fill": 0.68,
    },
    "fx_enemy_death": {
        "master": "fx_enemy_death_master.png",
        "frames": 8,
        "target_fill": 0.74,
    },
    "fx_boss_warning": {
        "master": "fx_boss_warning_master.png",
        "frames": 10,
        "target_fill": 0.78,
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


def render_hit_frame(base: Image.Image, frame_index: int, frame_count: int) -> Image.Image:
    t = frame_index / max(1, frame_count - 1)
    scale = 0.62 + (0.88 * t)
    rotate = -8 + (16 * t)
    alpha_mul = max(0.0, 1.0 - (t * 0.95))
    bright = 1.30 - (0.26 * t)

    core = transform_layer(base, scale, rotate, bright, alpha_mul, blur=0.25)
    glow = transform_layer(base, scale * 1.12, rotate * 0.45, 1.20, alpha_mul * 0.58, blur=4.4)
    frame = ImageChops.screen(core, glow)

    draw = ImageDraw.Draw(frame, "RGBA")
    ring_r = int(168 + (t * 186))
    ring_a = int(132 * (1.0 - t))
    draw.ellipse(
        (512 - ring_r, 512 - ring_r, 512 + ring_r, 512 + ring_r),
        outline=(255, 188, 132, max(0, ring_a)),
        width=6,
    )
    arc_a = int(118 * (1.0 - t))
    draw.arc((512 - ring_r - 30, 512 - ring_r - 30, 512 + ring_r + 30, 512 + ring_r + 30), 212, 334, fill=(140, 224, 255, max(0, arc_a)), width=4)
    draw.arc((512 - ring_r - 30, 512 - ring_r - 30, 512 + ring_r + 30, 512 + ring_r + 30), 32, 152, fill=(140, 224, 255, max(0, arc_a)), width=4)
    return frame


def render_death_frame(base: Image.Image, frame_index: int, frame_count: int) -> Image.Image:
    t = frame_index / max(1, frame_count - 1)
    scale = 0.78 + (1.36 * t)
    rotate = -14 + (28 * t)
    alpha_mul = max(0.0, 1.0 - (t ** 0.95))
    bright = 1.18 - (0.48 * t)

    core = transform_layer(base, scale, rotate, bright, alpha_mul, blur=0.4)
    smoke = transform_layer(base, scale * 1.22, rotate * 0.38, 0.72, alpha_mul * 0.62, blur=6.8)
    frame = ImageChops.screen(core, smoke)

    draw = ImageDraw.Draw(frame, "RGBA")
    burst_r = int(154 + (t * 244))
    burst_a = int(124 * (1.0 - t))
    draw.ellipse(
        (512 - burst_r, 512 - burst_r, 512 + burst_r, 512 + burst_r),
        outline=(255, 142, 106, max(0, burst_a)),
        width=5,
    )

    ember_alpha = int(132 * (1.0 - t))
    for i in range(8):
        ang = math.radians((i * 45) + (t * 120))
        px = 512 + math.cos(ang) * (120 + (t * 240))
        py = 512 + math.sin(ang) * (100 + (t * 200))
        rr = 18 + int((1.0 - t) * 10)
        draw.ellipse((px - rr, py - rr, px + rr, py + rr), fill=(255, 154, 118, max(0, ember_alpha)))

    return frame


def render_boss_warning_frame(base: Image.Image, frame_index: int, frame_count: int) -> Image.Image:
    phase = (frame_index / max(1, frame_count)) * math.tau
    t = frame_index / max(1, frame_count - 1)
    pulse = 0.5 + (0.5 * math.sin(phase * 1.8))

    scale = 0.92 + (pulse * 0.14)
    rotate = math.sin(phase) * 8.0
    alpha_mul = 0.90 - (t * 0.20)
    bright = 1.18 + (pulse * 0.10)

    core = transform_layer(base, scale, rotate, bright, alpha_mul, blur=0.2)
    glow = transform_layer(base, scale * 1.06, rotate * 0.4, 1.14, alpha_mul * 0.55, blur=5.2)
    frame = ImageChops.screen(core, glow)

    draw = ImageDraw.Draw(frame, "RGBA")
    ring_base = 226 + int(pulse * 36)
    for idx in range(3):
        rr = ring_base + (idx * 84)
        alpha = max(0, int((146 - (idx * 34)) * (0.94 - (t * 0.32))))
        draw.ellipse((512 - rr, 512 - rr, 512 + rr, 512 + rr), outline=(255, 188, 112, alpha), width=6 - idx)

    marker_a = max(0, int(182 * (0.90 - (t * 0.28))))
    for idx in range(8):
        ang = math.radians((idx * 45) + (t * 34))
        outer = 380 + (pulse * 18)
        inner = outer - 80
        left = ang + math.radians(10)
        right = ang - math.radians(10)
        tip_x = 512 + math.cos(ang) * outer
        tip_y = 512 + math.sin(ang) * outer
        left_x = 512 + math.cos(left) * inner
        left_y = 512 + math.sin(left) * inner
        right_x = 512 + math.cos(right) * inner
        right_y = 512 + math.sin(right) * inner
        draw.polygon(((tip_x, tip_y), (left_x, left_y), (right_x, right_y)), fill=(255, 150, 102, marker_a))

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
            if fx_id == "fx_enemy_hit":
                frame = render_hit_frame(base, i, frames)
            elif fx_id == "fx_enemy_death":
                frame = render_death_frame(base, i, frames)
            else:
                frame = render_boss_warning_frame(base, i, frames)

            out = ANIM_DIR / f"{fx_id}_{i:02d}.png"
            frame.save(out)
            generated += 1

    make_contact_sheet(masters)
    print(f"masters={len(masters)} saved to {MASTER_OUT_DIR}")
    print(f"fx frames generated={generated} in {ANIM_DIR}")
    print(f"contact sheet: {CONTACT_SHEET_PATH}")


if __name__ == "__main__":
    main()
