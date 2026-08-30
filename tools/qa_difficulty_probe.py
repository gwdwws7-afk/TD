#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Difficulty probe sweep (plan v15 task 2 - Veteran gap fill, batch-4 prereq).

Terminal-run probes for one difficulty across all 20 levels, extending the
meta-0 sweep pattern (which covered Standard). Judgment data only from
full runs (victory/death), never time caps.

Usage:
  python tools/qa_difficulty_probe.py --difficulty Veteran
  python tools/qa_difficulty_probe.py --difficulty EmberTrial --levels 5 13

Run against a COMMITTED, refereed state only - probe data attaches to the
HEAD it was collected on (recorded in the summary).
"""

import argparse
import json
import os
import subprocess
import time

OUT = "output/playtest/difficulty_probes"
STRATEGY = "adaptive_network"


def run_one(level, difficulty, seed):
    tag = "L%02d_%s_s%d" % (level, difficulty[:3].lower(), seed)
    summary = "%s/%s.json" % (OUT, tag)
    p124 = "%s/%s.p124.json" % (OUT, tag)
    cmd = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
           "-File", "tools/td_mcp_playtest.ps1",
           "-LevelIndex", str(level),
           "-FormationDifficulty", difficulty,
           "-DurationSeconds", "200",
           "-TimeScale", "16",
           "-P124MaxRealSeconds", "170",
           "-P124AutoplayStrategy", STRATEGY,
           "-P124SiteVariant", "0",
           "-RandomSeed", str(seed),
           "-AllowConsoleIssues",
           "-P124RunReportPath", "E:/TD/" + p124,
           "-ScreenshotPath", "E:/TD/%s/%s.png" % (OUT, tag),
           "-SummaryPath", "E:/TD/" + summary]
    t0 = time.time()
    # per-run git state: if S4 lands mid-sweep the batch can be split
    # post-hoc and the delta re-run under the referee
    head = subprocess.run(["git", "rev-parse", "--short", "HEAD"],
                          cwd="E:/TD", capture_output=True, text=True).stdout.strip()
    dirty = bool(subprocess.run(["git", "status", "--porcelain", "--", "Assets/Scripts"],
                                cwd="E:/TD", capture_output=True).stdout.strip())
    try:
        subprocess.run(cmd, cwd="E:/TD", timeout=520,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except subprocess.TimeoutExpired:
        pass
    try:
        d = json.load(open(p124, encoding="utf-8-sig"))
    except (OSError, ValueError):
        d = {}
    rec = {"level": level, "difficulty": difficulty, "seed": seed,
           "head": head, "dirtyScripts": dirty,
           "victory": d.get("victory"), "waves": d.get("wavesCleared"),
           "towers": d.get("towersBuilt"), "elapsed": round(time.time() - t0)}
    print("L%02d %s V=%s waves=%s towers=%s [%ds]" % (
        level, difficulty, rec["victory"], rec["waves"], rec["towers"],
        rec["elapsed"]), flush=True)
    return rec


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--difficulty", required=True,
                    choices=["Standard", "Veteran", "EmberTrial"])
    ap.add_argument("--levels", nargs="+", type=int,
                    default=list(range(1, 21)))
    ap.add_argument("--seed", type=int, default=1337)
    args = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)

    rev = subprocess.run(["git", "rev-parse", "--short", "HEAD"],
                         cwd="E:/TD", capture_output=True, text=True).stdout.strip()
    dirty = bool(subprocess.run(["git", "status", "--porcelain",
                                 "--", "Assets/Scripts"],
                                cwd="E:/TD", capture_output=True).stdout.strip())
    if dirty:
        print("WARNING: Assets/Scripts has uncommitted changes - probe data "
              "would attach to a dirty tree. Referee first.", flush=True)

    results = []
    for level in args.levels:
        results.append(run_one(level, args.difficulty, args.seed))
        json.dump({"head": rev, "dirtyScripts": dirty,
                   "difficulty": args.difficulty, "seed": args.seed,
                   "runs": results},
                  open("%s/summary_%s.json" % (OUT, args.difficulty.lower()),
                       "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    wins = sum(1 for r in results if r["victory"])
    print("DONE %s: %d/%d wins (head=%s dirty=%s)" %
          (args.difficulty, wins, len(results), rev, dirty), flush=True)


if __name__ == "__main__":
    main()
