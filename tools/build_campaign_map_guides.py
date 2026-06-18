from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "output" / "imagegen" / "batch10_map_guides"

SIZE = (1536, 1024)
GRID_W = 16
GRID_H = 9

MAP_PATHS = {
    "grayline_junction": [
        (0, 5), (1, 4), (2, 4), (3, 4), (4, 4),
        (5, 4), (6, 4), (6, 3), (7, 3), (8, 3),
        (9, 2), (10, 2), (11, 2), (12, 2), (13, 3),
        (13, 4), (14, 4), (15, 4),
    ],
    "ashfall_depot": [
        (0, 6), (1, 6), (2, 6), (3, 6), (4, 6),
        (5, 5), (6, 5), (7, 5), (8, 4), (9, 4),
        (10, 4), (11, 3), (12, 3), (13, 3), (14, 4),
        (15, 4),
    ],
    "split_switch_canyon": [
        (0, 4), (1, 4), (2, 5), (3, 5), (4, 4),
        (5, 3), (6, 3), (7, 4), (8, 5), (9, 5),
        (10, 4), (11, 3), (12, 3), (13, 4), (14, 4),
        (15, 3),
    ],
    "hollow_kiln_basin": [
        (0, 3), (1, 3), (2, 3), (3, 4), (4, 5),
        (5, 5), (6, 4), (7, 3), (8, 3), (9, 4),
        (10, 5), (11, 5), (12, 4), (13, 3), (14, 3),
        (15, 4),
    ],
    "last_ember_terminus": [
        (0, 5), (1, 5), (2, 4), (3, 4), (4, 5),
        (5, 6), (6, 6), (7, 5), (8, 4), (9, 3),
        (10, 3), (11, 4), (12, 5), (13, 5), (14, 4),
        (15, 4),
    ],
}

MAP_ANCHORS = {
    "grayline_junction": [(2, 2), (13, 6)],
    "ashfall_depot": [(2, 7), (12, 1)],
    "split_switch_canyon": [(4, 7), (11, 1)],
    "hollow_kiln_basin": [(1, 1), (14, 6)],
    "last_ember_terminus": [(3, 7), (13, 1)],
}


def _cell_center(cell: tuple[int, int]) -> tuple[float, float]:
    cell_w = SIZE[0] / GRID_W
    cell_h = SIZE[1] / GRID_H
    return ((cell[0] + 0.5) * cell_w, (cell[1] + 0.5) * cell_h)


def _is_path_cell(path_set: set[tuple[int, int]], x: int, y: int) -> bool:
    return (x, y) in path_set


def _is_near_path(path_set: set[tuple[int, int]], x: int, y: int) -> bool:
    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            if ox == 0 and oy == 0:
                continue
            if (x + ox, y + oy) in path_set:
                return True
    return False


def _hash01(x: int, y: int, salt: int) -> float:
    n = (x * 73856093) ^ (y * 19349663) ^ (salt * 83492791)
    n ^= n >> 13
    n *= 1274126177
    n ^= n >> 16
    return float(n & 0x7FFFFFFF) / float(0x7FFFFFFF)


def _draw_background(draw: ImageDraw.ImageDraw) -> None:
    width, height = SIZE
    for y in range(height):
        t = y / max(1, height - 1)
        r = int(44 + (14 * (1.0 - t)))
        g = int(52 + (12 * (1.0 - t)))
        b = int(60 + (10 * (1.0 - t)))
        draw.line((0, y, width, y), fill=(r, g, b, 255))


def _draw_grid(draw: ImageDraw.ImageDraw) -> None:
    width, height = SIZE
    cell_w = width / GRID_W
    cell_h = height / GRID_H
    for gx in range(GRID_W + 1):
        x = gx * cell_w
        draw.line((x, 0, x, height), fill=(130, 142, 152, 54), width=1)
    for gy in range(GRID_H + 1):
        y = gy * cell_h
        draw.line((0, y, width, y), fill=(130, 142, 152, 54), width=1)


def _draw_path_overlay(base: Image.Image, path_cells: list[tuple[int, int]]) -> None:
    width, _ = SIZE
    cell_w = width / GRID_W
    points = [_cell_center(cell) for cell in path_cells]

    mask = Image.new("L", SIZE, 0)
    mdraw = ImageDraw.Draw(mask)
    path_width = int(cell_w * 0.90)
    mdraw.line(points, fill=255, width=path_width)
    radius = int(path_width * 0.5)
    for x, y in points:
        mdraw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(5))

    fill = Image.new("RGBA", SIZE, (196, 128, 82, 230))
    rim = Image.new("RGBA", SIZE, (240, 184, 128, 180))

    base.paste(fill, (0, 0), mask)
    edge = mask.filter(ImageFilter.MaxFilter(7))
    edge = ImageChops.subtract(edge, mask)
    base.paste(rim, (0, 0), edge)


def _draw_build_hints(draw: ImageDraw.ImageDraw, path_set: set[tuple[int, int]]) -> None:
    cell_w = SIZE[0] / GRID_W
    cell_h = SIZE[1] / GRID_H
    for y in range(GRID_H):
        for x in range(GRID_W):
            if _is_path_cell(path_set, x, y) or not _is_near_path(path_set, x, y):
                continue
            if _hash01(x, y, 701) > 0.22:
                continue
            cx, cy = _cell_center((x, y))
            rx = cell_w * 0.18
            ry = cell_h * 0.18
            draw.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), outline=(138, 208, 230, 170), width=2)


def _draw_anchor_hints(draw: ImageDraw.ImageDraw, map_id: str) -> None:
    cell_w = SIZE[0] / GRID_W
    cell_h = SIZE[1] / GRID_H
    for idx, (x, y) in enumerate(MAP_ANCHORS.get(map_id, [])):
        cx, cy = _cell_center((x, y))
        w = cell_w * (0.32 + (idx * 0.04))
        h = cell_h * (0.32 + (idx * 0.04))
        draw.polygon(
            ((cx, cy - h), (cx + w, cy), (cx, cy + h), (cx - w, cy)),
            fill=(246, 210, 132, 88),
            outline=(255, 228, 170, 190),
        )


def build_guide(map_id: str, path_cells: list[tuple[int, int]]) -> Path:
    image = Image.new("RGBA", SIZE, (0, 0, 0, 255))
    draw = ImageDraw.Draw(image, "RGBA")

    _draw_background(draw)
    _draw_grid(draw)
    _draw_path_overlay(image, path_cells)

    path_set = set(path_cells)
    _draw_build_hints(draw, path_set)
    _draw_anchor_hints(draw, map_id)

    out_path = OUT_DIR / f"guide_{map_id}_1536x1024.png"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(out_path)
    return out_path


def main() -> None:
    for map_id, path_cells in MAP_PATHS.items():
        out = build_guide(map_id, path_cells)
        print(f"wrote {out}")


if __name__ == "__main__":
    main()
