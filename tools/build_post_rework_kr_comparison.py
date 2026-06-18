from __future__ import annotations

from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from td_layout_data import MAP_LAYOUTS

ROOT = Path(__file__).resolve().parents[1]
OUR_ART = ROOT / "Assets" / "Resources" / "Art"
KR_REF = ROOT / "design" / "reference" / "kr_level"
OUT_PATH = ROOT / "design" / "reference" / "compare_post_rework_vs_kr_levels.png"

MAPS = MAP_LAYOUTS

GRID_W = 16
GRID_H = 9
COLORS = {
    "main": (255, 190, 60, 220),
    "left": (86, 200, 255, 215),
    "right": (130, 255, 150, 215),
    "cross": (255, 120, 150, 215),
}


def cell_to_xy(cell: tuple[int, int], w: int, h: int) -> tuple[float, float]:
    cw = w / GRID_W
    ch = h / GRID_H
    return ((cell[0] + 0.5) * cw, (cell[1] + 0.5) * ch)


def draw_paths(img: Image.Image, lane_map: dict[str, list[tuple[int, int]]]) -> None:
    draw = ImageDraw.Draw(img, "RGBA")
    w, h = img.size
    line_w = max(6, int(min(w, h) * 0.012))

    for lane_name, cells in lane_map.items():
        points = [cell_to_xy(cell, w, h) for cell in cells]
        color = COLORS.get(lane_name, (255, 255, 255, 200))
        draw.line(points, fill=color, width=line_w)
        if points:
            r = line_w * 0.7
            draw.ellipse((points[0][0]-r, points[0][1]-r, points[0][0]+r, points[0][1]+r), fill=(80,255,120,235))
            draw.ellipse((points[-1][0]-r, points[-1][1]-r, points[-1][0]+r, points[-1][1]+r), fill=(255,90,90,235))


def fit_cover(img: Image.Image, tw: int, th: int) -> Image.Image:
    src_w, src_h = img.size
    scale = max(tw / src_w, th / src_h)
    nw, nh = int(src_w * scale), int(src_h * scale)
    resized = img.resize((nw, nh), Image.Resampling.LANCZOS)
    x = (nw - tw) // 2
    y = (nh - th) // 2
    return resized.crop((x, y, x + tw, y + th))


def main() -> None:
    row_h = 420
    margin = 24
    panel_w = 820
    panel_h = 360
    title_h = 72
    total_w = margin + panel_w + 24 + panel_w + margin
    total_h = title_h + (row_h * len(MAPS)) + margin

    canvas = Image.new("RGB", (total_w, total_h), (11, 16, 24))
    draw = ImageDraw.Draw(canvas)

    font_title = ImageFont.load_default()
    font_text = ImageFont.load_default()

    draw.text((margin, 20), "Emberline Post-Rework Level Map vs Kingdom Rush Level Reference", fill=(240, 246, 255), font=font_title)
    draw.text((margin, 44), "Left: our current in-project map surface + active lane overlays | Right: KR level screenshot reference", fill=(170, 182, 202), font=font_text)

    y = title_h
    for item in MAPS:
        our_img = Image.open(OUR_ART / item["our_surface"]).convert("RGB")
        ref_img = Image.open(KR_REF / item["kr_ref"]).convert("RGB")

        our_fit = fit_cover(our_img, panel_w, panel_h)
        draw_paths(our_fit, item["lanes"])
        ref_fit = fit_cover(ref_img, panel_w, panel_h)

        x_left = margin
        x_right = margin + panel_w + 24
        y_top = y + 28

        canvas.paste(our_fit, (x_left, y_top))
        canvas.paste(ref_fit, (x_right, y_top))

        lane_count = len(item["lanes"])
        draw.text((x_left, y + 4), f"{item['map_id']}  [{item['stage']}]  lanes={lane_count}", fill=(238, 242, 248), font=font_text)
        draw.text((x_right, y + 4), f"KR ref: {item['kr_ref']}", fill=(238, 242, 248), font=font_text)

        # Divider line
        draw.rectangle((margin, y + row_h - 2, total_w - margin, y + row_h - 1), fill=(38, 48, 66))
        y += row_h

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(OUT_PATH)
    print(f"wrote {OUT_PATH}")


if __name__ == "__main__":
    main()
