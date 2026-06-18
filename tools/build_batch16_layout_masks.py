from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

from td_layout_data import MAP_LAYOUTS

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "tmp" / "imagegen" / "batch16_masks"
W = 4096
H = 2304
GRID_W = 16
GRID_H = 9


def cell_to_xy(cell: tuple[int, int]) -> tuple[float, float]:
    cw = W / GRID_W
    ch = H / GRID_H
    return ((cell[0] + 0.5) * cw, (cell[1] + 0.5) * ch)


def lane_width(map_id: str, name: str) -> int:
    if map_id == "ashfall_depot":
        if name == "main":
            return 72
        if name in {"left", "right"}:
            return 64
        return 58

    # Late map needs narrower editable corridors so lanes don't merge into one blob.
    if map_id == "last_ember_terminus":
        if name == "main":
            return 62
        if name in {"left", "right"}:
            return 56
        return 50

    if name == "main":
        return 88
    if name in {"left", "right"}:
        return 78
    return 70


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)

    for item in MAP_LAYOUTS:
        map_id = item["map_id"]
        if map_id == "grayline_junction":
            # Keep early map single-lane art as baseline.
            continue

        alpha = Image.new("L", (W, H), 255)
        draw = ImageDraw.Draw(alpha)

        for lane_name, cells in item["lanes"].items():
            points = [cell_to_xy(c) for c in cells]
            width = lane_width(map_id, lane_name)
            draw.line(points, fill=0, width=width, joint="curve")
            cap = width // 2
            for x, y in points:
                draw.ellipse((x - cap, y - cap, x + cap, y + cap), fill=0)

        alpha = alpha.filter(ImageFilter.GaussianBlur(1.2))
        mask = Image.new("RGBA", (W, H), (255, 255, 255, 255))
        mask.putalpha(alpha)

        out_path = OUT / f"mask_{map_id}.png"
        mask.save(out_path)
        print(f"wrote {out_path}")


if __name__ == "__main__":
    main()
