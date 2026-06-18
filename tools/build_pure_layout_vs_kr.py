from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from td_layout_data import GRID_H, GRID_W, MAP_LAYOUTS

ROOT = Path(__file__).resolve().parents[1]
KR_REF = ROOT / "design" / "reference" / "kr_level"
LAYOUT_DIR = ROOT / "design" / "layout" / "pure"
SHEET_PATH = ROOT / "design" / "reference" / "compare_pure_layout_vs_kr.png"

COLORS = {
    "main": (255, 196, 86, 240),
    "left": (123, 213, 255, 236),
    "right": (130, 242, 167, 236),
    "cross": (255, 142, 175, 236),
}


def cell_to_xy(cell: tuple[int, int], w: int, h: int) -> tuple[float, float]:
    cw = w / GRID_W
    ch = h / GRID_H
    return ((cell[0] + 0.5) * cw, (cell[1] + 0.5) * ch)


def fit_cover(img: Image.Image, tw: int, th: int) -> Image.Image:
    src_w, src_h = img.size
    scale = max(tw / src_w, th / src_h)
    nw, nh = int(src_w * scale), int(src_h * scale)
    resized = img.resize((nw, nh), Image.Resampling.LANCZOS)
    x = (nw - tw) // 2
    y = (nh - th) // 2
    return resized.crop((x, y, x + tw, y + th))


def draw_lane(img: Image.Image, lane_name: str, cells: list[tuple[int, int]]) -> None:
    draw = ImageDraw.Draw(img, "RGBA")
    w, h = img.size
    base_w = max(18, int(min(w, h) * 0.026))
    outer = base_w + 8
    core = max(8, base_w - 6)

    points = [cell_to_xy(cell, w, h) for cell in cells]
    if len(points) < 2:
        return

    lane_color = COLORS.get(lane_name, (240, 240, 240, 240))
    draw.line(points, fill=(37, 45, 56, 230), width=outer, joint="curve")
    draw.line(points, fill=lane_color, width=base_w, joint="curve")
    draw.line(points, fill=(255, 255, 255, 130), width=core, joint="curve")

    radius = max(7, int(base_w * 0.55))
    sx, sy = points[0]
    ex, ey = points[-1]
    draw.ellipse((sx - radius, sy - radius, sx + radius, sy + radius), fill=(110, 255, 144, 240))
    draw.ellipse((ex - radius, ey - radius, ex + radius, ey + radius), fill=(255, 92, 92, 240))


def build_layout_image(item: dict, size: tuple[int, int]) -> Image.Image:
    w, h = size
    canvas = Image.new("RGB", size, (18, 24, 35))
    draw = ImageDraw.Draw(canvas, "RGBA")

    # Subtle board guides for cell readability
    for gx in range(GRID_W + 1):
        x = int((gx / GRID_W) * w)
        draw.line([(x, 0), (x, h)], fill=(40, 52, 74, 110), width=1)
    for gy in range(GRID_H + 1):
        y = int((gy / GRID_H) * h)
        draw.line([(0, y), (w, y)], fill=(40, 52, 74, 110), width=1)

    for lane_name, cells in item["lanes"].items():
        draw_lane(canvas, lane_name, cells)

    return canvas


def main() -> None:
    layout_w, layout_h = 1536, 864
    sheet_margin = 24
    sheet_gap = 24
    row_h = 360
    panel_w = 820
    panel_h = 300
    title_h = 74

    LAYOUT_DIR.mkdir(parents=True, exist_ok=True)

    for item in MAP_LAYOUTS:
        layout = build_layout_image(item, (layout_w, layout_h))
        out_path = LAYOUT_DIR / f"layout_{item['map_id']}_pure.png"
        layout.save(out_path)
        print(f"wrote {out_path}")

    total_w = sheet_margin + panel_w + sheet_gap + panel_w + sheet_margin
    total_h = title_h + (row_h * len(MAP_LAYOUTS)) + sheet_margin
    sheet = Image.new("RGB", (total_w, total_h), (10, 16, 26))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()

    draw.text((sheet_margin, 20), "Emberline Pure Layout vs Kingdom Rush Level Reference", fill=(240, 247, 255), font=font)
    draw.text((sheet_margin, 44), "Left: pure lane layout only | Right: KR level screenshot reference", fill=(172, 186, 204), font=font)

    y = title_h
    for item in MAP_LAYOUTS:
        layout = Image.open(LAYOUT_DIR / f"layout_{item['map_id']}_pure.png").convert("RGB")
        ref = Image.open(KR_REF / item["kr_ref"]).convert("RGB")

        left_img = fit_cover(layout, panel_w, panel_h)
        right_img = fit_cover(ref, panel_w, panel_h)

        x_left = sheet_margin
        x_right = sheet_margin + panel_w + sheet_gap
        y_top = y + 28
        sheet.paste(left_img, (x_left, y_top))
        sheet.paste(right_img, (x_right, y_top))

        lane_count = len(item["lanes"])
        draw.text((x_left, y + 4), f"{item['map_id']} [{item['stage']}] lanes={lane_count}", fill=(238, 242, 248), font=font)
        draw.text((x_right, y + 4), f"KR ref: {item['kr_ref']}", fill=(238, 242, 248), font=font)
        draw.rectangle((sheet_margin, y + row_h - 2, total_w - sheet_margin, y + row_h - 1), fill=(40, 52, 70))
        y += row_h

    SHEET_PATH.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(SHEET_PATH)
    print(f"wrote {SHEET_PATH}")


if __name__ == "__main__":
    main()

