from __future__ import annotations

import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ANIM_DIR = ROOT / "Assets" / "Resources" / "Art" / "anim"
LIVE_MASTER_DIR = ROOT / "output" / "imagegen" / "batch13_mechanic_fx_live"
MASTER_OUT_DIR = ROOT / "output" / "imagegen" / "batch13_mechanic_fx_masters"
CONTACT_SHEET_PATH = ROOT / "output" / "imagegen" / "batch13_mechanic_fx_contact_sheet.png"

MASTER_SEARCH_DIRS = [
    LIVE_MASTER_DIR,
    ROOT / "output" / "imagegen" / "batch13_mechanic_fx_cut",
    ROOT / "output" / "imagegen" / "batch13_mechanic_fx_raw",
]

FX_SPECS = {
    "fx_burrow_ambush": {
        "master": "fx_burrow_ambush_master.png",
        "frames": 8,
        "target_fill": 0.74,
    },
    "fx_spore_split_warning": {
        "master": "fx_spore_split_warning_master.png",
        "frames": 8,
        "target_fill": 0.74,
    },
    "fx_mimic_shift": {
        "master": "fx_mimic_shift_master.png",
        "frames": 8,
        "target_fill": 0.74,
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


def render_burrow_frame(base: Image.Image, frame_index: int, frame_count: int) -> Image.Image:
    phase = (frame_index / max(1, frame_count)) * math.tau
    t = frame_index / max(1, frame_count - 1)
    pulse = 0.5 + (0.5 * math.sin(phase * 1.5))

    core = transform_layer(base, 0.86 + (pulse * 0.16), math.sin(phase) * 7.0, 1.08 + (pulse * 0.10), 0.92 - (t * 0.18), blur=0.25)
    shock = transform_layer(base, 1.06 + (pulse * 0.20), math.sin(phase * 1.2) * 4.2, 1.00, 0.56 - (t * 0.16), blur=4.6)
    frame = ImageChops.screen(core, shock)

    draw = ImageDraw.Draw(frame, "RGBA")
    ring = 188 + int(pulse * 48)
    ring_a = max(0, int(148 * (0.90 - (t * 0.30))))
    draw.ellipse((512 - ring, 512 - ring, 512 + ring, 512 + ring), outline=(255, 178, 110, ring_a), width=5)
    draw.arc((512 - ring - 36, 512 - ring - 36, 512 + ring + 36, 512 + ring + 36), 228, 334, fill=(124, 216, 255, ring_a), width=4)
    draw.arc((512 - ring - 36, 512 - ring - 36, 512 + ring + 36, 512 + ring + 36), 24, 128, fill=(124, 216, 255, ring_a), width=4)
    return frame


def render_spore_split_frame(base: Image.Image, frame_index: int, frame_count: int) -> Image.Image:
    phase = (frame_index / max(1, frame_count)) * math.tau
    t = frame_index / max(1, frame_count - 1)
    pulse = 0.5 + (0.5 * math.sin(phase * 1.35))

    core = transform_layer(base, 0.84 + (pulse * 0.18), math.sin(phase * 0.8) * 6.0, 1.06 + (pulse * 0.10), 0.92 - (t * 0.20), blur=0.22)
    haze = transform_layer(base, 1.10 + (pulse * 0.22), 0.0, 0.95, 0.58 - (t * 0.20), blur=5.0)
    frame = ImageChops.screen(core, haze)

    draw = ImageDraw.Draw(frame, "RGBA")
    ring = 178 + int(pulse * 58)
    ring_a = max(0, int(154 * (0.92 - (t * 0.30))))
    draw.ellipse((512 - ring, 512 - ring, 512 + ring, 512 + ring), outline=(168, 255, 152, ring_a), width=5)

    split_a = max(0, int(138 * (0.90 - (t * 0.36))))
    for i in range(6):
        ang = math.radians((i * 60) + (pulse * 18))
        inner = 122
        outer = 314 + (pulse * 24)
        x0 = 512 + math.cos(ang) * inner
        y0 = 512 + math.sin(ang) * inner
        x1 = 512 + math.cos(ang) * outer
        y1 = 512 + math.sin(ang) * outer
        draw.line((x0, y0, x1, y1), fill=(196, 255, 176, split_a), width=4)
    return frame


def render_mimic_shift_frame(base: Image.Image, frame_index: int, frame_count: int) -> Image.Image:
    phase = (frame_index / max(1, frame_count)) * math.tau
    t = frame_index / max(1, frame_count - 1)
    pulse = 0.5 + (0.5 * math.sin(phase * 1.7))

    core = transform_layer(base, 0.86 + (pulse * 0.18), math.sin(phase) * 9.0, 1.10 + (pulse * 0.08), 0.92 - (t * 0.20), blur=0.28)
    echo = transform_layer(base, 1.12 + (pulse * 0.16), -math.sin(phase * 1.3) * 8.0, 1.00, 0.54 - (t * 0.16), blur=4.8)
    frame = ImageChops.screen(core, echo)

    draw = ImageDraw.Draw(frame, "RGBA")
    rings = (
        (208 + int(pulse * 40), (255, 170, 110)),
        (276 + int(pulse * 30), (138, 206, 255)),
        (340 + int(pulse * 22), (160, 255, 172)),
    )
    for radius, color in rings:
        alpha = max(0, int(128 * (0.92 - (t * 0.30))))
        draw.ellipse((512 - radius, 512 - radius, 512 + radius, 512 + radius), outline=(color[0], color[1], color[2], alpha), width=4)

    tri_a = max(0, int(146 * (0.90 - (t * 0.34))))
    for i in range(3):
        base_ang = math.radians((i * 120) + (pulse * 24))
        tip = 358
        mid = 244
        left = base_ang + math.radians(12)
        right = base_ang - math.radians(12)
        p0 = (512 + math.cos(base_ang) * tip, 512 + math.sin(base_ang) * tip)
        p1 = (512 + math.cos(left) * mid, 512 + math.sin(left) * mid)
        p2 = (512 + math.cos(right) * mid, 512 + math.sin(right) * mid)
        draw.polygon((p0, p1, p2), fill=(214, 194, 255, tri_a))
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
            if fx_id == "fx_burrow_ambush":
                frame = render_burrow_frame(base, i, frames)
            elif fx_id == "fx_spore_split_warning":
                frame = render_spore_split_frame(base, i, frames)
            else:
                frame = render_mimic_shift_frame(base, i, frames)

            out = ANIM_DIR / f"{fx_id}_{i:02d}.png"
            frame.save(out)
            generated += 1

    make_contact_sheet(masters)
    print(f"masters={len(masters)} saved to {MASTER_OUT_DIR}")
    print(f"fx frames generated={generated} in {ANIM_DIR}")
    print(f"contact sheet: {CONTACT_SHEET_PATH}")


if __name__ == "__main__":
    main()
