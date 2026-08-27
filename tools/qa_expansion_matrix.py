#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Expansion batch-4 difficulty-tier matrix (plan v14 task 2).

Three-tier criteria per design/spec/balance-reweave-input-v1.md:
  A (L01-09)  calibration: curve unchanged, gate = win (anchor 15/15)
  B (L10-15)  platform:    acceptance = Standard meta-0 terminal wins,
                            all three strategies (post-expansion)
  C (L16-20)  exam:        pre-expansion below-tier is EXPECTED -
                            record-only, never tracked as defect
Judgment uses TERMINAL RUNS ONLY (full 20 waves to death/victory);
time probes are speed signals, never verdicts (§4).
Delta report compares against output/playtest/baseline_pre_expansion_20260825.

Usage:
  python tools/qa_expansion_matrix.py --tier B --difficulty Standard
  python tools/qa_expansion_matrix.py --tier B --difficulty Standard --seeds 1337 42 2024
"""

import argparse
import json
import os
import subprocess
import time

OUT = "output/playtest/expansion_matrix"
BASELINE = "output/playtest/baseline_pre_expansion_20260825"
TIERS = {"A": range(1, 10), "B": range(10, 16), "C": range(16, 21)}
STRATEGIES = ["adaptive_network", "focused_fire", "control_lattice"]
DIFFS = {"Standard": "Standard", "Veteran": "Veteran", "EmberTrial": "EmberTrial"}


def tier_of(level):
    for name, rng in TIERS.items():
        if level in rng:
            return name
    return "?"


def run_one(level, difficulty, strategy, seed):
    tag = "L%02d_%s_%s_s%d" % (level, difficulty[:3].lower(), strategy[:4], seed)
    summary = "%s/%s.json" % (OUT, tag)
    p124 = "%s/%s.p124.json" % (OUT, tag)
    cmd = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
           "-File", "tools/td_mcp_playtest.ps1",
           "-LevelIndex", str(level),
           "-FormationDifficulty", DIFFS[difficulty],
           "-DurationSeconds", "200",          # terminal run: no time-cap verdicts
           "-TimeScale", "16",
           "-P124MaxRealSeconds", "170",
           "-P124AutoplayStrategy", strategy,
           "-P124SiteVariant", "0",
           "-RandomSeed", str(seed),
           "-AllowConsoleIssues",
           "-P124RunReportPath", "E:/TD/" + p124,
           "-ScreenshotPath", "E:/TD/%s/%s.png" % (OUT, tag),
           "-SummaryPath", "E:/TD/" + summary]
    t0 = time.time()
    try:
        subprocess.run(cmd, cwd="E:/TD", timeout=520,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except subprocess.TimeoutExpired:
        pass
    try:
        p = json.load(open(p124, encoding="utf-8-sig"))
    except (OSError, ValueError):
        p = {}
    kinds = {}
    for tw in (p.get("towers") or []):
        kinds[tw.get("towerKind")] = kinds.get(tw.get("towerKind"), 0) + 1
    rec = {"tag": tag, "level": level, "tier": tier_of(level),
           "difficulty": difficulty, "strategy": strategy, "seed": seed,
           "victory": p.get("victory"), "waves": p.get("wavesCleared"),
           "towers": p.get("towersBuilt"), "kindCounts": kinds,
           "elapsed": round(time.time() - t0),
           "focusedStall": bool(strategy == "focused_fire"
                                 and p.get("towersBuilt") in (3, 4, None)
                                 and p.get("victory") is False)}
    print("[%s] tier=%s %s %s V=%s waves=%s towers=%s%s" % (
        tag, rec["tier"], difficulty, strategy, rec["victory"],
        rec["waves"], rec["towers"],
        "  <B.4>" if rec["focusedStall"] else ""), flush=True)
    return rec


def load_baseline_waves(level):
    try:
        d = json.load(open("%s/digest.json" % BASELINE, encoding="utf-8-sig"))
        for t in d.get("targeted", []):
            if t.get("level") == level and t.get("seed") == 1337:
                return t.get("wavesCleared")
    except (OSError, ValueError):
        pass
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tier", default="B", choices=["A", "B", "C", "all"])
    ap.add_argument("--difficulty", default="Standard",
                    choices=list(DIFFS))
    ap.add_argument("--seeds", nargs="+", type=int, default=[1337])
    ap.add_argument("--strategies", nargs="+", default=STRATEGIES)
    args = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)

    levels = sorted(set(l for t in (TIERS if args.tier == "all"
                                    else {args.tier: TIERS[args.tier]})
                        for l in TIERS[t]))
    results = []
    for level in levels:
        for seed in args.seeds:
            for strat in args.strategies:
                results.append(run_one(level, args.difficulty, strat, seed))
                json.dump(results, open("%s/summary.json" % OUT, "w",
                                        encoding="utf-8"),
                          ensure_ascii=False, indent=1)

    # tier digest + verdicts per the three-tier rule
    digest = []
    for level in levels:
        runs = [r for r in results if r["level"] == level]
        wins = sum(1 for r in runs if r["victory"])
        waves = sorted(r["waves"] for r in runs if r["waves"] is not None)
        median_w = waves[len(waves) // 2] if waves else None
        base = load_baseline_waves(level)
        entry = {"level": level, "tier": tier_of(level),
                 "runs": len(runs), "wins": wins,
                 "medianWaves": median_w,
                 "baselineWaves_s1337": base,
                 "deltaVsBaseline": (median_w - base
                                     if median_w is not None and base else None),
                 "b4Stalls": sum(1 for r in runs if r["focusedStall"])}
        if entry["tier"] == "A":
            entry["verdict"] = "GATE" if wins == len(runs) else "REGRESSION"
        elif entry["tier"] == "B":
            entry["verdict"] = ("ACCEPT" if wins == len(runs)
                                else "BELOW-TIER (reweave pending)")
        else:
            entry["verdict"] = ("EXPECTED-BELOW-TIER (record only)"
                                if wins < len(runs) else "ABOVE EXPECTATION")
        digest.append(entry)
    json.dump({"difficulty": args.difficulty, "seeds": args.seeds,
               "tiers": digest},
              open("%s/tier_digest.json" % OUT, "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    for e in digest:
        print(e, flush=True)
    print("DONE -> %s/tier_digest.json" % OUT, flush=True)


if __name__ == "__main__":
    main()
