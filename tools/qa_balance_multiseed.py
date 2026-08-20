#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Multi-seed balance matrix (plan v7 task 1, 2026-08-20).

Protocol (design appendix B.5 + QA multi-seed requirement):
  - 25-run gate via the existing suite (single batch, -Force)
  - L13/L20 targeted: 3 seeds x 5 variants each
  - Formation criterion: SiegeDrill present in adaptive/control formations
  - B.4 triage: focused runs stalling at 3-4 towers with big unspent budget
    are flagged as the archived build-loop stall, NOT quota attribution
  - GravSnare: record honestly if still never picked

Usage: python tools/qa_balance_multiseed.py
Writes output/playtest/balance_multiseed/ (per-run JSON copies + summary).
"""

import json
import os
import subprocess
import time

OUT = "output/playtest/balance_multiseed"
SEEDS = [1337, 42, 2024]
VARIANTS = [
    ("adaptive_network", 0),
    ("focused_fire", 0),
    ("control_lattice", 0),
    ("adaptive_network", 1),
    ("focused_fire", 1),
]
LEVELS = [13, 20]


def run_one(level, strategy, site, seed, tag):
    summary = "%s/%s.json" % (OUT, tag)
    p124 = "%s/%s.p124.json" % (OUT, tag)
    cmd = [
        "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
        "-File", "tools/td_mcp_playtest.ps1",
        "-LevelIndex", str(level),
        "-DurationSeconds", "170",
        "-TimeScale", "16",
        "-P124MaxRealSeconds", "150",
        "-P124AutoplayStrategy", strategy,
        "-P124SiteVariant", str(site),
        "-RandomSeed", str(seed),
        "-AllowConsoleIssues",
        "-P124RunReportPath", "E:/TD/" + p124,
        "-ScreenshotPath", "E:/TD/%s/%s.png" % (OUT, tag),
        "-SummaryPath", "E:/TD/" + summary,
    ]
    t0 = time.time()
    try:
        subprocess.run(cmd, cwd="E:/TD", timeout=420,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except subprocess.TimeoutExpired:
        pass
    entry = {"tag": tag, "level": level, "strategy": strategy, "site": site,
             "seed": seed, "elapsed": round(time.time() - t0)}
    for src, key in ((summary, "script"), (p124, "p124")):
        try:
            with open(src, encoding="utf-8-sig") as f:
                entry[key] = json.load(f)
        except (OSError, ValueError):
            entry[key] = None
    p = entry.get("p124") or {}
    entry["victory"] = p.get("victory")
    entry["wavesCleared"] = p.get("wavesCleared")
    entry["towers"] = p.get("towersBuilt")
    kinds = {}
    for tw in (p.get("towers") or []):
        kinds[tw.get("towerKind")] = kinds.get(tw.get("towerKind"), 0) + 1
    entry["kindCounts"] = kinds
    sd = p.get("strategyId", strategy)
    stalled = (sd == "focused_fire" and entry["towers"] in (3, 4, None)
               and entry["victory"] is False)
    entry["focusedStallFlag"] = bool(stalled)
    print("[%s] L%d %s s%d seed=%d V=%s waves=%s towers=%s kinds=%s%s" % (
        tag, level, strategy, site, seed, entry["victory"],
        entry["wavesCleared"], entry["towers"], kinds,
        "  <B.4-STALL?>" if stalled else ""), flush=True)
    return entry


def main():
    os.makedirs(OUT, exist_ok=True)
    results = []
    # targeted L13/L20 multi-seed first (the decision data), gate after
    for level in LEVELS:
        for seed in SEEDS:
            for strategy, site in VARIANTS:
                tag = "L%d_s%d_%s_v%d" % (level, seed, strategy, site)
                results.append(run_one(level, strategy, site, seed, tag))
                with open("%s/summary.json" % OUT, "w", encoding="utf-8") as f:
                    json.dump(results, f, ensure_ascii=False, indent=1)
    # 25-run gate (single batch, force rerun)
    print("=== 25-run gate ===", flush=True)
    subprocess.run(
        ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
         "-File", "tools/td_raillancer_balance_regression.ps1", "-Force"],
        cwd="E:/TD", stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        gate = json.load(open(
            "output/playtest/balance_regression/status.json",
            encoding="utf-8-sig"))
    except (OSError, ValueError):
        gate = {}
    print("gate:", {k: gate.get(k) for k in
                    ("singleKindRailLancerClears", "victories", "state")},
          flush=True)

    # digest
    digest = {"gate": gate, "targeted": []}
    for level in LEVELS:
        for seed in SEEDS:
            runs = [r for r in results
                    if r["level"] == level and r["seed"] == seed]
            wins = sum(1 for r in runs if r["victory"])
            sd_forms = sum(1 for r in runs
                           if "SiegeDrill" in r["kindCounts"]
                           and r["strategy"] != "focused_fire")
            gs_forms = sum(1 for r in runs if "GravSnare" in r["kindCounts"])
            stalls = sum(1 for r in runs if r["focusedStallFlag"])
            digest["targeted"].append({
                "level": level, "seed": seed, "wins": "%d/5" % wins,
                "siegeDrillInAdaptiveControl": "%d/3" % sd_forms,
                "gravSnareAppearances": gs_forms,
                "focusedStallFlags": stalls,
                "wavesCleared": [r["wavesCleared"] for r in runs],
                "towerCounts": [r["towers"] for r in runs]})
    with open("%s/digest.json" % OUT, "w", encoding="utf-8") as f:
        json.dump(digest, f, ensure_ascii=False, indent=1)
    print(json.dumps(digest["targeted"], ensure_ascii=False, indent=1),
          flush=True)
    print("DONE -> %s/digest.json" % OUT, flush=True)


if __name__ == "__main__":
    main()
