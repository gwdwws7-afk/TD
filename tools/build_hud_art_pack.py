#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageEnhance


ROOT = Path(r"C:\test\TD")
SRC = ROOT / "output" / "imagegen" / "hud_v1_cut"
DST = ROOT / "Assets" / "Resources" / "Art"
PREVIEW = ROOT / "output" / "imagegen" / "hud_v1_pack_preview.png"


def crop_alpha(image: Image.Image, pad: int = 0) -> Image.Image:
    alpha = image.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is None:
        return image.copy()

    x0, y0, x1, y1 = bbox
    x0 = max(0, x0 - pad)
    y0 = max(0, y0 - pad)
    x1 = min(image.width, x1 + pad)
    y1 = min(image.height, y1 + pad)
    return image.crop((x0, y0, x1, y1))


def fit_center(image: Image.Image, target_w: int, target_h: int, scale: float = 1.0) -> Image.Image:
    canvas = Image.new("RGBA", (target_w, target_h), (0, 0, 0, 0))

    sw = max(1, int(image.width * scale))
    sh = max(1, int(image.height * scale))
    resized = image.resize((sw, sh), Image.Resampling.LANCZOS)

    if resized.width > target_w or resized.height > target_h:
        ratio = min(target_w / max(1, resized.width), target_h / max(1, resized.height))
        rw = max(1, int(resized.width * ratio))
        rh = max(1, int(resized.height * ratio))
        resized = resized.resize((rw, rh), Image.Resampling.LANCZOS)

    px = (target_w - resized.width) // 2
    py = (target_h - resized.height) // 2
    canvas.alpha_composite(resized, (px, py))
    return canvas


def save(image: Image.Image, name: str) -> Path:
    out = DST / name
    image.save(out)
    return out


def build() -> None:
    DST.mkdir(parents=True, exist_ok=True)

    panel_raw = Image.open(SRC / "hud_panel_frame_raw.png").convert("RGBA")
    button_raw = Image.open(SRC / "hud_button_restart_raw.png").convert("RGBA")
    wave_raw = Image.open(SRC / "hud_icon_wave_raw.png").convert("RGBA")
    integrity_raw = Image.open(SRC / "hud_icon_integrity_raw.png").convert("RGBA")
    budget_raw = Image.open(SRC / "hud_icon_budget_raw.png").convert("RGBA")

    # Build main panel (wide HUD ratio close to previous OnGUI layout).
    panel_trim = crop_alpha(panel_raw, pad=10)
    panel_bg = fit_center(panel_trim, 1536, 600, scale=1.0)
    save(panel_bg, "hud_panel_bg.png")

    # Decorative title strip from top area of the panel.
    title_slice = panel_bg.crop((0, 0, panel_bg.width, 176))
    title_slice = ImageEnhance.Brightness(title_slice).enhance(1.08)
    save(title_slice, "hud_panel_titlebar.png")

    button_trim = crop_alpha(button_raw, pad=10)

    # Slim status strip to highlight runtime status text.
    status_source = fit_center(button_trim, 1536, 180, scale=1.0)
    status_slice = ImageEnhance.Brightness(status_source).enhance(0.92)
    save(status_slice, "hud_status_strip.png")

    # Restart button skin.
    button_final = fit_center(button_trim, 1024, 320, scale=1.0)
    save(button_final, "hud_button_restart.png")

    # HUD icons.
    wave_icon = fit_center(crop_alpha(wave_raw, pad=8), 512, 512, scale=1.0)
    integrity_icon = fit_center(crop_alpha(integrity_raw, pad=8), 512, 512, scale=1.0)
    budget_icon = fit_center(crop_alpha(budget_raw, pad=8), 512, 512, scale=1.0)
    save(wave_icon, "hud_icon_wave.png")
    save(integrity_icon, "hud_icon_integrity.png")
    save(budget_icon, "hud_icon_budget.png")

    # Quick visual preview board.
    preview = Image.new("RGBA", (1920, 1080), (16, 20, 26, 255))
    preview.alpha_composite(panel_bg.resize((920, 360), Image.Resampling.LANCZOS), (36, 36))
    preview.alpha_composite(title_slice.resize((920, 96), Image.Resampling.LANCZOS), (36, 36))
    preview.alpha_composite(status_slice.resize((760, 82), Image.Resampling.LANCZOS), (98, 286))
    preview.alpha_composite(button_final.resize((360, 112), Image.Resampling.LANCZOS), (1200, 780))
    preview.alpha_composite(wave_icon.resize((96, 96), Image.Resampling.LANCZOS), (92, 140))
    preview.alpha_composite(integrity_icon.resize((96, 96), Image.Resampling.LANCZOS), (362, 140))
    preview.alpha_composite(budget_icon.resize((96, 96), Image.Resampling.LANCZOS), (632, 140))
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    preview.save(PREVIEW)

    print("HUD pack exported:")
    for name in [
        "hud_panel_bg.png",
        "hud_panel_titlebar.png",
        "hud_status_strip.png",
        "hud_button_restart.png",
        "hud_icon_wave.png",
        "hud_icon_integrity.png",
        "hud_icon_budget.png",
    ]:
        print(DST / name)
    print(f"Preview: {PREVIEW}")


if __name__ == "__main__":
    build()
