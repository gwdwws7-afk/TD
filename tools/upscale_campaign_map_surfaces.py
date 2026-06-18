from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


MAP_IDS = (
    "grayline_junction",
    "ashfall_depot",
    "split_switch_canyon",
    "hollow_kiln_basin",
    "last_ember_terminus",
)

TARGET_SIZE = (4096, 2304)


def _to_opaque_rgba(image: Image.Image) -> Image.Image:
    rgb = image.convert("RGB")
    opaque = Image.new("RGBA", rgb.size, (0, 0, 0, 255))
    opaque.paste(rgb, (0, 0))
    return opaque


def upscale_map_surfaces(raw_dir: Path, out_dir: Path, art_dir: Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    art_dir.mkdir(parents=True, exist_ok=True)

    for map_id in MAP_IDS:
        src = raw_dir / f"map_surface_{map_id}_16x9_raw.png"
        if not src.exists():
            raise FileNotFoundError(f"missing raw map surface: {src}")

        with Image.open(src) as image:
            resized = image.convert("RGBA").resize(TARGET_SIZE, Image.Resampling.LANCZOS)
            final_image = _to_opaque_rgba(resized)

        out_path = out_dir / f"map_surface_{map_id}_16x9.png"
        art_path = art_dir / out_path.name
        final_image.save(out_path)
        final_image.save(art_path)
        print(f"wrote {out_path}")
        print(f"wrote {art_path}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Upscale campaign map surfaces to 4096x2304 and copy to Art resources.")
    parser.add_argument("--raw-dir", required=True)
    parser.add_argument("--out-dir", required=True)
    parser.add_argument("--art-dir", required=True)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    upscale_map_surfaces(Path(args.raw_dir), Path(args.out_dir), Path(args.art_dir))


if __name__ == "__main__":
    main()
