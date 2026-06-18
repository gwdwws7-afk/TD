from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


SPRITE_SIZE = 1024
TILE_SIZE = 1024
SUPERSAMPLE = 1
SPRITE_CENTER = SPRITE_SIZE / 2.0
SPRITE_DRAW_SCALE = SPRITE_SIZE / 256.0
MAP_WIDTH = 4096
MAP_HEIGHT = 2304
MAP_BOARD_W = 16
MAP_BOARD_H = 9

ART_DIR = Path("Assets/Resources/Art")
ANIM_DIR = ART_DIR / "anim"
GRAYLINE_PATH_CELLS = [
    (0, 5), (1, 4), (2, 4), (3, 4), (4, 4),
    (5, 4), (6, 4), (6, 3), (7, 3), (8, 3),
    (9, 2), (10, 2), (11, 2), (12, 2), (13, 3),
    (13, 4), (14, 4), (15, 4),
]


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))


def lerp(a: float, b: float, t: float) -> float:
    return a + ((b - a) * t)


def s(value: float) -> float:
    return value * SUPERSAMPLE * SPRITE_DRAW_SCALE


def u(value: float) -> float:
    return value * SPRITE_DRAW_SCALE


def color_lerp(a: tuple[int, int, int], b: tuple[int, int, int], t: float) -> tuple[int, int, int]:
    return (
        int(lerp(a[0], b[0], t)),
        int(lerp(a[1], b[1], t)),
        int(lerp(a[2], b[2], t)),
    )


def ensure_dirs() -> None:
    ART_DIR.mkdir(parents=True, exist_ok=True)
    ANIM_DIR.mkdir(parents=True, exist_ok=True)


def finalize(image: Image.Image, target_size: int) -> Image.Image:
    if SUPERSAMPLE <= 1:
        return image
    return image.resize((target_size, target_size), Image.Resampling.LANCZOS)


def blank_sprite() -> tuple[Image.Image, ImageDraw.ImageDraw]:
    size = SPRITE_SIZE * SUPERSAMPLE
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    return image, ImageDraw.Draw(image, "RGBA")


def save_anim(image: Image.Image, name: str, index: int) -> None:
    finalize(image, SPRITE_SIZE).save(ANIM_DIR / f"{name}_{index:02d}.png")


def save_static(image: Image.Image, name: str) -> None:
    image.save(ART_DIR / f"{name}.png")


def force_opaque(image: Image.Image) -> Image.Image:
    result = image.convert("RGBA")
    result.putalpha(Image.new("L", result.size, 255))
    return result


def draw_soft_glow(
    draw: ImageDraw.ImageDraw,
    cx: float,
    cy: float,
    radius: float,
    rgb: tuple[int, int, int],
    alpha: int,
    steps: int = 12,
) -> None:
    for i in range(steps, 0, -1):
        t = i / steps
        r = radius * t
        a = int(alpha * t * t)
        draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(rgb[0], rgb[1], rgb[2], a))


def draw_shadow(draw: ImageDraw.ImageDraw, x: float, y: float, w: float, h: float, alpha: int = 90) -> None:
    draw.ellipse((x - w, y - h, x + w, y + h), fill=(14, 10, 10, alpha))


def draw_wrapped_dot(
    draw: ImageDraw.ImageDraw,
    x: float,
    y: float,
    radius: float,
    fill: tuple[int, int, int, int],
    tile_size: int,
) -> None:
    for ox in (-tile_size, 0, tile_size):
        for oy in (-tile_size, 0, tile_size):
            draw.ellipse(
                (x + ox - radius, y + oy - radius, x + ox + radius, y + oy + radius),
                fill=fill,
            )


def gen_tower_rail_lancer(frames: int = 6) -> None:
    center = SPRITE_CENTER * SUPERSAMPLE
    for i in range(frames):
        image, draw = blank_sprite()
        phase = i / frames
        sway = math.sin(phase * math.tau) * s(4.0)
        pulse = (math.sin(phase * math.tau) + 1.0) * 0.5
        recoil = (math.cos(phase * math.tau) + 1.0) * s(2.6)

        draw_shadow(draw, center, center + s(42), s(44), s(12))
        draw_soft_glow(draw, center, center + s(4), s(42), (86, 154, 255), 95)

        draw.ellipse((center - s(42), center - s(35), center + s(42), center + s(44)), fill=(30, 58, 118, 255))
        draw.ellipse((center - s(30), center - s(24), center + s(30), center + s(30)), fill=(62, 112, 208, 255))
        draw.ellipse((center - s(14), center - s(10), center + s(14), center + s(16)), fill=(154, 209, 255, 240))

        barrel_x = center + sway
        barrel_top = center - s(70) + recoil
        draw.rounded_rectangle(
            (barrel_x - s(12), barrel_top, barrel_x + s(12), center - s(6)),
            radius=int(s(8)),
            fill=(205, 224, 252, 255),
        )
        draw.rectangle((barrel_x - s(4), barrel_top - s(12), barrel_x + s(4), barrel_top), fill=(245, 250, 255, 255))

        muzzle_alpha = int(lerp(80, 180, pulse))
        draw_soft_glow(draw, barrel_x, barrel_top - s(12), s(14), (196, 226, 255), muzzle_alpha, steps=8)

        save_anim(image, "tower_rail_lancer", i)


def gen_tower_cinder_mortar(frames: int = 6) -> None:
    center = SPRITE_CENTER * SUPERSAMPLE
    for i in range(frames):
        image, draw = blank_sprite()
        phase = i / frames
        pulse = (math.sin(phase * math.tau) + 1.0) * 0.5
        recoil = pulse * s(5.2)

        draw_shadow(draw, center, center + s(42), s(46), s(13))
        draw_soft_glow(draw, center, center + s(4), s(40), (255, 126, 64), 80)

        draw.ellipse((center - s(44), center - s(32), center + s(44), center + s(44)), fill=(118, 58, 32, 255))
        draw.ellipse((center - s(30), center - s(20), center + s(30), center + s(28)), fill=(186, 98, 56, 255))
        draw.ellipse((center - s(13), center - s(10), center + s(13), center + s(14)), fill=(247, 178, 122, 210))

        top = center - s(70) + recoil
        draw.rounded_rectangle(
            (center - s(18), top, center + s(18), center - s(2)),
            radius=int(s(10)),
            fill=(62, 51, 48, 255),
        )

        ember_alpha = int(lerp(120, 235, pulse))
        draw_soft_glow(draw, center, top + s(20), s(18), (255, 170, 84), ember_alpha, steps=10)
        draw.ellipse((center - s(10), top + s(7), center + s(10), top + s(24)), fill=(255, 188, 98, 230))

        save_anim(image, "tower_cinder_mortar", i)


def gen_tower_frost_coil(frames: int = 6) -> None:
    center = SPRITE_CENTER * SUPERSAMPLE
    for i in range(frames):
        image, draw = blank_sprite()
        phase = i / frames
        pulse = (math.sin(phase * math.tau) + 1.0) * 0.5

        draw_shadow(draw, center, center + s(42), s(44), s(12))
        draw_soft_glow(draw, center, center, s(46), (140, 230, 255), 90)

        draw.ellipse((center - s(40), center - s(36), center + s(40), center + s(40)), fill=(44, 100, 142, 255))
        draw.ellipse((center - s(24), center - s(22), center + s(24), center + s(24)), fill=(116, 201, 236, 255))

        ring_radius = lerp(s(28), s(40), pulse)
        ring_alpha = int(lerp(120, 210, pulse))
        draw.ellipse(
            (center - ring_radius, center - ring_radius, center + ring_radius, center + ring_radius),
            outline=(214, 244, 255, ring_alpha),
            width=int(s(4)),
        )
        draw.ellipse((center - s(8), center - s(8), center + s(8), center + s(8)), fill=(222, 248, 255, 235))

        save_anim(image, "tower_frost_coil", i)


def gen_enemy_skitter_runner(frames: int = 8) -> None:
    center = SPRITE_CENTER * SUPERSAMPLE
    for i in range(frames):
        image, draw = blank_sprite()
        phase = i / frames
        bob = math.sin(phase * math.tau) * s(4.0)
        stride = math.cos(phase * math.tau) * s(6.0)

        x = center + stride
        y = center + bob
        draw_shadow(draw, x, y + s(42), s(34), s(10), 95)
        draw_soft_glow(draw, x, y + s(2), s(34), (255, 145, 86), 80)

        draw.ellipse((x - s(30), y - s(20), x + s(30), y + s(20)), fill=(232, 116, 66, 255))
        draw.ellipse((x - s(16), y - s(34), x + s(16), y - s(8)), fill=(255, 160, 100, 255))
        draw.ellipse((x - s(7), y - s(22), x + s(7), y - s(14)), fill=(255, 214, 172, 220))

        for leg_index, leg in enumerate((-22, -8, 8, 22)):
            leg_shift = s(5) if leg_index % 2 == 0 else -s(4)
            leg_y = y + s(14) + leg_shift
            leg_end_x = x + leg + (s(7) if stride > 0 else -s(7))
            draw.line((x + leg, leg_y, leg_end_x, leg_y + s(16)), fill=(114, 52, 28, 255), width=int(s(2.8)))

        save_anim(image, "enemy_skitter_runner", i)


def gen_enemy_carapace_brute(frames: int = 6) -> None:
    center = SPRITE_CENTER * SUPERSAMPLE
    for i in range(frames):
        image, draw = blank_sprite()
        phase = i / frames
        bob = math.sin(phase * math.tau) * s(2.8)
        pulse = (math.sin((phase + 0.18) * math.tau) + 1.0) * 0.5

        x = center
        y = center + bob
        draw_shadow(draw, x, y + s(44), s(44), s(12), 110)
        draw_soft_glow(draw, x, y + s(2), s(42), (194, 136, 92), 72)

        draw.ellipse((x - s(44), y - s(28), x + s(44), y + s(28)), fill=(114, 66, 44, 255))
        draw.ellipse((x - s(30), y - s(38), x + s(30), y + s(14)), fill=(156, 103, 72, 255))

        arc_shift = lerp(-s(4), s(4), pulse)
        draw.arc((x - s(36), y - s(28) + arc_shift, x + s(36), y + s(30) + arc_shift), 198, 342, fill=(230, 184, 142, 255), width=int(s(4)))
        draw.arc((x - s(22), y - s(20) + arc_shift, x + s(22), y + s(20) + arc_shift), 204, 336, fill=(248, 216, 188, 255), width=int(s(3)))

        save_anim(image, "enemy_carapace_brute", i)


def gen_enemy_ash_swarm(frames: int = 8) -> None:
    center = SPRITE_CENTER * SUPERSAMPLE
    for i in range(frames):
        image, draw = blank_sprite()
        phase = i / frames
        draw_shadow(draw, center, center + s(42), s(38), s(11), 86)

        for j in range(7):
            angle = (j / 7.0) * math.tau + (phase * math.tau * 1.35)
            radius = s(16 + ((j % 3) * 5))
            x = center + math.cos(angle) * radius
            y = center + math.sin(angle) * radius * 0.74
            size = s(8 + (j % 3))
            tone = 226 - (j * 14)
            draw.ellipse((x - size, y - size, x + size, y + size), fill=(tone, tone - 10, tone - 28, 232))

        core_pulse = (math.sin(phase * math.tau) + 1.0) * 0.5
        core_size = lerp(s(10), s(14), core_pulse)
        draw_soft_glow(draw, center, center, core_size * 1.6, (255, 238, 208), 120, steps=9)
        draw.ellipse((center - core_size, center - core_size, center + core_size, center + core_size), fill=(248, 236, 206, 245))

        save_anim(image, "enemy_ash_swarm", i)


def gen_enemy_plated_spore(frames: int = 6) -> None:
    center = SPRITE_CENTER * SUPERSAMPLE
    for i in range(frames):
        image, draw = blank_sprite()
        phase = i / frames
        pulse = (math.sin(phase * math.tau) + 1.0) * 0.5
        armor_shift = lerp(0.0, s(5), pulse)

        x = center
        y = center
        draw_shadow(draw, x, y + s(42), s(38), s(10), 96)
        draw_soft_glow(draw, x, y + s(4), s(40), (136, 206, 110), 80)

        draw.ellipse((x - s(30), y - s(30), x + s(30), y + s(30)), fill=(74, 134, 66, 255))
        draw.ellipse((x - s(20), y - s(20), x + s(20), y + s(20)), fill=(132, 194, 108, 255))
        draw.arc((x - s(36), y - s(36) + armor_shift, x + s(36), y + s(34) + armor_shift), 190, 352, fill=(206, 238, 158, 255), width=int(s(4)))
        draw.ellipse((x - s(7), y - s(7), x + s(7), y + s(7)), fill=(220, 244, 190, 230))

        save_anim(image, "enemy_plated_spore", i)


def gen_tile_grass() -> None:
    size = TILE_SIZE
    image = Image.new("RGBA", (size, size), (0, 0, 0, 255))
    pixels = image.load()

    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            n = (
                math.sin((u * math.tau * 2.0) + 0.7) * 0.40
                + math.sin((v * math.tau * 2.2) - 0.45) * 0.29
                + math.sin(((u + v) * math.tau * 1.6) + 1.2) * 0.21
                + math.sin(((u - v) * math.tau * 3.5) + 2.0) * 0.13
            )
            tone = clamp01(0.5 + (n * 0.24))
            base = color_lerp((40, 80, 46), (116, 184, 100), tone)
            pixels[x, y] = (base[0], base[1], base[2], 255)

    draw = ImageDraw.Draw(image, "RGBA")
    rng = random.Random(101)
    for _ in range(640):
        x = rng.random() * size
        y = rng.random() * size
        radius = rng.uniform(1.8, 4.8)
        tint = rng.randint(-18, 18)
        g = max(0, min(255, 140 + tint))
        fill = (54 + max(tint, 0), g, 56 + max(-tint, 0), rng.randint(36, 92))
        draw_wrapped_dot(draw, x, y, radius, fill, size)

    image = image.filter(ImageFilter.GaussianBlur(0.75))
    save_static(image, "tile_grass")


def gen_tile_path() -> None:
    size = TILE_SIZE
    image = Image.new("RGBA", (size, size), (0, 0, 0, 255))
    pixels = image.load()

    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            n = (
                math.sin((u * math.tau * 1.8) + 1.3) * 0.34
                + math.sin((v * math.tau * 2.2) + 2.1) * 0.25
                + math.sin(((u + v) * math.tau * 2.4) + 0.3) * 0.26
                + math.sin(((u - v) * math.tau * 4.8) + 1.0) * 0.15
            )
            tone = clamp01(0.5 + (n * 0.25))
            base = color_lerp((90, 64, 48), (160, 130, 98), tone)
            pixels[x, y] = (base[0], base[1], base[2], 255)

    draw = ImageDraw.Draw(image, "RGBA")
    rng = random.Random(202)
    for _ in range(520):
        x = rng.random() * size
        y = rng.random() * size
        radius = rng.uniform(1.6, 5.2)
        fill = (rng.randint(116, 178), rng.randint(94, 150), rng.randint(74, 122), rng.randint(42, 116))
        draw_wrapped_dot(draw, x, y, radius, fill, size)

    for _ in range(24):
        y = rng.random() * size
        thickness = rng.uniform(1.8, 4.2)
        color = (192, 154, 116, 34)
        draw.line((0, y, size, y + rng.uniform(-12, 12)), fill=color, width=int(thickness))

    image = image.filter(ImageFilter.GaussianBlur(0.8))
    save_static(image, "tile_path")


def gen_map_backdrop() -> None:
    width = MAP_WIDTH
    height = MAP_HEIGHT
    image = Image.new("RGBA", (width, height), (0, 0, 0, 255))
    pixels = image.load()

    for y in range(height):
        v = y / height
        for x in range(width):
            u = x / width
            n = (
                math.sin((u * math.tau * 1.0) + 0.4) * 0.30
                + math.sin((v * math.tau * 1.35) + 2.1) * 0.22
                + math.sin(((u + v) * math.tau * 1.1) + 1.2) * 0.16
            )
            horizon = clamp01(1.0 - abs((v - 0.36) / 0.58))
            tone = clamp01(0.28 + (n * 0.16) + (horizon * 0.46))
            rgb = color_lerp((20, 30, 44), (92, 122, 138), tone)
            warm = clamp01((0.52 - v) * 1.6)
            rgb = (
                min(255, int(rgb[0] + (warm * 16))),
                min(255, int(rgb[1] + (warm * 9))),
                min(255, int(rgb[2] + (warm * 4))),
            )
            if v > 0.68:
                ground_dark = clamp01((v - 0.68) * 2.2)
                rgb = (
                    int(rgb[0] * (1.0 - (ground_dark * 0.38))),
                    int(rgb[1] * (1.0 - (ground_dark * 0.34))),
                    int(rgb[2] * (1.0 - (ground_dark * 0.30))),
                )
            pixels[x, y] = (rgb[0], rgb[1], rgb[2], 255)

    draw = ImageDraw.Draw(image, "RGBA")
    rng = random.Random(404)

    # Soft cloud belts.
    for _ in range(26):
        cy = rng.uniform(-30, height * 0.70)
        ry = rng.uniform(36, 120)
        a = rng.randint(7, 18)
        draw.ellipse((-220, cy - ry, width + 220, cy + ry), fill=(150, 168, 180, a))

    # Distant ridge silhouettes.
    for layer in range(3):
        points = [(-120, height)]
        base_y = height * (0.55 + (layer * 0.08))
        for x in range(-120, width + 121, 96):
            t = x / width
            y = base_y + math.sin((t * math.tau * (0.8 + layer * 0.14)) + (layer * 1.1)) * (64 - layer * 14)
            points.append((x, y))
        points += [(width + 120, height), (-120, height)]
        tint = 48 + (layer * 18)
        draw.polygon(points, fill=(tint, tint + 10, tint + 18, 86 - layer * 18))

    # Ash particles.
    for _ in range(900):
        x = rng.uniform(0, width)
        y = rng.uniform(0, height)
        r = rng.uniform(0.8, 2.3)
        a = rng.randint(8, 28)
        t = rng.randint(148, 214)
        draw.ellipse((x - r, y - r, x + r, y + r), fill=(t, t - 8, t - 18, a))

    image = image.filter(ImageFilter.GaussianBlur(0.65))
    save_static(force_opaque(image), "map_backdrop")


def gen_map_surface_grayline_16x9() -> None:
    width = MAP_WIDTH
    height = MAP_HEIGHT
    board_w = MAP_BOARD_W
    board_h = MAP_BOARD_H
    cell = width / board_w

    grass_img = Image.new("RGBA", (width, height), (0, 0, 0, 255))
    grass_pixels = grass_img.load()

    for y in range(height):
        v = y / height
        for x in range(width):
            u_norm = x / width
            n = (
                math.sin((u_norm * math.tau * 1.25) + 0.7) * 0.34
                + math.sin((v * math.tau * 1.6) + 2.1) * 0.27
                + math.sin(((u_norm + v) * math.tau * 2.1) + 1.0) * 0.18
                + math.sin(((u_norm - v) * math.tau * 3.5) + 0.4) * 0.10
            )
            tone = clamp01(0.50 + (n * 0.23))
            c = color_lerp((62, 102, 82), (146, 186, 126), tone)
            wind = math.sin((u_norm * 9.5) + (v * 6.0)) * 0.04
            grass_pixels[x, y] = (
                max(0, min(255, int(c[0] + (wind * 22)))),
                max(0, min(255, int(c[1] + (wind * 30)))),
                max(0, min(255, int(c[2] + (wind * 14)))),
                255,
            )

    path_img = Image.new("RGBA", (width, height), (0, 0, 0, 255))
    path_pixels = path_img.load()
    for y in range(height):
        v = y / height
        for x in range(width):
            u_norm = x / width
            n = (
                math.sin((u_norm * math.tau * 2.0) + 1.1) * 0.32
                + math.sin((v * math.tau * 1.85) + 2.8) * 0.28
                + math.sin(((u_norm + v) * math.tau * 2.55) + 0.35) * 0.16
            )
            tone = clamp01(0.48 + (n * 0.24))
            c = color_lerp((102, 82, 64), (178, 144, 108), tone)
            dust = math.sin((u_norm * 21.0) + (v * 14.0)) * 0.05
            path_pixels[x, y] = (
                max(0, min(255, int(c[0] + (dust * 22)))),
                max(0, min(255, int(c[1] + (dust * 14)))),
                max(0, min(255, int(c[2] + (dust * 8)))),
                255,
            )

    path_mask = Image.new("L", (width, height), 0)
    mdraw = ImageDraw.Draw(path_mask)
    centers = [((cx + 0.5) * cell, (cy + 0.5) * cell) for cx, cy in GRAYLINE_PATH_CELLS]
    path_width = int(cell * 0.90)
    mdraw.line(centers, fill=255, width=path_width)
    radius = int(path_width * 0.5)
    for px, py in centers:
        mdraw.ellipse((px - radius, py - radius, px + radius, py + radius), fill=255)
    path_mask = path_mask.filter(ImageFilter.GaussianBlur(8))

    scene = Image.composite(path_img, grass_img, path_mask)
    draw = ImageDraw.Draw(scene, "RGBA")
    rng = random.Random(1307)

    # Terrain brush breakup.
    for _ in range(1100):
        x = rng.uniform(0, width)
        y = rng.uniform(0, height)
        r1 = rng.uniform(8, 24)
        r2 = r1 * rng.uniform(0.6, 1.5)
        a = rng.randint(10, 34)
        tone = rng.randint(84, 152)
        draw.ellipse((x - r1, y - r2, x + r1, y + r2), fill=(tone, tone + 18, tone + 8, a))

    # Path edge wear.
    for _ in range(6200):
        x = rng.uniform(0, width)
        y = rng.uniform(0, height)
        a = path_mask.getpixel((int(x), int(y))) / 255.0
        if 0.12 < a < 0.92:
            r = rng.uniform(1.2, 4.8)
            if a < 0.45:
                draw.ellipse((x - r, y - r, x + r, y + r), fill=(86, 72, 58, rng.randint(20, 54)))
            else:
                draw.ellipse((x - r, y - r, x + r, y + r), fill=(168, 136, 108, rng.randint(16, 42)))

    # Embedded rail debris to reduce clean CG look.
    for _ in range(48):
        px = rng.uniform(cell * 0.6, width - cell * 0.6)
        py = rng.uniform(cell * 0.6, height - cell * 0.6)
        w = rng.uniform(cell * 0.08, cell * 0.16)
        h = rng.uniform(cell * 0.03, cell * 0.08)
        draw.rounded_rectangle((px - w, py - h, px + w, py + h), radius=int(h * 0.5), fill=(108, 92, 80, rng.randint(84, 136)))

    # Subtle read-light and vignette pass.
    spx = scene.load()
    for y in range(height):
        v = y / height
        for x in range(width):
            u = x / width
            nx = (u - 0.5) / 0.5
            ny = (v - 0.5) / 0.5
            dist = math.sqrt((nx * nx) + (ny * ny))
            center_boost = 1.0 + (0.08 * clamp01(1.0 - (dist / 1.1)))
            top_light = 1.0 + (0.05 * clamp01(1.0 - abs((v - 0.34) / 0.58)))
            mult = max(0.78, min(1.14, center_boost * top_light))
            r, g, b, a = spx[x, y]
            spx[x, y] = (
                max(0, min(255, int(r * mult))),
                max(0, min(255, int(g * mult))),
                max(0, min(255, int(b * mult))),
                a,
            )

    scene = scene.filter(ImageFilter.GaussianBlur(0.28))
    save_static(force_opaque(scene), "map_surface_grayline_16x9")


def gen_map_shadow_overlay() -> None:
    width = MAP_WIDTH
    height = MAP_HEIGHT
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    pixels = image.load()
    for y in range(height):
        v = y / height
        for x in range(width):
            u = x / width
            nx = (u - 0.5) / 0.5
            ny = (v - 0.5) / 0.5
            dist = math.sqrt((nx * nx) + (ny * ny))
            edge = clamp01((dist - 0.58) / 0.50)
            alpha = int(edge * 18)
            pixels[x, y] = (8, 12, 16, alpha)

    draw = ImageDraw.Draw(image, "RGBA")
    rng = random.Random(1201)
    for _ in range(8):
        cx = rng.uniform(width * 0.1, width * 0.9)
        cy = rng.uniform(height * 0.15, height * 0.7)
        rx = rng.uniform(180, 420)
        ry = rng.uniform(70, 180)
        draw.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=(10, 14, 18, rng.randint(3, 8)))

    image = image.filter(ImageFilter.GaussianBlur(28))
    save_static(image, "map_shadow_overlay")


def gen_map_light_overlay() -> None:
    width = MAP_WIDTH
    height = MAP_HEIGHT
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    pixels = image.load()
    for y in range(height):
        v = y / height
        for x in range(width):
            u = x / width
            diag = (u * 0.65) + ((1.0 - v) * 0.35)
            band = clamp01(1.0 - abs((diag - 0.48) / 0.48))
            center = clamp01(1.0 - math.sqrt(((u - 0.5) ** 2) + ((v - 0.5) ** 2)) / 0.72)
            alpha = int((band * 5.0) + (center * 2.0))
            pixels[x, y] = (198, 218, 224, max(0, min(8, alpha)))

    draw = ImageDraw.Draw(image, "RGBA")
    rng = random.Random(1202)
    for _ in range(5):
        cx = rng.uniform(width * 0.22, width * 0.82)
        cy = rng.uniform(height * 0.22, height * 0.62)
        rx = rng.uniform(140, 260)
        ry = rng.uniform(80, 160)
        draw.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=(214, 192, 166, rng.randint(2, 6)))

    image = image.filter(ImageFilter.GaussianBlur(22))
    save_static(image, "map_light_overlay")


def gen_decal_ash_patch(name: str, seed: int, tone_shift: int) -> None:
    image = Image.new("RGBA", (SPRITE_SIZE, SPRITE_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    center = SPRITE_SIZE / 2.0
    rng = random.Random(seed)

    for _ in range(24):
        x = center + rng.uniform(-280, 280)
        y = center + rng.uniform(-280, 280)
        rx = rng.uniform(90, 220)
        ry = rng.uniform(70, 190)
        alpha = rng.randint(20, 62)
        base_r = max(0, min(255, 74 + tone_shift + rng.randint(-12, 14)))
        base_g = max(0, min(255, 72 + tone_shift + rng.randint(-14, 10)))
        base_b = max(0, min(255, 68 + tone_shift + rng.randint(-16, 8)))
        draw.ellipse((x - rx, y - ry, x + rx, y + ry), fill=(base_r, base_g, base_b, alpha))

    image = image.filter(ImageFilter.GaussianBlur(14))
    save_static(image, name)


def gen_decal_scrap_cluster(name: str, seed: int) -> None:
    image = Image.new("RGBA", (SPRITE_SIZE, SPRITE_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    center = SPRITE_SIZE / 2.0
    rng = random.Random(seed)

    for _ in range(16):
        x = center + rng.uniform(-260, 260)
        y = center + rng.uniform(-260, 260)
        w = rng.uniform(18, 72)
        h = rng.uniform(8, 30)
        angle = rng.uniform(0, math.tau)
        c = math.cos(angle)
        s_ = math.sin(angle)
        half_w = w * 0.5
        half_h = h * 0.5
        points = []
        for px, py in ((-half_w, -half_h), (half_w, -half_h), (half_w, half_h), (-half_w, half_h)):
            rx = (px * c) - (py * s_) + x
            ry = (px * s_) + (py * c) + y
            points.append((rx, ry))

        rust = rng.randint(0, 46)
        fill = (98 + rust, 86 - (rust // 3), 78 - (rust // 2), rng.randint(72, 132))
        draw.polygon(points, fill=fill)
        draw.line(points + [points[0]], fill=(212, 188, 156, rng.randint(24, 64)), width=2)

    for _ in range(60):
        x = center + rng.uniform(-320, 320)
        y = center + rng.uniform(-320, 320)
        r = rng.uniform(2.0, 5.0)
        draw.ellipse((x - r, y - r, x + r, y + r), fill=(118, 98, 82, rng.randint(40, 92)))

    image = image.filter(ImageFilter.GaussianBlur(1.6))
    save_static(image, name)


def gen_decal_path_crack(name: str, seed: int) -> None:
    image = Image.new("RGBA", (SPRITE_SIZE, SPRITE_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    rng = random.Random(seed)

    for _ in range(6):
        x = rng.uniform(120, SPRITE_SIZE - 120)
        y = rng.uniform(120, SPRITE_SIZE - 120)
        points = [(x, y)]
        length = rng.randint(6, 12)
        angle = rng.uniform(0, math.tau)
        for _ in range(length):
            angle += rng.uniform(-0.42, 0.42)
            dist = rng.uniform(28, 66)
            x += math.cos(angle) * dist
            y += math.sin(angle) * dist
            x = max(30, min(SPRITE_SIZE - 30, x))
            y = max(30, min(SPRITE_SIZE - 30, y))
            points.append((x, y))

        width = rng.randint(3, 7)
        draw.line(points, fill=(56, 40, 34, rng.randint(110, 180)), width=width)
        draw.line(points, fill=(172, 138, 108, rng.randint(28, 76)), width=max(1, width - 2))

    image = image.filter(ImageFilter.GaussianBlur(0.7))
    save_static(image, name)


def gen_decal_path_rail(name: str, seed: int) -> None:
    image = Image.new("RGBA", (SPRITE_SIZE, SPRITE_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    rng = random.Random(seed)

    center = SPRITE_SIZE / 2.0
    for lane in (-1, 1):
        base_y = center + (lane * rng.uniform(52, 72))
        draw.line((120, base_y, SPRITE_SIZE - 120, base_y), fill=(108, 92, 84, 156), width=14)
        draw.line((120, base_y - 2, SPRITE_SIZE - 120, base_y - 2), fill=(184, 166, 152, 98), width=4)

    for _ in range(10):
        x = rng.uniform(180, SPRITE_SIZE - 180)
        y = center + rng.uniform(-128, 128)
        draw.rounded_rectangle(
            (x - 40, y - 10, x + 40, y + 10),
            radius=8,
            fill=(104, 84, 72, rng.randint(92, 142)),
        )

    for _ in range(30):
        x = rng.uniform(140, SPRITE_SIZE - 140)
        y = center + rng.uniform(-160, 160)
        r = rng.uniform(2.0, 5.4)
        draw.ellipse((x - r, y - r, x + r, y + r), fill=(150, 108, 76, rng.randint(52, 110)))

    image = image.filter(ImageFilter.GaussianBlur(1.0))
    save_static(image, name)


def gen_prop_rail_barricade(name: str, seed: int) -> None:
    image = Image.new("RGBA", (SPRITE_SIZE, SPRITE_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    center = SPRITE_SIZE / 2.0
    rng = random.Random(seed)

    # Ground shadow.
    for i in range(8, 0, -1):
        t = i / 8.0
        rx = u(62) * t
        ry = u(20) * t
        a = int(lerp(0, 52, t * t))
        draw.ellipse((center - rx, center + u(44) - ry, center + rx, center + u(44) + ry), fill=(18, 14, 12, a))

    # Steel frame.
    body = (center - u(48), center - u(22), center + u(48), center + u(22))
    draw.rounded_rectangle(body, radius=int(u(8)), fill=(92, 90, 96, 236))
    draw.rounded_rectangle((body[0] + u(6), body[1] + u(6), body[2] - u(6), body[3] - u(6)), radius=int(u(6)), fill=(74, 72, 78, 238))

    # Warning stripe.
    stripe_h = u(14)
    stripe_top = center - (stripe_h * 0.5)
    for i in range(6):
        x0 = body[0] + i * u(16)
        draw.polygon(
            (
                (x0, stripe_top),
                (x0 + u(10), stripe_top),
                (x0 + u(4), stripe_top + stripe_h),
                (x0 - u(6), stripe_top + stripe_h),
            ),
            fill=(206, 128 + rng.randint(-10, 12), 58, 230),
        )

    # Bolts and wear.
    for dx in (-u(36), -u(12), u(12), u(36)):
        draw.ellipse((center + dx - u(4), center - u(12), center + dx + u(4), center - u(4)), fill=(164, 162, 156, 220))
        draw.ellipse((center + dx - u(3), center + u(4), center + dx + u(3), center + u(10)), fill=(164, 162, 156, 200))

    image = image.filter(ImageFilter.GaussianBlur(0.5))
    save_static(image, name)


def gen_prop_signal_post(name: str, seed: int, lamp_color: tuple[int, int, int]) -> None:
    image = Image.new("RGBA", (SPRITE_SIZE, SPRITE_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    center = SPRITE_SIZE / 2.0
    rng = random.Random(seed)

    # Shadow.
    draw.ellipse((center - u(36), center + u(56), center + u(36), center + u(82)), fill=(18, 14, 12, 52))

    # Pole and head.
    pole = (center - u(8), center - u(80), center + u(8), center + u(54))
    draw.rounded_rectangle(pole, radius=int(u(4)), fill=(90, 94, 102, 242))
    draw.rounded_rectangle((center - u(28), center - u(108), center + u(28), center - u(70)), radius=int(u(6)), fill=(68, 72, 82, 246))

    # Lamp glow.
    lamp_x = center + rng.uniform(-u(2), u(2))
    lamp_y = center - u(88)
    for i in range(9, 0, -1):
        t = i / 9.0
        r = u(8 + (26 * t))
        a = int(lerp(0, 66, t * t))
        draw.ellipse((lamp_x - r, lamp_y - r, lamp_x + r, lamp_y + r), fill=(lamp_color[0], lamp_color[1], lamp_color[2], a))
    draw.ellipse((lamp_x - u(8), lamp_y - u(8), lamp_x + u(8), lamp_y + u(8)), fill=(236, 244, 248, 242))

    # Rust specks.
    for _ in range(24):
        x = rng.uniform(center - u(28), center + u(28))
        y = rng.uniform(center - u(108), center + u(56))
        r = rng.uniform(u(1), u(2.6))
        draw.ellipse((x - r, y - r, x + r, y + r), fill=(126, 94, 76, rng.randint(28, 76)))

    image = image.filter(ImageFilter.GaussianBlur(0.5))
    save_static(image, name)


def gen_prop_wreck_crate(name: str, seed: int) -> None:
    image = Image.new("RGBA", (SPRITE_SIZE, SPRITE_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    center = SPRITE_SIZE / 2.0
    rng = random.Random(seed)

    draw.ellipse((center - u(46), center + u(48), center + u(46), center + u(74)), fill=(16, 12, 10, 58))

    # Crate silhouette.
    body = (
        (center - u(44), center + u(38)),
        (center + u(38), center + u(38)),
        (center + u(52), center - u(26)),
        (center - u(30), center - u(26)),
    )
    draw.polygon(body, fill=(112, 90, 70, 238))
    top = (
        (center - u(30), center - u(26)),
        (center + u(52), center - u(26)),
        (center + u(30), center - u(46)),
        (center - u(52), center - u(46)),
    )
    draw.polygon(top, fill=(138, 112, 86, 230))

    # Reinforcement bands.
    draw.line((center - u(28), center + u(34), center - u(16), center - u(42)), fill=(82, 74, 70, 196), width=int(u(6)))
    draw.line((center + u(8), center + u(36), center + u(20), center - u(44)), fill=(82, 74, 70, 196), width=int(u(6)))

    # Debris bits.
    for _ in range(18):
        x = center + rng.uniform(-u(62), u(62))
        y = center + rng.uniform(-u(10), u(74))
        r = rng.uniform(u(1.2), u(3.6))
        draw.ellipse((x - r, y - r, x + r, y + r), fill=(120, 98, 82, rng.randint(48, 112)))

    image = image.filter(ImageFilter.GaussianBlur(0.6))
    save_static(image, name)


def gen_build_marker() -> None:
    image = Image.new("RGBA", (SPRITE_SIZE, SPRITE_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    center = SPRITE_SIZE / 2.0

    for i in range(10, 0, -1):
        t = i / 10.0
        r = lerp(u(34), u(92), t)
        a = int(26 * t * t)
        draw.ellipse((center - r, center - r, center + r, center + r), fill=(86, 176, 232, a))

    draw.ellipse((center - u(70), center - u(70), center + u(70), center + u(70)), outline=(144, 222, 255, 220), width=int(u(6)))
    draw.ellipse((center - u(50), center - u(50), center + u(50), center + u(50)), outline=(172, 234, 255, 186), width=int(u(4)))
    draw.line((center - u(28), center, center + u(28), center), fill=(210, 246, 255, 170), width=int(u(3)))
    draw.line((center, center - u(28), center, center + u(28)), fill=(210, 246, 255, 170), width=int(u(3)))

    for i in range(6):
        ring = u(78 + i * 10)
        alpha = max(0, 42 - (i * 6))
        draw.ellipse((center - ring, center - ring, center + ring, center + ring), outline=(102, 184, 236, alpha), width=int(u(2)))

    save_static(image, "build_marker")


def gen_projectile_bolt() -> None:
    image = Image.new("RGBA", (SPRITE_SIZE, SPRITE_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    center = SPRITE_SIZE / 2.0

    for i in range(8, 0, -1):
        t = i / 8.0
        r = lerp(u(12), u(58), t)
        a = int(48 * t * t)
        draw.ellipse((center - r, center - r, center + r, center + r), fill=(255, 232, 140, a))

    bolt = [
        (center - u(18), center + u(28)),
        (center + u(2), center + u(8)),
        (center - u(8), center + u(8)),
        (center + u(16), center - u(30)),
        (center + u(2), center - u(4)),
        (center + u(16), center - u(4)),
    ]
    draw.polygon(bolt, fill=(255, 244, 184, 248))
    draw.line((center - u(3), center + u(20), center + u(12), center - u(20)), fill=(255, 255, 255, 200), width=int(u(3)))

    save_static(image.filter(ImageFilter.GaussianBlur(0.35)), "projectile_bolt")


def gen_enemy_slime() -> None:
    image = Image.new("RGBA", (SPRITE_SIZE, SPRITE_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    center = SPRITE_SIZE / 2.0

    draw_soft_glow(draw, center, center + u(10), u(70), (255, 116, 96), 72)
    draw.ellipse((center - u(54), center - u(18), center + u(54), center + u(62)), fill=(224, 92, 92, 248))
    draw.ellipse((center - u(38), center - u(44), center + u(38), center + u(22)), fill=(250, 128, 122, 250))
    draw.ellipse((center - u(20), center - u(18), center - u(6), center - u(2)), fill=(38, 22, 22, 220))
    draw.ellipse((center + u(6), center - u(18), center + u(20), center - u(2)), fill=(38, 22, 22, 220))
    draw.ellipse((center - u(8), center + u(6), center + u(8), center + u(18)), fill=(252, 184, 170, 180))
    draw.arc((center - u(24), center + u(18), center + u(24), center + u(42)), 12, 168, fill=(126, 56, 58, 170), width=int(u(3)))

    save_static(image.filter(ImageFilter.GaussianBlur(0.25)), "enemy_slime")


def gen_tower_basic() -> None:
    image = Image.open(ANIM_DIR / "tower_rail_lancer_00.png").convert("RGBA")
    save_static(image, "tower_basic")


def gen_tower_base_plate() -> None:
    image = Image.new("RGBA", (SPRITE_SIZE, SPRITE_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    center = SPRITE_SIZE / 2.0

    for i in range(14, 0, -1):
        t = i / 14.0
        r = lerp(u(120), u(360), t)
        a = int(34 * t * t)
        draw.ellipse((center - r, center - r, center + r, center + r), fill=(22, 20, 18, a))

    draw.ellipse((center - u(180), center - u(180), center + u(180), center + u(180)), fill=(84, 74, 66, 228))
    draw.ellipse((center - u(166), center - u(166), center + u(166), center + u(166)), fill=(58, 52, 48, 238))
    draw.ellipse((center - u(146), center - u(146), center + u(146), center + u(146)), outline=(168, 152, 136, 186), width=int(u(10)))
    draw.ellipse((center - u(118), center - u(118), center + u(118), center + u(118)), outline=(98, 124, 132, 166), width=int(u(8)))

    for idx in range(8):
        angle = (idx / 8.0) * math.tau
        x = center + math.cos(angle) * u(130)
        y = center + math.sin(angle) * u(130)
        draw.ellipse((x - u(10), y - u(10), x + u(10), y + u(10)), fill=(118, 104, 94, 214))
        draw.ellipse((x - u(4), y - u(4), x + u(4), y + u(4)), fill=(212, 198, 178, 210))

    image = image.filter(ImageFilter.GaussianBlur(0.6))
    save_static(image, "tower_base_plate")


def main() -> None:
    ensure_dirs()

    # Animated towers and enemies for demo combat loop.
    gen_tower_rail_lancer()
    gen_tower_cinder_mortar()
    gen_tower_frost_coil()
    gen_enemy_skitter_runner()
    gen_enemy_carapace_brute()
    gen_enemy_ash_swarm()
    gen_enemy_plated_spore()

    # Static level/UI resources used by the board and projectile renderer.
    gen_map_surface_grayline_16x9()
    gen_map_backdrop()
    gen_map_shadow_overlay()
    gen_map_light_overlay()
    gen_tile_grass()
    gen_tile_path()
    gen_decal_ash_patch("decal_ash_patch_a", 5101, -6)
    gen_decal_ash_patch("decal_ash_patch_b", 5102, 4)
    gen_decal_scrap_cluster("decal_scrap_cluster_a", 6201)
    gen_decal_scrap_cluster("decal_scrap_cluster_b", 6202)
    gen_decal_path_crack("decal_path_crack_a", 7101)
    gen_decal_path_crack("decal_path_crack_b", 7102)
    gen_decal_path_rail("decal_path_rail_a", 8101)
    gen_decal_path_rail("decal_path_rail_b", 8102)
    gen_prop_rail_barricade("prop_rail_barricade_a", 9101)
    gen_prop_rail_barricade("prop_rail_barricade_b", 9102)
    gen_prop_signal_post("prop_signal_post_a", 9201, (166, 218, 242))
    gen_prop_signal_post("prop_signal_post_b", 9202, (242, 198, 148))
    gen_prop_wreck_crate("prop_wreck_crate_a", 9301)
    gen_prop_wreck_crate("prop_wreck_crate_b", 9302)
    gen_build_marker()
    gen_projectile_bolt()
    gen_enemy_slime()
    gen_tower_basic()
    gen_tower_base_plate()

    print(f"Generated animated assets in {ANIM_DIR}")
    print(f"Generated static assets in {ART_DIR}")


if __name__ == "__main__":
    main()
