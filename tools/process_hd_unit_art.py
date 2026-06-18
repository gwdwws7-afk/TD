#!/usr/bin/env python3
from __future__ import annotations

import math
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageEnhance, ImageFilter


ROOT = Path(r"C:\test\TD")
SOURCE_DIRS = [
    ROOT / "output" / "imagegen" / "units_v4_cut",
    ROOT / "output" / "imagegen" / "units_v4_raw",
    ROOT / "output" / "imagegen" / "units_v3_raw",
]
CUTOUT_DIR = ROOT / "output" / "imagegen" / "units_v4_cutout"
ANIM_DIR = ROOT / "Assets" / "Resources" / "Art" / "anim"


MASTER_SOURCES = {
    "tower_rail_lancer_master.png": [
        "tower_rail_lancer_master.png",
        "001-prompt-top-down-2d-game-sprite-railway-coil-spear-turret-for.png",
    ],
    "tower_cinder_mortar_master.png": [
        "tower_cinder_mortar_master.png",
        "002-top-down-2d-game-sprite-ember-mortar-turret-built-from-steel.png",
    ],
    "tower_frost_coil_master.png": [
        "tower_frost_coil_master.png",
        "003-top-down-2d-game-sprite-frost-coil-turret-with-concentric-ri.png",
    ],
    "enemy_skitter_runner_master.png": [
        "enemy_skitter_runner_master.png",
        "004b-skitter-clean.png",
        "004-top-down-2d-enemy-sprite-skitter-runner-creature-for-rail-ap.png",
    ],
    "enemy_carapace_brute_master.png": [
        "enemy_carapace_brute_master.png",
        "005-top-down-2d-enemy-sprite-carapace-brute-beetle-like-heavy-un.png",
    ],
    "enemy_ash_swarm_master.png": [
        "enemy_ash_swarm_master.png",
        "006-top-down-2d-enemy-sprite-ash-swarm-orb-like-unit-with-drifti.png",
    ],
    "enemy_plated_spore_master.png": [
        "enemy_plated_spore_master.png",
        "007-top-down-2d-enemy-sprite-plated-spore-biological-pod-enemy-w.png",
    ],
}


ANIM_SPECS = {
    "tower_rail_lancer": {"count": 6, "rot": 1.8, "tx": 5, "ty": 2, "scale": 0.018, "bright": 0.06},
    "tower_cinder_mortar": {"count": 6, "rot": 1.4, "tx": 4, "ty": 2, "scale": 0.014, "bright": 0.09},
    "tower_frost_coil": {"count": 6, "rot": 1.6, "tx": 4, "ty": 2, "scale": 0.017, "bright": 0.07},
    "enemy_skitter_runner": {"count": 8, "rot": 3.0, "tx": 9, "ty": 4, "scale": 0.012, "bright": 0.05},
    "enemy_carapace_brute": {"count": 6, "rot": 2.1, "tx": 5, "ty": 3, "scale": 0.010, "bright": 0.04},
    "enemy_ash_swarm": {"count": 8, "rot": 4.2, "tx": 6, "ty": 5, "scale": 0.016, "bright": 0.08},
    "enemy_plated_spore": {"count": 6, "rot": 2.4, "tx": 6, "ty": 3, "scale": 0.012, "bright": 0.06},
}


def extract_foreground_rgba(image_path: Path) -> Image.Image:
    bgr = cv2.imread(str(image_path), cv2.IMREAD_COLOR)
    if bgr is None:
        raise FileNotFoundError(image_path)

    h, w = bgr.shape[:2]
    mask = np.full((h, w), cv2.GC_PR_BGD, np.uint8)

    border = int(min(w, h) * 0.10)
    mask[:border, :] = cv2.GC_BGD
    mask[-border:, :] = cv2.GC_BGD
    mask[:, :border] = cv2.GC_BGD
    mask[:, -border:] = cv2.GC_BGD

    c = (w // 2, h // 2)
    cv2.circle(mask, c, int(min(w, h) * 0.34), cv2.GC_PR_FGD, -1)
    cv2.circle(mask, c, int(min(w, h) * 0.22), cv2.GC_FGD, -1)

    bgd_model = np.zeros((1, 65), np.float64)
    fgd_model = np.zeros((1, 65), np.float64)
    cv2.grabCut(bgr, mask, None, bgd_model, fgd_model, 6, cv2.GC_INIT_WITH_MASK)

    alpha = np.where((mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD), 255, 0).astype(np.uint8)
    n, labels, stats, _ = cv2.connectedComponentsWithStats((alpha > 0).astype(np.uint8), 8)
    if n > 1:
        areas = stats[1:, cv2.CC_STAT_AREA]
        keep = int(1 + np.argmax(areas))
        alpha = np.where(labels == keep, 255, 0).astype(np.uint8)

    alpha = cv2.GaussianBlur(alpha, (0, 0), 1.2)
    rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    rgba = np.dstack([rgb, alpha])
    return Image.fromarray(rgba, "RGBA")


def has_meaningful_alpha(image: Image.Image) -> bool:
    rgba = image.convert("RGBA")
    arr = np.array(rgba)
    alpha = arr[:, :, 3]
    if alpha.max() == 0:
        return False

    h, w = alpha.shape
    corners = [alpha[0, 0], alpha[0, w - 1], alpha[h - 1, 0], alpha[h - 1, w - 1]]
    transparent_corners = sum(1 for c in corners if c == 0) >= 3
    occupied_ratio = float((alpha > 0).sum()) / float(alpha.size)
    return transparent_corners and occupied_ratio < 0.92


def prepare_master_rgba(source_path: Path) -> Image.Image:
    base = Image.open(source_path).convert("RGBA")
    if has_meaningful_alpha(base):
        return base
    return extract_foreground_rgba(source_path)


def resolve_source_for_master(master_name: str) -> Path:
    candidates = MASTER_SOURCES[master_name]
    for source_dir in SOURCE_DIRS:
        for candidate in candidates:
            path = source_dir / candidate
            if path.exists():
                return path
    raise FileNotFoundError(f"No source found for {master_name}")


def render_anim_frame(base: Image.Image, frame_index: int, frame_count: int, spec: dict) -> Image.Image:
    phase = (frame_index / max(1, frame_count)) * math.tau

    scale = 1.0 + (spec["scale"] * math.sin(phase))
    rot = spec["rot"] * math.sin(phase + 0.35)
    tx = spec["tx"] * math.sin(phase)
    ty = spec["ty"] * math.cos((2.0 * phase) + 0.2)
    bright = 1.0 + (spec["bright"] * math.sin(phase + 1.1))

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


def make_contact_sheet(paths: list[Path], out_path: Path, cols: int = 4) -> None:
    thumbs = []
    for p in paths:
        img = Image.open(p).convert("RGBA").resize((256, 256), Image.Resampling.LANCZOS)
        bg = Image.new("RGBA", (288, 300), (18, 22, 28, 255))
        bg.alpha_composite(img, (16, 12))
        thumbs.append((p.stem, bg))

    rows = math.ceil(len(thumbs) / cols)
    sheet = Image.new("RGBA", (cols * 288, rows * 300), (10, 14, 18, 255))

    for i, (_, thumb) in enumerate(thumbs):
        x = (i % cols) * 288
        y = (i // cols) * 300
        sheet.alpha_composite(thumb, (x, y))

    sheet.filter(ImageFilter.SMOOTH_MORE).save(out_path)


def update_meta_rect_to_full(meta_path: Path, full_w: int = 1024, full_h: int = 1024) -> None:
    if not meta_path.exists():
        return

    lines = meta_path.read_text(encoding="utf-8", errors="ignore").splitlines()
    out = []
    for i, line in enumerate(lines):
        stripped = line.strip()
        if stripped.startswith("x:") and i > 0 and lines[i - 1].strip() == "serializedVersion: 2" and i + 3 < len(lines):
            out.append(line[: line.index("x:")] + f"x: 0")
            continue
        if stripped.startswith("y:") and i > 1 and lines[i - 2].strip() == "serializedVersion: 2":
            out.append(line[: line.index("y:")] + f"y: 0")
            continue
        if stripped.startswith("width:") and i > 2 and lines[i - 3].strip() == "serializedVersion: 2":
            out.append(line[: line.index("width:")] + f"width: {full_w}")
            continue
        if stripped.startswith("height:") and i > 3 and lines[i - 4].strip() == "serializedVersion: 2":
            out.append(line[: line.index("height:")] + f"height: {full_h}")
            continue
        out.append(line)

    meta_path.write_text("\n".join(out) + "\n", encoding="utf-8")


def main() -> None:
    CUTOUT_DIR.mkdir(parents=True, exist_ok=True)
    ANIM_DIR.mkdir(parents=True, exist_ok=True)

    masters = {}
    for master_name in MASTER_SOURCES:
        raw_path = resolve_source_for_master(master_name)
        master_path = CUTOUT_DIR / master_name
        rgba = prepare_master_rgba(raw_path)
        rgba.save(master_path)
        masters[master_name] = master_path
        print(f"master: {master_name} <= {raw_path}")

    generated_frames = []
    for unit_key, spec in ANIM_SPECS.items():
        master_name = f"{unit_key}_master.png"
        base = Image.open(masters[master_name]).convert("RGBA")
        frame_count = spec["count"]
        for i in range(frame_count):
            frame = render_anim_frame(base, i, frame_count, spec)
            frame_path = ANIM_DIR / f"{unit_key}_{i:02d}.png"
            frame.save(frame_path)
            update_meta_rect_to_full(frame_path.with_suffix(".png.meta"))
            generated_frames.append(frame_path)

    contact_sheet = CUTOUT_DIR / "units_v4_contact_sheet.png"
    make_contact_sheet([masters[k] for k in sorted(masters.keys())], contact_sheet, cols=4)

    print(f"cutouts: {len(masters)} -> {CUTOUT_DIR}")
    print(f"frames: {len(generated_frames)} -> {ANIM_DIR}")
    print(f"sheet: {contact_sheet}")


if __name__ == "__main__":
    main()
