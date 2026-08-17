#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Symbol audit for a built player package (task 4, plan v3 2026-08-17).

Scans the managed assemblies of a Windows/Mono player build for
automation-only and gameplay symbol names and prints a verdict:
automation symbols must be ABSENT from a pure (-tdAutomation false)
package, gameplay symbols must be PRESENT.

Usage: python tools/qa_pure_package_audit.py [buildRoot]
       buildRoot defaults to builds/qa-pure-20260817
"""

import os
import sys

AUTOMATION_SYMBOLS = [
    "DebugStartP124AutoplayForTest",
    "DebugPrepareP122ExamForTest",
    "DebugPrepareP123SettingsForTest",
    "DebugAuditP111ForTest",
    "DebugUnlockThroughLevelForTest",
    "TDP1254StandaloneProbe",
    "TDStandaloneSmokeProbe",
    "TDP1254SoakRuntimeState",
]

GAMEPLAY_SYMBOLS = [
    "TDGameManager",
    "TrySellTower",
    "SellRefundValue",
    "GamepadCursor",
    "TDCombatMath",
    "ResolveArmoredDamage",
    "TDRadialTowerMenu",
]

DEFAULT_ROOT = "builds/qa-pure-20260817"


def find_managed(root):
    hits = []
    for base, _, files in os.walk(root):
        if os.path.basename(base).lower() == "managed":
            for f in files:
                if f.lower().endswith(".dll"):
                    hits.append(os.path.join(base, f))
    return hits


def contains(name_bytes, symbol):
    """Search UTF-8 and UTF-16LE encodings in raw assembly bytes."""
    return (symbol.encode("utf-8") in name_bytes or
            symbol.encode("utf-16-le") in name_bytes)


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_ROOT
    dlls = find_managed(root)
    if not dlls:
        print("NO managed assemblies under", root)
        return 2
    print("scanning %d assemblies under %s" % (len(dlls), root))

    blob = bytearray()
    for dll in dlls:
        with open(dll, "rb") as f:
            blob.extend(f.read())

    leaked = [s for s in AUTOMATION_SYMBOLS if contains(blob, s)]
    missing = [s for s in GAMEPLAY_SYMBOLS if not contains(blob, s)]
    for s in AUTOMATION_SYMBOLS:
        print("  automation %-34s %s" % (s, "PRESENT (LEAK)" if s in leaked else "absent ok"))
    for s in GAMEPLAY_SYMBOLS:
        print("  gameplay   %-34s %s" % (s, "absent (MISSING)" if s in missing else "present ok"))

    pure = not leaked and not missing
    print("VERDICT:", "PURE PASS" if pure else "FAIL")
    return 0 if pure else 1


if __name__ == "__main__":
    sys.exit(main())
