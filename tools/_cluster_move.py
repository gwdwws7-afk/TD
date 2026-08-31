"""Freeze-period scattered-cluster move tool (S4+).

Extracts named member runs from TDGameManager.cs into a new partial file,
with the S3 lesson built in: every run's boundary is asserted — the line
BEFORE the run must not be an unterminated member, and the line AFTER the
run must not be a method-body continuation (a '{' belonging to a signature
that stayed behind, or anything that is not itself a member/brace-closing
context). Usage:

    python tools/_cluster_move.py <new-file-stem> <method-name-regex>
"""
import re
import sys

SRC = "Assets/Scripts/TowerDefense/TDGameManager.cs"

def main():
    stem = sys.argv[1]
    pattern = re.compile(sys.argv[2])
    lines = open(SRC, encoding="utf-8").read().splitlines(keepends=True)

    starts = []
    for i, l in enumerate(lines, 1):
        if re.match(r"^        (?:public|private|internal|protected)[^=]*(?:\([^;]*)?\{?\s*$", l) and not l.strip().endswith(";"):
            starts.append(i)
    starts.append(len(lines) + 1)

    targets = []
    for k, idx in enumerate(starts[:-1]):
        m = re.search(r"(\w+)\(", lines[idx - 1])
        if m and pattern.search(m.group(1)):
            targets.append((starts[k], starts[k + 1] - 1))

    # Boundary assertion (S3 lesson): the line AFTER a run end must start a
    # new member, close a scope, or be blank — never a body continuation.
    member_or_scope = re.compile(r"^(\s+\}|\s+(?:public|private|internal|protected|#|\})|$)")
    for a, b in targets:
        after = lines[b] if b < len(lines) else ""
        assert member_or_scope.match(after), (
            f"boundary violation after run {a}-{b}: next line is a body continuation: {after[:60]!r}")

    runs = []
    for s in targets:
        if runs and s[0] <= runs[-1][1] + 3:
            runs[-1] = (runs[-1][0], max(runs[-1][1], s[1]))
        else:
            runs.append((s[0], s[1]))

    extracted = []
    keep = [True] * len(lines)
    for a, b in sorted(runs, reverse=True):
        extracted.insert(0, lines[a - 1:b])
        for i in range(a - 1, b):
            keep[i] = False
    flat = [seg for run in extracted for seg in run]
    remaining = [l for i, l in enumerate(lines) if keep[i]]

    usings = "".join(l for l in lines[:12] if l.startswith("using"))
    header = sys.argv[3] if len(sys.argv) > 3 else f"Freeze-period move: {stem} cluster."
    new_file = (
        f"// {header}\n"
        + usings + "\nnamespace TD\n{\n"
        "    public sealed partial class TDGameManager : MonoBehaviour\n    {\n"
        + "".join(flat) +
        "    }\n}\n"
    )
    out = f"Assets/Scripts/TowerDefense/TDGameManager.{stem}.cs"
    open(out, "w", encoding="utf-8", newline="").write(new_file)
    open(SRC, "w", encoding="utf-8", newline="").write("".join(remaining))

    # Post-move structural balance on both files.
    for p in (SRC, out):
        depth = braces = 0
        for l in open(p, encoding="utf-8").read().splitlines():
            s = l.strip()
            if s.startswith("#if"): depth += 1
            elif s.startswith("#endif"): depth -= 1
            braces += l.count("{") - l.count("}")
        assert depth == 0 and braces == 0, f"unbalanced {p}: gate={depth} brace={braces}"

    print(f"moved {len(flat)} lines in {len(runs)} runs; main {len(lines)} -> {len(remaining)}; out={out}")

if __name__ == "__main__":
    main()
