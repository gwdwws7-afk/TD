from __future__ import annotations

from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter
from td_layout_data import MAP_LAYOUTS

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets" / "Resources" / "Art"
OUT = ROOT / "output" / "imagegen" / "batch15_multilane_fix"
BACKUP = OUT / "backup_before"

W, H = 4096, 2304
GRID_W, GRID_H = 16, 9

PATHS = {
    item["map_id"]: item["lanes"]
    for item in MAP_LAYOUTS
    if item["map_id"] in {
        "ashfall_depot",
        "split_switch_canyon",
        "hollow_kiln_basin",
        "last_ember_terminus",
    }
}

STYLE = {
    "ashfall_depot": {
        "fill": (184, 138, 92),
        "edge": (88, 62, 42),
        "inner": (212, 168, 120),
    },
    "split_switch_canyon": {
        "fill": (176, 136, 96),
        "edge": (86, 62, 44),
        "inner": (206, 164, 118),
    },
    "hollow_kiln_basin": {
        "fill": (242, 142, 70),
        "edge": (255, 196, 120),
        "inner": (255, 228, 150),
    },
    "last_ember_terminus": {
        "fill": (246, 148, 78),
        "edge": (255, 206, 128),
        "inner": (255, 234, 160),
    },
}


def cell_center(cell: tuple[int, int]) -> tuple[float, float]:
    cw = W / GRID_W
    ch = H / GRID_H
    return ((cell[0] + 0.5) * cw, (cell[1] + 0.5) * ch)


def draw_lane_mask(mask: Image.Image, cells: list[tuple[int, int]], width: int) -> None:
    if not cells:
        return
    points = [cell_center(c) for c in cells]
    d = ImageDraw.Draw(mask)
    d.line(points, fill=255, width=width)
    r = max(3, width // 2)
    for x, y in points:
        d.ellipse((x - r, y - r, x + r, y + r), fill=255)


def _mask_with_strength(mask: Image.Image, strength: float) -> Image.Image:
    strength = max(0.0, min(1.0, strength))
    return mask.point(lambda p: int(p * strength))


def _composite_tint(base: Image.Image, color: tuple[int, int, int], mask: Image.Image, strength: float) -> Image.Image:
    tint = Image.new("RGB", base.size, color)
    return Image.composite(tint, base, _mask_with_strength(mask, strength))


def apply_lane_style(base: Image.Image, lane_mask: Image.Image, style: dict[str, tuple[int, int, int]]) -> Image.Image:

    soft_mask = lane_mask.filter(ImageFilter.GaussianBlur(3.0))
    core_mask = lane_mask.filter(ImageFilter.GaussianBlur(1.2))

    expanded = lane_mask.filter(ImageFilter.MaxFilter(17))
    edge_mask = ImageChops.subtract(expanded, lane_mask)
    edge_mask = edge_mask.filter(ImageFilter.GaussianBlur(2.0))

    result = base.copy()
    result = _composite_tint(result, style["fill"], soft_mask, 0.48)
    result = _composite_tint(result, style["edge"], edge_mask, 0.52)
    result = _composite_tint(result, style["inner"], core_mask, 0.34)
    return result


def build_map(map_id: str, img_path: Path) -> None:
    base = Image.open(img_path).convert("RGB")

    lane_mask = Image.new("L", base.size, 0)
    lane_sets = PATHS[map_id]

    draw_lane_mask(lane_mask, lane_sets.get("main", []), 94)
    draw_lane_mask(lane_mask, lane_sets.get("left", []), 86)
    draw_lane_mask(lane_mask, lane_sets.get("right", []), 86)
    draw_lane_mask(lane_mask, lane_sets.get("cross", []), 74)

    lane_mask = lane_mask.filter(ImageFilter.GaussianBlur(1.1))

    styled = apply_lane_style(base, lane_mask, STYLE[map_id])

    # Add a subtle dark underlay for readability without crushing value.
    under_mask = lane_mask.filter(ImageFilter.GaussianBlur(6.0))
    styled = _composite_tint(styled, (46, 36, 28), under_mask, 0.20)

    backup_path = BACKUP / img_path.name
    backup_path.parent.mkdir(parents=True, exist_ok=True)
    if not backup_path.exists():
        Image.open(img_path).save(backup_path)

    preview_path = OUT / img_path.name
    preview_path.parent.mkdir(parents=True, exist_ok=True)
    styled.save(preview_path)
    styled.save(img_path)
    print(f"updated {img_path.name}")


def main() -> None:
    targets = [
        "map_surface_ashfall_depot_16x9.png",
        "map_surface_split_switch_canyon_16x9.png",
        "map_surface_hollow_kiln_basin_16x9.png",
        "map_surface_last_ember_terminus_16x9.png",
    ]

    for name in targets:
        map_id = name.replace("map_surface_", "").replace("_16x9.png", "")
        path = ART / name
        if not path.exists():
            print(f"missing {name}")
            continue
        build_map(map_id, path)


if __name__ == "__main__":
    main()
