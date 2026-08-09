#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path
from typing import Callable

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets" / "Resources" / "Art"
DST = ART / "UI" / "P11"
PREVIEW = ROOT / "output" / "p11" / "p11_hud_icon_pack_preview.png"
SIZE = 128
SCALE = 4

INK = (226, 238, 240, 255)
DARK = (12, 18, 21, 255)
PANEL = (25, 34, 38, 255)
MUTED = (91, 111, 118, 255)


TOWERS = [
    ("rail_lancer", "tower_rail_lancer_00.png", (64, 137, 218), "pierce"),
    ("cinder_mortar", "tower_cinder_mortar_00.png", (212, 104, 48), "blast"),
    ("frost_coil", "tower_frost_coil_00.png", (79, 194, 232), "slow"),
    ("arc_welder", "tower_arc_welder_00.png", (55, 205, 193), "chain"),
    ("siege_drill", "tower_siege_drill_00.png", (214, 166, 56), "break"),
    ("ember_flak", "tower_ember_flak_00.png", (238, 105, 52), "swarm"),
    ("resonance_beacon", "tower_resonance_beacon_00.png", (105, 207, 116), "support"),
    ("grav_snare", "tower_grav_snare_00.png", (116, 132, 226), "control"),
]


def rgba(color: tuple[int, int, int], alpha: int = 255) -> tuple[int, int, int, int]:
    return color[0], color[1], color[2], alpha


def scaled_canvas() -> Image.Image:
    return Image.new("RGBA", (SIZE * SCALE, SIZE * SCALE), (0, 0, 0, 0))


def finish(image: Image.Image) -> Image.Image:
    return image.resize((SIZE, SIZE), Image.Resampling.LANCZOS)


def draw_badge_base(draw: ImageDraw.ImageDraw, accent: tuple[int, int, int]) -> None:
    s = SCALE
    draw.rounded_rectangle((9 * s, 9 * s, 119 * s, 119 * s), radius=22 * s, fill=(7, 11, 13, 236))
    draw.rounded_rectangle(
        (13 * s, 13 * s, 115 * s, 115 * s),
        radius=18 * s,
        fill=PANEL,
        outline=rgba(accent),
        width=5 * s,
    )
    draw.line((24 * s, 104 * s, 104 * s, 104 * s), fill=rgba(accent, 120), width=2 * s)


def draw_wave(draw: ImageDraw.ImageDraw, accent: tuple[int, int, int]) -> None:
    s = SCALE
    for radius, alpha in ((38, 80), (29, 130), (20, 210)):
        draw.arc((64 * s - radius * s, 64 * s - radius * s, 64 * s + radius * s, 64 * s + radius * s), 205, 335, fill=rgba(accent, alpha), width=5 * s)
    draw.polygon([(85 * s, 45 * s), (104 * s, 64 * s), (85 * s, 83 * s)], fill=INK)
    draw.ellipse((48 * s, 58 * s, 60 * s, 70 * s), fill=rgba(accent))


def draw_integrity(draw: ImageDraw.ImageDraw, accent: tuple[int, int, int]) -> None:
    s = SCALE
    points = [(64 * s, 29 * s), (94 * s, 41 * s), (90 * s, 76 * s), (64 * s, 99 * s), (38 * s, 76 * s), (34 * s, 41 * s)]
    draw.polygon(points, fill=rgba(accent, 72), outline=INK)
    draw.line(points + [points[0]], fill=INK, width=5 * s, joint="curve")
    draw.line((64 * s, 36 * s, 64 * s, 89 * s), fill=rgba(accent), width=4 * s)
    draw.line((43 * s, 48 * s, 64 * s, 60 * s, 85 * s, 48 * s), fill=rgba(accent), width=4 * s, joint="curve")


def draw_budget(draw: ImageDraw.ImageDraw, accent: tuple[int, int, int]) -> None:
    s = SCALE
    draw.ellipse((30 * s, 30 * s, 98 * s, 98 * s), fill=rgba(accent, 80), outline=INK, width=5 * s)
    draw.ellipse((40 * s, 40 * s, 88 * s, 88 * s), outline=rgba(accent), width=4 * s)
    draw.polygon([(64 * s, 43 * s), (70 * s, 57 * s), (86 * s, 59 * s), (74 * s, 69 * s), (78 * s, 85 * s), (64 * s, 76 * s), (50 * s, 85 * s), (54 * s, 69 * s), (42 * s, 59 * s), (58 * s, 57 * s)], fill=INK)


def draw_build(draw: ImageDraw.ImageDraw, accent: tuple[int, int, int]) -> None:
    s = SCALE
    draw.rounded_rectangle((55 * s, 31 * s, 73 * s, 94 * s), radius=5 * s, fill=INK)
    draw.rounded_rectangle((35 * s, 27 * s, 93 * s, 48 * s), radius=6 * s, fill=rgba(accent), outline=INK, width=4 * s)
    draw.line((38 * s, 90 * s, 90 * s, 38 * s), fill=INK, width=9 * s)
    draw.line((42 * s, 94 * s, 94 * s, 42 * s), fill=rgba(accent), width=3 * s)


def draw_damage(draw: ImageDraw.ImageDraw, accent: tuple[int, int, int]) -> None:
    s = SCALE
    draw.polygon([(73 * s, 24 * s), (96 * s, 56 * s), (78 * s, 58 * s), (86 * s, 101 * s), (41 * s, 61 * s), (59 * s, 58 * s)], fill=rgba(accent), outline=INK)
    draw.line((72 * s, 30 * s, 50 * s, 82 * s), fill=INK, width=5 * s)


def draw_utility(draw: ImageDraw.ImageDraw, accent: tuple[int, int, int]) -> None:
    s = SCALE
    nodes = [(64, 35), (39, 76), (89, 76)]
    draw.line(tuple(v * s for p in nodes for v in p) + (64 * s, 35 * s), fill=rgba(accent), width=5 * s, joint="curve")
    for x, y in nodes:
        draw.ellipse(((x - 10) * s, (y - 10) * s, (x + 10) * s, (y + 10) * s), fill=INK, outline=rgba(accent), width=4 * s)


def draw_route(draw: ImageDraw.ImageDraw, accent: tuple[int, int, int]) -> None:
    s = SCALE
    draw.line((64 * s, 96 * s, 64 * s, 65 * s), fill=INK, width=8 * s)
    draw.line((64 * s, 65 * s, 39 * s, 39 * s), fill=rgba(accent), width=8 * s)
    draw.line((64 * s, 65 * s, 91 * s, 39 * s), fill=rgba(accent), width=8 * s)
    draw.polygon([(29 * s, 37 * s), (47 * s, 27 * s), (46 * s, 47 * s)], fill=INK)
    draw.polygon([(99 * s, 37 * s), (81 * s, 27 * s), (82 * s, 47 * s)], fill=INK)


def draw_enemy(draw: ImageDraw.ImageDraw, accent: tuple[int, int, int]) -> None:
    s = SCALE
    draw.ellipse((35 * s, 31 * s, 93 * s, 85 * s), fill=rgba(accent, 70), outline=INK, width=5 * s)
    draw.polygon([(42 * s, 72 * s), (49 * s, 99 * s), (60 * s, 83 * s), (68 * s, 99 * s), (78 * s, 80 * s), (86 * s, 69 * s)], fill=INK)
    draw.ellipse((47 * s, 51 * s, 59 * s, 63 * s), fill=rgba(accent))
    draw.ellipse((69 * s, 51 * s, 81 * s, 63 * s), fill=rgba(accent))


def draw_speed(draw: ImageDraw.ImageDraw, accent: tuple[int, int, int]) -> None:
    s = SCALE
    draw.polygon([(42 * s, 34 * s), (75 * s, 64 * s), (42 * s, 94 * s)], fill=INK)
    draw.polygon([(68 * s, 34 * s), (101 * s, 64 * s), (68 * s, 94 * s)], fill=rgba(accent))


def draw_pause(draw: ImageDraw.ImageDraw, accent: tuple[int, int, int]) -> None:
    s = SCALE
    draw.rounded_rectangle((38 * s, 30 * s, 56 * s, 98 * s), radius=5 * s, fill=INK)
    draw.rounded_rectangle((72 * s, 30 * s, 90 * s, 98 * s), radius=5 * s, fill=rgba(accent))


HUD_ICONS: list[tuple[str, tuple[int, int, int], Callable[[ImageDraw.ImageDraw, tuple[int, int, int]], None]]] = [
    ("wave", (82, 196, 232), draw_wave),
    ("integrity", (151, 214, 108), draw_integrity),
    ("budget", (238, 185, 69), draw_budget),
    ("build", (225, 142, 56), draw_build),
    ("damage", (235, 91, 61), draw_damage),
    ("utility", (85, 200, 171), draw_utility),
    ("route", (230, 156, 53), draw_route),
    ("enemy", (221, 91, 78), draw_enemy),
    ("speed", (98, 178, 224), draw_speed),
    ("pause", (171, 191, 199), draw_pause),
]


def build_hud_icon(name: str, accent: tuple[int, int, int], symbol: Callable) -> Image.Image:
    image = scaled_canvas()
    draw = ImageDraw.Draw(image)
    draw_badge_base(draw, accent)
    symbol(draw, accent)
    return finish(image)


def crop_alpha(image: Image.Image, pad: int = 8) -> Image.Image:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        return image
    x0, y0, x1, y1 = bbox
    return image.crop((max(0, x0 - pad), max(0, y0 - pad), min(image.width, x1 + pad), min(image.height, y1 + pad)))


def fit(image: Image.Image, width: int, height: int) -> Image.Image:
    ratio = min(width / image.width, height / image.height)
    return image.resize((max(1, round(image.width * ratio)), max(1, round(image.height * ratio))), Image.Resampling.LANCZOS)


def draw_role_glyph(draw: ImageDraw.ImageDraw, role: str, accent: tuple[int, int, int]) -> None:
    s = SCALE
    color = INK
    if role == "pierce":
        draw.line((48 * s, 91 * s, 80 * s, 75 * s), fill=color, width=5 * s)
        draw.polygon([(80 * s, 75 * s), (72 * s, 74 * s), (78 * s, 82 * s)], fill=rgba(accent))
    elif role == "blast":
        draw.ellipse((51 * s, 76 * s, 77 * s, 102 * s), outline=color, width=4 * s)
        for angle in ((64, 71, 64, 77), (64, 101, 64, 107), (46, 89, 52, 89), (76, 89, 82, 89)):
            draw.line(tuple(v * s for v in angle), fill=rgba(accent), width=3 * s)
    elif role == "slow":
        draw.line((64 * s, 74 * s, 64 * s, 104 * s), fill=color, width=4 * s)
        draw.line((51 * s, 81 * s, 77 * s, 97 * s), fill=color, width=4 * s)
        draw.line((77 * s, 81 * s, 51 * s, 97 * s), fill=rgba(accent), width=4 * s)
    elif role == "chain":
        draw.line((46 * s, 79 * s, 58 * s, 90 * s, 70 * s, 79 * s, 82 * s, 90 * s), fill=color, width=4 * s, joint="curve")
        for x, y in ((46, 79), (58, 90), (70, 79), (82, 90)):
            draw.ellipse(((x - 3) * s, (y - 3) * s, (x + 3) * s, (y + 3) * s), fill=rgba(accent))
    elif role == "break":
        draw.polygon([(64 * s, 73 * s), (80 * s, 89 * s), (64 * s, 105 * s), (48 * s, 89 * s)], outline=color)
        draw.line((64 * s, 75 * s, 58 * s, 88 * s, 69 * s, 91 * s, 63 * s, 103 * s), fill=rgba(accent), width=4 * s)
    elif role == "swarm":
        for x, y in ((53, 82), (75, 82), (64, 99)):
            draw.ellipse(((x - 5) * s, (y - 5) * s, (x + 5) * s, (y + 5) * s), fill=color, outline=rgba(accent), width=2 * s)
    elif role == "support":
        draw.ellipse((59 * s, 84 * s, 69 * s, 94 * s), fill=color)
        draw.arc((48 * s, 73 * s, 80 * s, 105 * s), 205, 335, fill=rgba(accent), width=4 * s)
        draw.arc((42 * s, 67 * s, 86 * s, 111 * s), 205, 335, fill=color, width=3 * s)
    else:
        draw.ellipse((48 * s, 73 * s, 80 * s, 105 * s), outline=color, width=4 * s)
        draw.ellipse((56 * s, 81 * s, 72 * s, 97 * s), outline=rgba(accent), width=4 * s)
        draw.ellipse((62 * s, 87 * s, 66 * s, 91 * s), fill=color)


def build_tower_icon(source_name: str, accent: tuple[int, int, int], role: str) -> Image.Image:
    image = scaled_canvas()
    draw = ImageDraw.Draw(image)
    draw_badge_base(draw, accent)

    source = Image.open(ART / "anim" / source_name).convert("RGBA")
    source = crop_alpha(source)
    source = ImageEnhance.Contrast(source).enhance(1.06)
    source = ImageEnhance.Sharpness(source).enhance(1.20)
    source = fit(source, 94 * SCALE, 82 * SCALE)
    x = ((SIZE * SCALE) - source.width) // 2
    y = 18 * SCALE + max(0, ((78 * SCALE) - source.height) // 2)

    shadow = Image.new("RGBA", image.size, (0, 0, 0, 0))
    alpha = source.getchannel("A").filter(ImageFilter.GaussianBlur(4 * SCALE))
    shadow_piece = Image.new("RGBA", source.size, (0, 0, 0, 190))
    shadow_piece.putalpha(alpha)
    shadow.alpha_composite(shadow_piece, (x + 2 * SCALE, y + 3 * SCALE))
    image.alpha_composite(shadow)
    image.alpha_composite(source, (x, y))

    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((22 * SCALE, 101 * SCALE, 106 * SCALE, 111 * SCALE), radius=5 * SCALE, fill=rgba(accent))
    draw.ellipse((42 * SCALE, 70 * SCALE, 86 * SCALE, 114 * SCALE), fill=(8, 13, 15, 224), outline=rgba(accent), width=3 * SCALE)
    draw_role_glyph(draw, role, accent)
    return finish(image)


def get_font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    candidates = ["arialbd.ttf" if bold else "arial.ttf", "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"]
    for candidate in candidates:
        try:
            return ImageFont.truetype(candidate, size)
        except OSError:
            pass
    return ImageFont.load_default()


def make_preview(hud: dict[str, Image.Image], towers: dict[str, Image.Image]) -> None:
    preview = Image.new("RGBA", (1180, 760), (10, 15, 18, 255))
    draw = ImageDraw.Draw(preview)
    title_font = get_font(28, True)
    label_font = get_font(16, True)
    draw.text((42, 30), "P11.1 HUD ICONS / TOWER IDENTITIES", font=title_font, fill=(232, 238, 236, 255))
    draw.text((42, 86), "COMBAT HUD", font=label_font, fill=(143, 169, 177, 255))
    for i, (name, icon) in enumerate(hud.items()):
        x = 42 + (i % 10) * 109
        y = 118
        preview.alpha_composite(icon.resize((88, 88), Image.Resampling.LANCZOS), (x, y))
        draw.text((x, y + 92), name.upper(), font=get_font(11, True), fill=(196, 207, 210, 255))

    draw.text((42, 264), "EIGHT TOWER READS: SILHOUETTE + COLOR + ROLE GLYPH", font=label_font, fill=(143, 169, 177, 255))
    for i, (name, icon) in enumerate(towers.items()):
        x = 42 + (i % 8) * 138
        y = 304
        preview.alpha_composite(icon, (x, y))
        draw.text((x, y + 136), name.replace("_", " ").upper(), font=get_font(10, True), fill=(210, 218, 218, 255))

    draw.rounded_rectangle((42, 512, 1138, 708), radius=8, fill=(22, 29, 32, 255), outline=(77, 91, 97, 255), width=2)
    draw.text((66, 536), "GAMEPLAY SCALE", font=label_font, fill=(143, 169, 177, 255))
    for i, icon in enumerate(towers.values()):
        preview.alpha_composite(icon.resize((64, 64), Image.Resampling.LANCZOS), (66 + i * 92, 580))
    for i, icon in enumerate(list(hud.values())[:6]):
        preview.alpha_composite(icon.resize((32, 32), Image.Resampling.LANCZOS), (824 + i * 48, 596))

    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    preview.save(PREVIEW)


def build() -> None:
    DST.mkdir(parents=True, exist_ok=True)
    hud: dict[str, Image.Image] = {}
    towers: dict[str, Image.Image] = {}

    for name, accent, symbol in HUD_ICONS:
        icon = build_hud_icon(name, accent, symbol)
        icon.save(DST / f"hud_{name}.png")
        hud[name] = icon

    for slug, source, accent, role in TOWERS:
        icon = build_tower_icon(source, accent, role)
        icon.save(DST / f"tower_{slug}.png")
        towers[slug] = icon

    make_preview(hud, towers)
    print(f"Exported {len(hud)} HUD icons and {len(towers)} tower identities to {DST}")
    print(f"Preview: {PREVIEW}")


if __name__ == "__main__":
    build()
