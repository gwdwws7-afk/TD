from __future__ import annotations

from pathlib import Path
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ART_DIR = ROOT / "Assets" / "Resources" / "Art"
SIZE = (4096, 2304)


def _vignette_alpha(size: tuple[int, int], strength: int) -> Image.Image:
    width, height = size
    gradient = Image.radial_gradient("L").resize((width, width))
    gradient = gradient.crop((0, (width - height) // 2, width, ((width - height) // 2) + height))
    inverted = ImageChops.invert(gradient)
    return inverted.point(lambda p: int((p / 255.0) * strength))


def _make_shadow_overlay(size: tuple[int, int]) -> Image.Image:
    width, height = size
    overlay = Image.new("RGBA", size, (10, 14, 20, 0))
    alpha = _vignette_alpha(size, strength=160)

    draw = ImageDraw.Draw(alpha)
    random.seed(3407)

    for _ in range(9):
        cx = random.randint(int(width * 0.08), int(width * 0.92))
        cy = random.randint(int(height * 0.10), int(height * 0.90))
        rx = random.randint(220, 520)
        ry = random.randint(160, 380)
        local = random.randint(14, 42)
        draw.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=local)

    alpha = alpha.filter(ImageFilter.GaussianBlur(34))
    overlay.putalpha(alpha)
    return overlay


def _make_light_overlay(size: tuple[int, int]) -> Image.Image:
    width, height = size
    overlay = Image.new("RGBA", size, (206, 228, 238, 0))
    alpha = Image.new("L", size, 0)
    draw = ImageDraw.Draw(alpha)
    random.seed(1188)

    # Cooler top mist.
    draw.rectangle((0, 0, width, int(height * 0.30)), fill=20)

    # Warm pools around center path zones.
    warm_anchors = (
        (int(width * 0.18), int(height * 0.47), 640, 350, 48),
        (int(width * 0.52), int(height * 0.43), 820, 380, 54),
        (int(width * 0.80), int(height * 0.52), 700, 360, 44),
    )
    for cx, cy, rx, ry, a in warm_anchors:
        draw.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=a)

    # Subtle randomized bloom fragments.
    for _ in range(14):
        cx = random.randint(int(width * 0.05), int(width * 0.95))
        cy = random.randint(int(height * 0.08), int(height * 0.92))
        rx = random.randint(160, 380)
        ry = random.randint(110, 260)
        local = random.randint(10, 26)
        draw.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=local)

    alpha = alpha.filter(ImageFilter.GaussianBlur(48))
    overlay.putalpha(alpha)
    return overlay


def main() -> None:
    ART_DIR.mkdir(parents=True, exist_ok=True)

    shadow = _make_shadow_overlay(SIZE)
    light = _make_light_overlay(SIZE)

    shadow_path = ART_DIR / "map_shadow_overlay.png"
    light_path = ART_DIR / "map_light_overlay.png"
    shadow.save(shadow_path)
    light.save(light_path)

    print(f"wrote {shadow_path}")
    print(f"wrote {light_path}")


if __name__ == "__main__":
    main()
