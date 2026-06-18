#!/usr/bin/env python3
"""Auto-correct wave budgetTarget values to satisfy wave-schema budget bounds.

Rule:
  actual_cost = sum(group.count * enemy.threatCost)
  lower_bound = budgetTarget * (2 - budgetTolerance)
  upper_bound = budgetTarget * budgetTolerance

For each wave, this script minimally adjusts budgetTarget by clamping the
existing target into the mathematically valid target interval derived from
actual_cost and tolerance:
  target in [actual / tolerance, actual / (2 - tolerance)]
"""

from __future__ import annotations

import argparse
import json
import math
from dataclasses import dataclass
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
WAVES_DIR = REPO_ROOT / "Assets" / "Resources" / "Data" / "waves"
GLOBAL_ENEMY_CATALOG_PATH = (
    REPO_ROOT / "Assets" / "Resources" / "Data" / "enemies" / "enemy_catalog_main_v1.json"
)


@dataclass
class FixStats:
    files_scanned: int = 0
    files_changed: int = 0
    waves_scanned: int = 0
    waves_changed: int = 0
    missing_enemy_refs: int = 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Fix budgetTarget in wave json files.")
    parser.add_argument("--dry-run", action="store_true", help="Only report changes without writing files.")
    parser.add_argument("--precision", type=int, default=2, help="Decimal precision for rewritten targets.")
    return parser.parse_args()


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as f:
        return json.load(f)


def write_json(path: Path, data: dict[str, Any]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="\n") as f:
        json.dump(data, f, indent=4, ensure_ascii=False)
        f.write("\n")


def as_float(value: Any, fallback: float) -> float:
    try:
        if value is None:
            return fallback
        return float(value)
    except (TypeError, ValueError):
        return fallback


def normalize_number(value: float, precision: int) -> float | int:
    rounded = round(float(value), precision)
    if abs(rounded - round(rounded)) < (10 ** -(precision + 2)):
        return int(round(rounded))
    return rounded


def compute_wave_actual_cost(
    wave: dict[str, Any], enemy_catalog: dict[str, dict[str, Any]], stats: FixStats
) -> float:
    actual = 0.0
    for group in wave.get("groups") or []:
        enemy_id = group.get("enemyId")
        count = int(as_float(group.get("count"), 0))
        if count <= 0 or not enemy_id:
            continue

        enemy = enemy_catalog.get(enemy_id)
        if enemy is None:
            stats.missing_enemy_refs += 1
            continue

        threat_cost = as_float(enemy.get("threatCost"), 0.0)
        actual += count * max(0.0, threat_cost)
    return actual


def clamp_target_for_schema(actual: float, tolerance: float, current_target: float) -> float:
    safe_tol = tolerance if tolerance > 0 else 1.0
    safe_tol = max(0.5, min(1.5, safe_tol))

    lower_target = actual / safe_tol
    denominator = 2.0 - safe_tol
    if abs(denominator) < 1e-6:
        upper_target = lower_target
    else:
        upper_target = actual / denominator

    if lower_target > upper_target:
        lower_target, upper_target = upper_target, lower_target

    if current_target <= 0:
        return max(0.0, actual)

    return min(max(current_target, lower_target), upper_target)


def quantize_inside_bounds(value: float, lower: float, upper: float, precision: int) -> float:
    step = 10 ** (-precision)
    low_i = math.ceil((lower - 1e-12) / step)
    high_i = math.floor((upper + 1e-12) / step)
    if low_i > high_i:
        return min(max(value, lower), upper)

    desired_i = int(round(value / step))
    clamped_i = min(max(desired_i, low_i), high_i)
    return clamped_i * step


def build_global_catalog() -> dict[str, dict[str, Any]]:
    global_data = load_json(GLOBAL_ENEMY_CATALOG_PATH)
    catalog: dict[str, dict[str, Any]] = {}
    for enemy in global_data.get("enemies") or []:
        enemy_id = enemy.get("enemyId")
        if enemy_id:
            catalog[str(enemy_id)] = enemy
    return catalog


def build_merged_catalog(
    global_catalog: dict[str, dict[str, Any]], wave_set: dict[str, Any]
) -> dict[str, dict[str, Any]]:
    merged = dict(global_catalog)
    for enemy in wave_set.get("enemyCatalog") or []:
        enemy_id = enemy.get("enemyId")
        if enemy_id:
            merged[str(enemy_id)] = enemy
    return merged


def fix_file(path: Path, global_catalog: dict[str, dict[str, Any]], precision: int, dry_run: bool, stats: FixStats) -> list[str]:
    data = load_json(path)
    merged_catalog = build_merged_catalog(global_catalog, data)
    waves = data.get("waves") or []
    changed_lines: list[str] = []
    changed = False

    for wave in waves:
        stats.waves_scanned += 1
        wave_index = int(as_float(wave.get("waveIndex"), 0))
        tolerance = as_float(wave.get("budgetTolerance"), 1.0)
        current_target = as_float(wave.get("budgetTarget"), 0.0)
        actual = compute_wave_actual_cost(wave, merged_catalog, stats)
        adjusted_target = clamp_target_for_schema(actual, tolerance, current_target)

        safe_tol = max(0.5, min(1.5, tolerance if tolerance > 0 else 1.0))
        lower = actual / safe_tol
        denom = 2.0 - safe_tol
        upper = lower if abs(denom) < 1e-6 else actual / denom
        if lower > upper:
            lower, upper = upper, lower

        quantized_target = quantize_inside_bounds(adjusted_target, lower, upper, precision)
        normalized_target = normalize_number(quantized_target, precision)

        if abs(float(normalized_target) - current_target) > 1e-9:
            stats.waves_changed += 1
            changed = True
            wave["budgetTarget"] = normalized_target
            changed_lines.append(
                f"  wave {wave_index:02d}: {current_target:.2f} -> {float(normalized_target):.2f} (actual={actual:.2f}, tol={tolerance:.2f})"
            )

    if changed and not dry_run:
        write_json(path, data)
        stats.files_changed += 1

    return changed_lines


def main() -> int:
    args = parse_args()
    stats = FixStats()
    global_catalog = build_global_catalog()

    if not WAVES_DIR.exists():
        raise FileNotFoundError(f"Waves directory not found: {WAVES_DIR}")

    all_reports: list[str] = []
    wave_files = sorted(WAVES_DIR.glob("*.json"))
    stats.files_scanned = len(wave_files)

    for path in wave_files:
        changes = fix_file(path, global_catalog, args.precision, args.dry_run, stats)
        if changes:
            all_reports.append(path.name)
            all_reports.extend(changes)

    mode = "DRY-RUN" if args.dry_run else "WRITE"
    print(
        f"[{mode}] scanned_files={stats.files_scanned} changed_files={stats.files_changed} "
        f"scanned_waves={stats.waves_scanned} changed_waves={stats.waves_changed} "
        f"missing_enemy_refs={stats.missing_enemy_refs}"
    )
    if all_reports:
        print("\n".join(all_reports))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
