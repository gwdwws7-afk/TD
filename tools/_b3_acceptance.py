# -*- coding: utf-8 -*-
"""Batch-3 acceptance battery (reweave-input §4): terminal runs only.

A-anchor: L01 seed 42 x2 — file untouched, must reproduce the recorded
anchor (category integrity-0 defeat, same-seed self-consistency).
B-core: L13 x3 strategies — the proven-unclearable level; Standard meta-0
terminal CLEAR is the acceptance.
B-spots: L10, L15 (adaptive).
C-smoke: L17 (adaptive) — boss functions, terminal state recorded.
"""
import http.client, json, time, sys

def parse_sse(raw):
    for line in raw.splitlines():
        line = line.strip()
        if line.startswith("data: "):
            try: return json.loads(line[6:])
            except json.JSONDecodeError: continue
    try: return json.loads(raw)
    except json.JSONDecodeError: return None

class Sess:
    def __init__(self):
        self.connect()
    def connect(self):
        self.conn = http.client.HTTPConnection("127.0.0.1", 8080, timeout=300)
        self.conn.request("POST", "/mcp", body=json.dumps({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"b3bat","version":"1"}}}).encode(), headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream"})
        r = self.conn.getresponse(); self.sid = r.getheader("Mcp-Session-Id"); r.read()
    def call(self, name, args, tries=8):
        for t in range(tries):
            try:
                self.conn.request("POST", "/mcp", body=json.dumps({"jsonrpc":"2.0","id":42,"method":"tools/call","params":{"name":name,"arguments":args}}).encode(), headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream","Mcp-Session-Id":self.sid})
                res = parse_sse(self.conn.getresponse().read().decode())
                if res is None:
                    time.sleep(5); self.connect(); continue
                for tx in (res.get("result") or {}).get("content") or []:
                    try: return json.loads(tx.get("text", "{}"))
                    except Exception: return {"raw": tx.get("text", "")[:200]}
                return {}
            except Exception:
                time.sleep(8)
                try: self.connect()
                except Exception: time.sleep(10)
        return {}

s = Sess()

def run_terminal(level, strategy, seed, max_seconds=420):
    s.call("manage_editor", {"action":"stop"})
    time.sleep(6)
    s.call("execute_code", {"action":"execute","code":f"TD.TDCampaignRouter.SaveLevelIndex({level}); return \"pinned\";"})
    s.call("manage_editor", {"action":"play"})
    time.sleep(14)
    r = s.call("execute_code", {"action":"execute","code":
        f"UnityEngine.Random.InitState({seed});\n"
        "var m = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();\n"
        "if (m == null) return \"no-manager\";\n"
        f"var d = m.DebugDeployCurrentMissionForTest();\n"
        f"var a = m.DebugStartP124AutoplayForTest(\"{strategy}\", 0, 900f);\n"
        "return d;"})
    terminal = None
    deadline = time.time() + max_seconds
    while time.time() < deadline:
        time.sleep(8)
        st = s.call("execute_code", {"action":"execute","code":
            "var m = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();\n"
            "if (m == null) return \"gone\";\n"
            "var st = m.DebugGetP1253RuntimeState();\n"
            "return st.currentWave + \",\" + st.integrity + \",\" + (st.gameOver ? (st.victory ? \"WIN\" : \"OVER\") : \"run\");"})
        txt = st.get("data", {}).get("result", "") if isinstance(st, dict) else ""
        if not isinstance(txt, str):
            txt = str(txt)
        if "WIN" in txt or "OVER" in txt or "gone" in txt:
            terminal = txt.strip().strip('"')
            break
    s.call("manage_editor", {"action":"stop"})
    time.sleep(4)
    return terminal or "TIMEOUT"

battery = [
    ("L01", 1, "adaptive_network", 42),
    ("L01", 1, "adaptive_network", 42),
    ("L13", 13, "adaptive_network", 42),
    ("L13", 13, "focused_fire", 42),
    ("L13", 13, "control_lattice", 42),
    ("L10", 10, "adaptive_network", 42),
    ("L15", 15, "adaptive_network", 42),
    ("L17", 17, "adaptive_network", 42),
]

results = {}
for label, level, strategy, seed in battery:
    key = f"{label}/{strategy}"
    if label == "L01":
        key = f"{label}/s{seed}/{strategy}"
    if key in results:
        continue
    out = run_terminal(level, strategy, seed)
    results[key] = out
    print(f"[{key}] {out}", flush=True)

print("---- SUMMARY ----")
for k, v in results.items():
    print(k, "=>", v)
