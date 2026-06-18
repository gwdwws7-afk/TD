#!/usr/bin/env python3
"""Rebuild campaign waves with progression-aware pacing and multi-lane pressure.

Design intent:
- Early levels teach readable pressure with simpler compositions.
- Mid levels introduce armor/special counters and lane juggling.
- Late levels escalate to mixed, high-pressure, multi-lane exams.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
CAMPAIGN_PATH = ROOT / "Assets" / "Resources" / "Data" / "campaign" / "campaign_main_v1.json"
WAVES_DIR = ROOT / "Assets" / "Resources" / "Data" / "waves"
ENEMY_CATALOG_PATH = ROOT / "Assets" / "Resources" / "Data" / "enemies" / "enemy_catalog_main_v1.json"

CHAPTER_BOOST = {
    "chapter_a": 0.00,
    "chapter_b": 0.02,
    "chapter_c": 0.04,
    "chapter_d": 0.06,
}

LEVEL_POOL_OVERRIDES = {
    1: ["skitter_runner"],
    2: ["skitter_runner", "ash_swarm"],
    3: ["skitter_runner", "ash_swarm"],
    4: ["skitter_runner", "ash_swarm"],
    5: ["skitter_runner", "ash_swarm"],
    6: ["skitter_runner", "ash_swarm", "carapace_brute"],
    7: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore"],
    8: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore", "burrow_sapper"],
    9: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore", "burrow_sapper", "ember_leech"],
    10: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore", "ember_leech", "spore_carrier"],
    11: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore", "burrow_sapper", "spore_carrier", "rail_warden"],
    12: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore", "burrow_sapper", "ember_leech", "spore_carrier", "rail_warden"],
    13: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore", "spore_carrier", "rail_warden", "cinder_glider"],
    14: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore", "burrow_sapper", "spore_carrier", "rail_warden", "cinder_glider"],
    15: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore", "spore_carrier", "rail_warden", "cinder_glider", "husk_titan"],
    16: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore", "burrow_sapper", "ember_leech", "rail_warden", "cinder_glider", "husk_titan"],
    17: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore", "spore_carrier", "rail_warden", "cinder_glider", "husk_titan", "echo_mimic"],
    18: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore", "burrow_sapper", "ember_leech", "cinder_glider", "husk_titan", "echo_mimic"],
    19: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore", "burrow_sapper", "spore_carrier", "cinder_glider", "husk_titan", "echo_mimic"],
    20: ["skitter_runner", "ash_swarm", "carapace_brute", "plated_spore", "burrow_sapper", "spore_carrier", "rail_warden", "cinder_glider", "husk_titan", "echo_mimic", "furnace_matriarch"],
}


@dataclass
class EnemyInfo:
    enemy_id: str
    threat_cost: float
    tags: set[str]


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as f:
        return json.load(f)


def save_json(path: Path, data: dict[str, Any]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="\n") as f:
        json.dump(data, f, indent=4, ensure_ascii=False)
        f.write("\n")


def choose(options: list[str], seed: int) -> str:
    if not options:
        return ""
    return options[seed % len(options)]


def normalize_num(value: float) -> int | float:
    rounded = round(float(value), 2)
    if abs(rounded - round(rounded)) < 1e-6:
        return int(round(rounded))
    return rounded


def has_any_tag(enemy: EnemyInfo, tags: set[str]) -> bool:
    return bool(enemy.tags.intersection(tags))


def build_role_groups(pool: list[EnemyInfo]) -> dict[str, list[EnemyInfo]]:
    groups = {
        "fast": [],
        "swarm": [],
        "armored": [],
        "special": [],
        "boss": [],
    }

    for enemy in pool:
        tags = enemy.tags
        if "boss" in tags or "final" in tags:
            groups["boss"].append(enemy)
            continue

        if has_any_tag(enemy, {"fast", "flank"}):
            groups["fast"].append(enemy)
        if has_any_tag(enemy, {"swarm", "spawn"}):
            groups["swarm"].append(enemy)
        if has_any_tag(enemy, {"armored", "heavy", "elite"}):
            groups["armored"].append(enemy)
        if has_any_tag(enemy, {"special", "support", "mixed", "attrition"}):
            groups["special"].append(enemy)

    for key in groups:
        groups[key].sort(key=lambda x: (x.threat_cost, x.enemy_id))
    return groups


def phase_for_wave(level_index: int, wave_index: int) -> str:
    if level_index == 20 and wave_index == 20:
        return "boss"

    rem = wave_index % 3
    if rem == 1:
        return "introduce"
    if rem == 2:
        return "reinforce"
    return "exam"


def wave_role_sequence(
    wave_index: int,
    phase: str,
    new_enemy_id: str | None,
    new_enemy_is_boss: bool,
) -> list[str]:
    if phase == "boss":
        return ["armored", "swarm", "fast", "special", "boss"]

    if wave_index <= 3:
        seq = ["fast", "swarm"]
    elif wave_index <= 6:
        seq = ["fast", "swarm", "armored"]
    elif wave_index <= 9:
        seq = ["swarm", "fast", "armored", "special"]
    elif wave_index <= 12:
        seq = ["armored", "fast", "swarm", "special"]
    elif wave_index <= 16:
        seq = ["armored", "swarm", "fast", "special"]
    else:
        seq = ["armored", "swarm", "fast", "special", "armored"]

    if phase == "introduce" and len(seq) > 2:
        seq = seq[:-1]

    if phase == "exam" and len(seq) < 4:
        seq.append("armored")

    # Force a visible first-contact for newly unlocked enemies (except final boss).
    if new_enemy_id and (not new_enemy_is_boss) and wave_index in {2, 3, 4, 5, 10}:
        if seq:
            seq[0] = "new"

    return seq


def pick_enemy_for_role(
    role: str,
    pool: list[EnemyInfo],
    role_groups: dict[str, list[EnemyInfo]],
    seed: int,
    forced_enemy_id: str | None,
) -> EnemyInfo:
    pool_non_boss = [e for e in pool if "boss" not in e.tags and "final" not in e.tags]

    if role == "new" and forced_enemy_id:
        for enemy in pool:
            if enemy.enemy_id == forced_enemy_id:
                return enemy

    candidates: list[EnemyInfo]
    if role == "fast":
        candidates = [e for e in role_groups["fast"] if e in pool_non_boss]
    elif role == "swarm":
        candidates = [e for e in role_groups["swarm"] if e in pool_non_boss]
    elif role == "armored":
        candidates = [e for e in role_groups["armored"] if e in pool_non_boss]
    elif role == "special":
        candidates = [e for e in role_groups["special"] if e in pool_non_boss]
    elif role == "boss":
        candidates = [e for e in role_groups["boss"] if e in pool]
    else:
        candidates = pool_non_boss

    if not candidates:
        if role == "boss":
            candidates = [e for e in pool if "boss" in e.tags or "final" in e.tags]
        else:
            candidates = pool_non_boss if pool_non_boss else pool

    if not candidates:
        raise ValueError("No enemy candidates available for role selection.")

    candidates = sorted(candidates, key=lambda e: (e.threat_cost, e.enemy_id))
    return candidates[seed % len(candidates)]


def pick_formation(
    enemy: EnemyInfo,
    wave_index: int,
    chapter_id: str,
    has_multilane: bool,
    seed: int,
) -> str:
    tags = enemy.tags

    if "boss" in tags or "final" in tags:
        return "boss_entry"

    if has_any_tag(enemy, {"swarm", "spawn"}):
        if "spawn" in tags and wave_index >= 10:
            return "spawn_chain"
        return "pack"

    if has_any_tag(enemy, {"fast", "flank"}):
        if has_multilane and wave_index >= 12 and chapter_id in {"chapter_c", "chapter_d"}:
            return choose(["flank_stream", "flank_strike", "adaptive"], seed)
        if has_multilane and wave_index >= 6:
            return choose(["split_lane", "cross_lane", "stream"], seed)
        return "stream"

    if has_any_tag(enemy, {"armored", "heavy", "elite"}):
        if "elite" in tags and wave_index >= 14:
            return "elite_drop"
        return "stagger" if wave_index >= 12 else "burst"

    if has_any_tag(enemy, {"special", "support", "mixed", "attrition"}):
        if has_multilane and wave_index >= 14:
            return "pressure_mix"
        return "escort"

    return "stream"


def pick_lane(formation: str, has_multilane: bool, wave_index: int, seed: int) -> str | None:
    if not has_multilane:
        return None

    if formation == "split_lane":
        return "split_lane"
    if formation == "cross_lane":
        return "cross_lane"

    if formation in {"pressure_mix", "adaptive", "spawn_chain", "boss_entry"} and wave_index >= 12:
        return "all"

    return choose(["left", "right", "center"], seed)


def base_interval(enemy: EnemyInfo) -> float:
    tags = enemy.tags
    if "boss" in tags or "final" in tags:
        return 3.2
    if has_any_tag(enemy, {"fast", "flank"}):
        return 0.24
    if has_any_tag(enemy, {"swarm", "spawn"}):
        return 0.19
    if has_any_tag(enemy, {"armored", "heavy", "elite"}):
        return 0.75
    return 0.46


def clamp_count_by_cost(cost: float, count: int, role: str) -> int:
    if role == "boss":
        return 1

    if cost <= 1.0:
        cap = 40
    elif cost <= 2.5:
        cap = 28
    elif cost <= 4.0:
        cap = 18
    elif cost <= 7.0:
        cap = 12
    elif cost <= 12.0:
        cap = 8
    else:
        cap = 4

    return max(1, min(cap, count))


def min_count_for_role(role: str, enemy: EnemyInfo) -> int:
    if role == "boss":
        return 1

    if has_any_tag(enemy, {"swarm", "spawn"}):
        return 4
    if has_any_tag(enemy, {"fast", "flank"}):
        return 3
    if has_any_tag(enemy, {"armored", "heavy", "elite"}):
        return 1
    if has_any_tag(enemy, {"special", "support", "mixed", "attrition"}):
        return 1
    return 2


def build_threat_tags(
    phase: str,
    map_id: str,
    has_multilane: bool,
    wave_index: int,
    level_index: int,
    pool: list[EnemyInfo],
) -> list[str]:
    tags = [phase, map_id]

    if has_multilane and wave_index >= 6:
        tags.append("split")

    if any(has_any_tag(enemy, {"armored", "heavy", "elite"}) for enemy in pool) and wave_index >= 7:
        tags.append("armored")

    if any(has_any_tag(enemy, {"fast", "flank"}) for enemy in pool) and has_multilane and wave_index >= 9:
        tags.append("flank")

    if wave_index >= 13:
        tags.append("high_pressure")

    if wave_index >= 17:
        tags.append("endgame")

    if phase in {"exam", "boss"} and wave_index in {10, 15, 20}:
        tags.append("exam_peak")

    if level_index == 20 and wave_index == 20:
        tags.append("finale")

    # Deduplicate while preserving order.
    deduped: list[str] = []
    seen: set[str] = set()
    for tag in tags:
        if tag not in seen:
            seen.add(tag)
            deduped.append(tag)
    return deduped


def build_hint(level_index: int, wave_index: int, phase: str, has_multilane: bool) -> str:
    if phase == "introduce":
        core = "识别新威胁并调整火力分工。"
    elif phase == "reinforce":
        core = "持续压制并补齐覆盖空洞。"
    elif phase == "boss":
        core = "终局Boss与混编压制，优先击杀高价值目标。"
    else:
        core = "综合考核：输出、控制、反制链稳定性。"

    lane_note = " 多线同时压测。" if has_multilane and wave_index >= 7 else ""
    return f"[L{level_index:02d}] W{wave_index:02d}: {core}{lane_note}".strip()


def desired_budget(_baseline: float, chapter_id: str, wave_index: int, phase: str, level_index: int) -> float:
    chapter_index = {
        "chapter_a": 0,
        "chapter_b": 1,
        "chapter_c": 2,
        "chapter_d": 3,
    }.get(chapter_id, 0)

    base = 6.5 + (level_index * 1.7) + (chapter_index * 2.0)
    growth = 2.1 + (level_index * 0.11) + (chapter_index * 0.25)
    target = base + (growth * max(0, wave_index - 1))

    if phase == "exam":
        target *= 1.10
    elif phase == "boss":
        target *= 1.20

    if wave_index in {10, 15, 20}:
        target *= 1.08

    if level_index >= 17 and wave_index >= 17:
        target *= 1.08

    if level_index == 20 and wave_index == 20:
        target *= 1.12

    return max(4.0, target)


def compute_weights(size: int) -> list[float]:
    if size <= 1:
        return [1.0]
    if size == 2:
        return [0.56, 0.44]
    if size == 3:
        return [0.42, 0.30, 0.28]
    if size == 4:
        return [0.32, 0.24, 0.22, 0.22]
    return [0.26, 0.20, 0.18, 0.18, 0.18]


def main() -> int:
    campaign = load_json(CAMPAIGN_PATH)
    enemy_catalog = load_json(ENEMY_CATALOG_PATH)

    enemies: dict[str, EnemyInfo] = {}
    for raw in enemy_catalog.get("enemies") or []:
        enemy_id = str(raw.get("enemyId", "")).strip()
        if not enemy_id:
            continue
        enemies[enemy_id] = EnemyInfo(
            enemy_id=enemy_id,
            threat_cost=max(0.01, float(raw.get("threatCost", 1.0))),
            tags={str(t).strip().lower() for t in (raw.get("tags") or []) if str(t).strip()},
        )

    levels = sorted(campaign.get("levels") or [], key=lambda item: int(item.get("levelIndex", 0)))

    unlocked: list[str] = []
    for level in levels:
        for enemy_id in level.get("newEnemyUnlocks") or []:
            if enemy_id in enemies and enemy_id not in unlocked:
                unlocked.append(enemy_id)

        level_index = int(level.get("levelIndex", 0))
        chapter_id = str(level.get("chapterId", "chapter_a"))
        map_id = str(level.get("mapId", "grayline_junction"))
        wave_set_id = str(level.get("waveSetId", "")).strip()
        if not wave_set_id:
            continue

        wave_path = WAVES_DIR / f"{wave_set_id}.json"
        if not wave_path.exists():
            print(f"skip missing wave file: {wave_path.name}")
            continue

        wave_set = load_json(wave_path)
        old_waves = wave_set.get("waves") or []
        global_defaults = wave_set.get("globalDefaults") or {}
        spawn_min = max(0.01, float(global_defaults.get("spawnMinSpacing", 0.16)))
        base_reward = int(global_defaults.get("baseRewardGold", 20))
        base_prep = float(global_defaults.get("prepSeconds", 8.0))

        override_ids = LEVEL_POOL_OVERRIDES.get(level_index)
        if override_ids:
            pool = [enemies[eid] for eid in override_ids if eid in enemies]
        else:
            pool = [enemies[eid] for eid in unlocked if eid in enemies]
        if not pool:
            # Fallback to skitter_runner if campaign data is inconsistent.
            if "skitter_runner" in enemies:
                pool = [enemies["skitter_runner"]]
            else:
                raise ValueError("No enemies available to build waves.")

        role_groups = build_role_groups(pool)
        has_multilane = map_id in {
            "ashfall_depot",
            "split_switch_canyon",
            "hollow_kiln_basin",
            "last_ember_terminus",
        }

        new_unlocks = [eid for eid in (level.get("newEnemyUnlocks") or []) if eid in enemies]
        new_enemy_id = new_unlocks[0] if new_unlocks else None
        new_enemy_is_boss = bool(new_enemy_id and ("boss" in enemies[new_enemy_id].tags or "final" in enemies[new_enemy_id].tags))

        rebuilt_waves: list[dict[str, Any]] = []
        for wave_index in range(1, 21):
            phase = phase_for_wave(level_index, wave_index)
            seed_base = (level_index * 911) + (wave_index * 131)

            baseline_budget = float(old_waves[wave_index - 1].get("budgetTarget", 10.0)) if wave_index - 1 < len(old_waves) else (10.0 + wave_index * 3.0)
            target_budget = desired_budget(baseline_budget, chapter_id, wave_index, phase, level_index)

            sequence = wave_role_sequence(wave_index, phase, new_enemy_id, new_enemy_is_boss)
            weights = compute_weights(len(sequence))

            picked: list[tuple[str, EnemyInfo, str, str | None]] = []
            for idx, role in enumerate(sequence):
                seed = seed_base + (idx * 17)
                enemy = pick_enemy_for_role(role, pool, role_groups, seed, new_enemy_id)
                formation = pick_formation(enemy, wave_index, chapter_id, has_multilane, seed)
                lane = pick_lane(formation, has_multilane, wave_index, seed + 7)
                picked.append((role, enemy, formation, lane))

            groups: list[dict[str, Any]] = []
            actual_budget = 0.0
            for idx, (role, enemy, formation, lane) in enumerate(picked):
                enemy_cost = max(0.01, enemy.threat_cost)
                if role == "boss":
                    count = 1
                elif idx < len(picked) - 1:
                    count = int(round((target_budget * weights[idx]) / enemy_cost))
                else:
                    remaining = max(enemy_cost, target_budget - actual_budget)
                    count = int(round(remaining / enemy_cost))

                count = max(min_count_for_role(role, enemy), count)
                count = clamp_count_by_cost(enemy_cost, count, role)
                if role == "boss":
                    count = 1

                interval = base_interval(enemy)
                if phase == "introduce":
                    interval *= 1.05
                elif phase == "exam":
                    interval *= 0.92
                elif phase == "boss":
                    interval *= 1.08

                if wave_index >= 16:
                    interval *= 0.90

                if lane in {"all", "cross_lane"}:
                    interval *= 0.95

                interval = max(spawn_min + 0.01, interval)

                start_delay = (idx * 1.2) + (((seed_base + idx * 23) % 7) - 3) * 0.08
                if phase in {"exam", "boss"}:
                    start_delay -= idx * 0.12
                if wave_index >= 17:
                    start_delay -= idx * 0.08
                start_delay = max(0.0, start_delay)

                group = {
                    "enemyId": enemy.enemy_id,
                    "count": int(count),
                    "startDelay": normalize_num(start_delay),
                    "spawnInterval": normalize_num(interval),
                    "formation": formation,
                }
                if lane:
                    group["lane"] = lane

                groups.append(group)
                actual_budget += float(count) * enemy_cost

            prep_seconds = base_prep - (wave_index - 1) * 0.14
            if phase in {"exam", "boss"}:
                prep_seconds += 0.4
            if wave_index in {10, 20}:
                prep_seconds += 0.5
            if phase == "boss":
                prep_seconds += 1.0
            prep_seconds = max(4.5, prep_seconds)

            reward_gold = base_reward + int((wave_index * 2.0) + (level_index * 0.4))
            if phase == "exam":
                reward_gold += 3
            if phase == "boss":
                reward_gold += 12
            if wave_index in {10, 20}:
                reward_gold += 4

            if wave_index <= 6:
                tolerance = 1.10
            elif wave_index <= 12:
                tolerance = 1.07
            elif wave_index <= 16:
                tolerance = 1.05
            else:
                tolerance = 1.04
            if phase == "boss":
                tolerance = 1.05

            wave = {
                "waveIndex": wave_index,
                "phase": phase,
                "goalTag": f"{chapter_id}_{map_id}_l{level_index:02d}_w{wave_index:02d}_{phase}",
                "threatTags": build_threat_tags(phase, map_id, has_multilane, wave_index, level_index, pool),
                "prepSeconds": normalize_num(prep_seconds),
                "rewardGold": int(reward_gold),
                "budgetTarget": normalize_num(actual_budget),
                "budgetTolerance": normalize_num(tolerance),
                "hint": build_hint(level_index, wave_index, phase, has_multilane),
                "groups": groups,
            }
            rebuilt_waves.append(wave)

        wave_set["waves"] = rebuilt_waves
        save_json(wave_path, wave_set)
        print(f"rebuilt {wave_path.name}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())

