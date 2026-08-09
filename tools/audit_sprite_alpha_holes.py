#!/usr/bin/env python3
from __future__ import annotations

import json
import re
from collections import defaultdict, deque
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
ANIM_ROOT = ROOT / "Assets" / "Resources" / "Art" / "anim"
REPAIR_SHADER = ROOT / "Assets" / "Shaders" / "TDEnemyBodyRepair.shader"
SAMPLE_SIZE = 160
ALPHA_THRESHOLD = 40
UNEXPECTED_HOLE_RATIO = 0.08
UNEXPECTED_PLACEHOLDER_RATIO = 0.02
KNOWN_REPAIRS = {
    "enemy_cinder_glider": REPAIR_SHADER,
    "enemy_ember_leech": REPAIR_SHADER,
    "enemy_furnace_matriarch": REPAIR_SHADER,
}


def group_name(path: Path) -> str:
    return re.sub(r"_\d\d$", "", path.stem)


def enclosed_hole_ratio(path: Path) -> float:
    alpha = Image.open(path).convert("RGBA").getchannel("A")
    alpha = alpha.resize((SAMPLE_SIZE, SAMPLE_SIZE), Image.Resampling.BILINEAR)
    solid = [[alpha.getpixel((x, y)) >= ALPHA_THRESHOLD for x in range(SAMPLE_SIZE)] for y in range(SAMPLE_SIZE)]
    points = [(x, y) for y in range(SAMPLE_SIZE) for x in range(SAMPLE_SIZE) if solid[y][x]]
    if not points:
        return 0.0

    x0 = min(point[0] for point in points)
    x1 = max(point[0] for point in points)
    y0 = min(point[1] for point in points)
    y1 = max(point[1] for point in points)
    outside: set[tuple[int, int]] = set()
    queue: deque[tuple[int, int]] = deque()

    def enqueue(x: int, y: int) -> None:
        if not solid[y][x] and (x, y) not in outside:
            outside.add((x, y))
            queue.append((x, y))

    for x in range(x0, x1 + 1):
        enqueue(x, y0)
        enqueue(x, y1)
    for y in range(y0, y1 + 1):
        enqueue(x0, y)
        enqueue(x1, y)

    while queue:
        x, y = queue.popleft()
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if x0 <= nx <= x1 and y0 <= ny <= y1 and not solid[ny][nx] and (nx, ny) not in outside:
                outside.add((nx, ny))
                queue.append((nx, ny))

    visited = set(outside)
    largest_hole = 0
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if solid[y][x] or (x, y) in visited:
                continue
            size = 0
            queue.append((x, y))
            visited.add((x, y))
            while queue:
                px, py = queue.popleft()
                size += 1
                for nx, ny in ((px + 1, py), (px - 1, py), (px, py + 1), (px, py - 1)):
                    if x0 <= nx <= x1 and y0 <= ny <= y1 and not solid[ny][nx] and (nx, ny) not in visited:
                        visited.add((nx, ny))
                        queue.append((nx, ny))
            largest_hole = max(largest_hole, size)

    solid_area = sum(1 for y in range(y0, y1 + 1) for x in range(x0, x1 + 1) if solid[y][x])
    return largest_hole / max(1, solid_area + largest_hole)


def repeated_placeholder_ratio(path: Path) -> float:
    image = Image.open(path).convert("RGBA")
    visible_count = sum(image.getchannel("A").histogram()[1:])
    colors = image.getcolors(maxcolors=image.width * image.height) or []
    repeated_count = max(
        (
            count
            for count, pixel in colors
            if 12 <= pixel[3] <= 80 and pixel[0] >= 180 and pixel[0] > pixel[1] * 1.25
        ),
        default=0,
    )
    return repeated_count / max(1, visible_count)


def main() -> int:
    paths = sorted(
        path
        for path in ANIM_ROOT.glob("*.png")
        if path.name.startswith("enemy_") or path.name.startswith("tower_")
    )
    groups: dict[str, list[tuple[str, float, float]]] = defaultdict(list)
    for path in paths:
        groups[group_name(path)].append((path.name, enclosed_hole_ratio(path), repeated_placeholder_ratio(path)))

    ranked = sorted(
        (
            (name, max(item[1] for item in frames), max(item[2] for item in frames), len(frames))
            for name, frames in groups.items()
        ),
        key=lambda item: max(item[1], item[2]),
        reverse=True,
    )
    unexpected = []
    repaired = []
    for name, hole_ratio, placeholder_ratio, frame_count in ranked:
        needs_repair = hole_ratio >= UNEXPECTED_HOLE_RATIO or placeholder_ratio >= UNEXPECTED_PLACEHOLDER_RATIO
        if not needs_repair:
            continue
        repair = KNOWN_REPAIRS.get(name)
        if repair is not None and repair.exists():
            repaired.append({
                "group": name,
                "frames": frame_count,
                "holeRatio": round(hole_ratio, 3),
                "placeholderRatio": round(placeholder_ratio, 3),
                "repair": str(repair),
            })
        else:
            unexpected.append({
                "group": name,
                "frames": frame_count,
                "holeRatio": round(hole_ratio, 3),
                "placeholderRatio": round(placeholder_ratio, 3),
            })

    other = [
        {
            "group": name,
            "frames": count,
            "holeRatio": round(hole_ratio, 3),
            "placeholderRatio": round(placeholder_ratio, 3),
        }
        for name, hole_ratio, placeholder_ratio, count in ranked
        if name not in KNOWN_REPAIRS
    ][:5]
    result = {
        "pass": len(unexpected) == 0,
        "scannedFrames": len(paths),
        "scannedGroups": len(groups),
        "threshold": UNEXPECTED_HOLE_RATIO,
        "placeholderThreshold": UNEXPECTED_PLACEHOLDER_RATIO,
        "knownRepairs": repaired,
        "largestOtherGroups": other,
        "unexpected": unexpected,
    }
    print(json.dumps(result, ensure_ascii=True, indent=2))
    return 0 if result["pass"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
