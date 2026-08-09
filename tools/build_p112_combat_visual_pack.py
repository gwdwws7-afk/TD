#!/usr/bin/env python3
from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[1]
DST = ROOT / "Assets" / "Resources" / "Art" / "Combat" / "P11"
PREVIEW = ROOT / "output" / "p11" / "p112_combat_visual_pack_preview.png"
SIZE = 128
SCALE = 4

INK = (232, 241, 241, 255)
DARK = (8, 13, 16, 236)

THREAT_COLORS = {
    "fast": (72, 196, 226),
    "swarm": (236, 112, 64),
    "armored": (232, 181, 66),
    "support": (96, 206, 122),
    "special": (157, 118, 226),
    "boss": (238, 72, 54),
}

STATUS_COLORS = {
    "slow": (82, 202, 235),
    "armor_break": (238, 171, 54),
    "stagger": (244, 112, 57),
    "exposed": (237, 92, 84),
    "resonance": (102, 215, 136),
}

TOWERS = [
    ("rail_lancer", (64, 137, 218), "rail"),
    ("cinder_mortar", (212, 104, 48), "mortar"),
    ("frost_coil", (79, 194, 232), "frost"),
    ("arc_welder", (55, 205, 193), "arc"),
    ("siege_drill", (214, 166, 56), "drill"),
    ("ember_flak", (238, 105, 52), "flak"),
    ("resonance_beacon", (105, 207, 116), "beacon"),
    ("grav_snare", (116, 132, 226), "grav"),
]


def rgba(color: tuple[int, int, int], alpha: int = 255) -> tuple[int, int, int, int]:
    return color[0], color[1], color[2], alpha


def canvas() -> Image.Image:
    return Image.new("RGBA", (SIZE * SCALE, SIZE * SCALE), (0, 0, 0, 0))


def finish(image: Image.Image) -> Image.Image:
    return image.resize((SIZE, SIZE), Image.Resampling.LANCZOS)


def line(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], fill, width: int, joint: str = "curve") -> None:
    draw.line([(x * SCALE, y * SCALE) for x, y in points], fill=fill, width=width * SCALE, joint=joint)


def ellipse(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], **kwargs) -> None:
    draw.ellipse(tuple(value * SCALE for value in box), **kwargs)


def polygon(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], **kwargs) -> None:
    draw.polygon([(x * SCALE, y * SCALE) for x, y in points], **kwargs)


def draw_threat_plate(draw: ImageDraw.ImageDraw, color: tuple[int, int, int]) -> None:
    s = SCALE
    polygon(draw, [(64, 8), (111, 32), (111, 92), (64, 120), (17, 92), (17, 32)], fill=DARK)
    polygon(
        draw,
        [(64, 13), (106, 35), (106, 88), (64, 114), (22, 88), (22, 35)],
        fill=(20, 28, 32, 246),
        outline=rgba(color),
    )
    draw.line((29 * s, 98 * s, 99 * s, 98 * s), fill=rgba(color, 120), width=3 * s)


def build_threat_icon(kind: str, color: tuple[int, int, int]) -> Image.Image:
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw_threat_plate(draw, color)

    if kind == "fast":
        polygon(draw, [(30, 34), (62, 64), (30, 94), (45, 94), (78, 64), (45, 34)], fill=INK)
        polygon(draw, [(63, 34), (96, 64), (63, 94), (78, 94), (111, 64), (78, 34)], fill=rgba(color))
    elif kind == "swarm":
        for x, y, radius in ((43, 48, 14), (83, 48, 14), (64, 82, 16)):
            ellipse(draw, (x - radius, y - radius, x + radius, y + radius), fill=rgba(color, 92), outline=INK, width=4 * SCALE)
        line(draw, [(49, 60), (60, 72), (77, 59)], rgba(color), 4)
    elif kind == "armored":
        polygon(draw, [(64, 27), (93, 38), (89, 76), (64, 99), (39, 76), (35, 38)], fill=rgba(color, 74), outline=INK)
        line(draw, [(64, 31), (64, 91)], rgba(color), 5)
        line(draw, [(42, 46), (64, 58), (86, 46)], INK, 5)
    elif kind == "support":
        line(draw, [(64, 38), (43, 78), (85, 78), (64, 38)], rgba(color), 5)
        for x, y in ((64, 36), (41, 80), (87, 80)):
            ellipse(draw, (x - 9, y - 9, x + 9, y + 9), fill=INK, outline=rgba(color), width=4 * SCALE)
        line(draw, [(64, 54), (64, 82)], INK, 5)
        line(draw, [(50, 68), (78, 68)], INK, 5)
    elif kind == "special":
        polygon(draw, [(64, 25), (92, 64), (64, 103), (36, 64)], fill=rgba(color, 78), outline=INK)
        polygon(draw, [(64, 40), (77, 64), (64, 88), (51, 64)], fill=INK)
        ellipse(draw, (59, 59, 69, 69), fill=rgba(color))
    else:
        polygon(draw, [(31, 48), (46, 28), (64, 49), (82, 28), (99, 48), (91, 91), (37, 91)], fill=rgba(color, 80), outline=INK)
        line(draw, [(38, 57), (91, 57)], INK, 5)
        ellipse(draw, (48, 65, 59, 76), fill=INK)
        ellipse(draw, (69, 65, 80, 76), fill=INK)
        polygon(draw, [(64, 76), (58, 87), (70, 87)], fill=rgba(color))

    return finish(image)


def draw_status_plate(draw: ImageDraw.ImageDraw, color: tuple[int, int, int]) -> None:
    ellipse(draw, (9, 9, 119, 119), fill=DARK)
    ellipse(draw, (15, 15, 113, 113), fill=(21, 29, 33, 246), outline=rgba(color), width=5 * SCALE)


def build_status_icon(kind: str, color: tuple[int, int, int]) -> Image.Image:
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw_status_plate(draw, color)

    if kind == "slow":
        for angle in (0, 60, 120):
            rad = math.radians(angle)
            dx = int(math.cos(rad) * 30)
            dy = int(math.sin(rad) * 30)
            line(draw, [(64 - dx, 64 - dy), (64 + dx, 64 + dy)], INK, 5)
        ellipse(draw, (57, 57, 71, 71), fill=rgba(color))
    elif kind == "armor_break":
        polygon(draw, [(64, 27), (92, 39), (87, 79), (64, 101), (39, 79), (35, 39)], fill=rgba(color, 62), outline=INK)
        line(draw, [(66, 29), (57, 53), (69, 59), (55, 82), (64, 99)], rgba(color), 6)
    elif kind == "stagger":
        polygon(draw, [(36, 40), (73, 40), (57, 58), (91, 58), (52, 93), (62, 68), (33, 68)], fill=INK)
        line(draw, [(80, 35), (95, 46)], rgba(color), 4)
        line(draw, [(84, 82), (98, 72)], rgba(color), 4)
    elif kind == "exposed":
        ellipse(draw, (31, 31, 97, 97), outline=INK, width=6 * SCALE)
        line(draw, [(64, 20), (64, 43)], rgba(color), 5)
        line(draw, [(64, 85), (64, 108)], rgba(color), 5)
        line(draw, [(20, 64), (43, 64)], rgba(color), 5)
        line(draw, [(85, 64), (108, 64)], rgba(color), 5)
        ellipse(draw, (53, 53, 75, 75), fill=rgba(color))
    else:
        ellipse(draw, (57, 57, 71, 71), fill=INK)
        draw.arc((39 * SCALE, 39 * SCALE, 89 * SCALE, 89 * SCALE), 205, 335, fill=rgba(color), width=6 * SCALE)
        draw.arc((25 * SCALE, 25 * SCALE, 103 * SCALE, 103 * SCALE), 205, 335, fill=INK, width=5 * SCALE)

    return finish(image)


def build_threat_pip() -> Image.Image:
    image = canvas()
    draw = ImageDraw.Draw(image)
    polygon(draw, [(64, 18), (110, 64), (64, 110), (18, 64)], fill=INK, outline=(255, 255, 255, 255))
    polygon(draw, [(64, 36), (92, 64), (64, 92), (36, 64)], fill=(255, 255, 255, 255))
    return finish(image)


def draw_projectile_shape(draw: ImageDraw.ImageDraw, kind: str, color: tuple[int, int, int]) -> None:
    if kind == "rail":
        polygon(draw, [(16, 58), (86, 54), (116, 64), (86, 74), (16, 70), (45, 64)], fill=rgba(color, 100))
        line(draw, [(20, 64), (105, 64)], INK, 4)
        polygon(draw, [(105, 54), (120, 64), (105, 74)], fill=rgba(color))
    elif kind == "mortar":
        ellipse(draw, (47, 43, 91, 87), fill=rgba(color, 130), outline=INK, width=5 * SCALE)
        line(draw, [(18, 64), (49, 64)], rgba(color), 8)
        ellipse(draw, (57, 53, 75, 71), fill=(255, 230, 170, 255))
    elif kind == "frost":
        polygon(draw, [(18, 64), (53, 50), (80, 25), (76, 54), (115, 64), (76, 74), (80, 103), (53, 78)], fill=rgba(color, 116), outline=INK)
        line(draw, [(31, 64), (105, 64)], INK, 4)
    elif kind == "arc":
        polygon(draw, [(18, 70), (48, 39), (59, 57), (82, 32), (75, 60), (111, 53), (76, 92), (65, 72), (42, 96), (50, 69)], fill=rgba(color), outline=INK)
    elif kind == "drill":
        polygon(draw, [(18, 49), (77, 49), (116, 64), (77, 79), (18, 79), (39, 64)], fill=rgba(color, 110), outline=INK)
        line(draw, [(35, 49), (56, 79), (77, 49), (94, 70)], INK, 4)
    elif kind == "flak":
        for y in (48, 64, 80):
            line(draw, [(19, y), (67, y)], rgba(color, 150), 5)
            ellipse(draw, (65, y - 9, 83, y + 9), fill=INK, outline=rgba(color), width=3 * SCALE)
        polygon(draw, [(82, 51), (114, 64), (82, 77)], fill=rgba(color, 100))
    elif kind == "beacon":
        polygon(draw, [(22, 64), (65, 30), (108, 64), (65, 98)], fill=rgba(color, 80), outline=INK)
        ellipse(draw, (54, 53, 76, 75), fill=rgba(color))
        draw.arc((38 * SCALE, 38 * SCALE, 92 * SCALE, 92 * SCALE), 200, 340, fill=INK, width=4 * SCALE)
    else:
        ellipse(draw, (31, 31, 97, 97), fill=rgba(color, 74), outline=INK, width=5 * SCALE)
        ellipse(draw, (46, 46, 82, 82), outline=rgba(color), width=6 * SCALE)
        ellipse(draw, (57, 57, 71, 71), fill=(245, 246, 255, 255))
        draw.arc((19 * SCALE, 19 * SCALE, 109 * SCALE, 109 * SCALE), 205, 338, fill=rgba(color), width=4 * SCALE)


def build_projectile(kind: str, color: tuple[int, int, int]) -> Image.Image:
    core = canvas()
    draw_projectile_shape(ImageDraw.Draw(core), kind, color)

    glow = Image.new("RGBA", core.size, (0, 0, 0, 0))
    glow_piece = Image.new("RGBA", core.size, rgba(color, 150))
    glow_piece.putalpha(core.getchannel("A").filter(ImageFilter.GaussianBlur(5 * SCALE)))
    glow.alpha_composite(glow_piece)
    glow.alpha_composite(core)
    return finish(glow)


def build_impact(kind: str, color: tuple[int, int, int]) -> Image.Image:
    image = canvas()
    draw = ImageDraw.Draw(image)

    if kind in {"rail", "drill"}:
        line(draw, [(17, 64), (111, 64)], rgba(color), 6)
        line(draw, [(64, 20), (64, 108)], INK, 5)
        for angle in (35, 145, 215, 325):
            rad = math.radians(angle)
            line(draw, [(64, 64), (64 + int(math.cos(rad) * 42), 64 + int(math.sin(rad) * 42))], rgba(color, 190), 4)
    elif kind in {"mortar", "flak"}:
        for radius, alpha, width in ((19, 255, 7), (35, 190, 5), (51, 100, 3)):
            ellipse(draw, (64 - radius, 64 - radius, 64 + radius, 64 + radius), outline=rgba(color, alpha), width=width * SCALE)
        for angle in range(0, 360, 45):
            rad = math.radians(angle)
            line(draw, [(64 + int(math.cos(rad) * 24), 64 + int(math.sin(rad) * 24)), (64 + int(math.cos(rad) * 55), 64 + int(math.sin(rad) * 55))], INK, 4)
    elif kind == "frost":
        for angle in (0, 45, 90, 135):
            rad = math.radians(angle)
            dx = int(math.cos(rad) * 45)
            dy = int(math.sin(rad) * 45)
            line(draw, [(64 - dx, 64 - dy), (64 + dx, 64 + dy)], rgba(color), 5)
        ellipse(draw, (52, 52, 76, 76), fill=INK)
    elif kind == "arc":
        for offset in (-12, 0, 12):
            line(draw, [(22, 62 + offset), (48, 43 + offset), (61, 69 + offset), (83, 42 + offset), (108, 64 + offset)], rgba(color, 220 if offset == 0 else 110), 4)
    elif kind == "beacon":
        for radius, alpha in ((18, 255), (34, 180), (50, 90)):
            polygon(draw, [(64, 64 - radius), (64 + radius, 64), (64, 64 + radius), (64 - radius, 64)], outline=rgba(color, alpha))
        ellipse(draw, (57, 57, 71, 71), fill=INK)
    else:
        for radius, alpha, width in ((16, 255, 6), (31, 190, 5), (49, 100, 4)):
            ellipse(draw, (64 - radius, 64 - radius, 64 + radius, 64 + radius), outline=rgba(color, alpha), width=width * SCALE)
        polygon(draw, [(64, 20), (73, 54), (108, 64), (73, 74), (64, 108), (55, 74), (20, 64), (55, 54)], fill=rgba(color, 55))

    glow_layer = image.copy()
    glow_layer = glow_layer.filter(ImageFilter.GaussianBlur(4 * SCALE))
    image.alpha_composite(glow_layer)
    return finish(image)


def get_font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    candidates = ["arialbd.ttf" if bold else "arial.ttf", "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"]
    for candidate in candidates:
        try:
            return ImageFont.truetype(candidate, size)
        except OSError:
            pass
    return ImageFont.load_default()


def make_preview(assets: dict[str, Image.Image]) -> None:
    preview = Image.new("RGBA", (1320, 920), (10, 15, 18, 255))
    draw = ImageDraw.Draw(preview)
    title = get_font(28, True)
    label = get_font(14, True)
    small = get_font(10, True)
    draw.text((40, 28), "P11.2 ENEMY READABILITY / COMBAT VISUAL LANGUAGE", font=title, fill=(232, 239, 239, 255))

    groups = [
        ("THREAT SHAPES", [f"threat_{name}" for name in THREAT_COLORS], 92),
        ("STATUS ICONS", [f"status_{name}" for name in STATUS_COLORS], 290),
        ("PROJECTILE IDENTITIES", [f"projectile_{name}" for name, _, _ in TOWERS], 488),
        ("IMPACT IDENTITIES", [f"impact_{name}" for name, _, _ in TOWERS], 686),
    ]
    for heading, keys, y in groups:
        draw.text((40, y), heading, font=label, fill=(137, 164, 172, 255))
        for index, key in enumerate(keys):
            x = 40 + index * 150
            preview.alpha_composite(assets[key], (x, y + 34))
            short_key = key.replace("projectile_", "").replace("impact_", "").replace("threat_", "").replace("status_", "")
            draw.text((x, y + 166), short_key.replace("_", " ").upper(), font=small, fill=(205, 215, 216, 255))

    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    preview.save(PREVIEW)


def build() -> None:
    DST.mkdir(parents=True, exist_ok=True)
    assets: dict[str, Image.Image] = {}

    for name, color in THREAT_COLORS.items():
        assets[f"threat_{name}"] = build_threat_icon(name, color)
    assets["threat_pip"] = build_threat_pip()

    for name, color in STATUS_COLORS.items():
        assets[f"status_{name}"] = build_status_icon(name, color)

    for slug, color, kind in TOWERS:
        assets[f"projectile_{slug}"] = build_projectile(kind, color)
        assets[f"impact_{slug}"] = build_impact(kind, color)

    for name, image in assets.items():
        image.save(DST / f"{name}.png")

    make_preview(assets)
    print(f"Exported {len(assets)} P11.2 combat assets to {DST}")
    print(f"Preview: {PREVIEW}")


if __name__ == "__main__":
    build()
