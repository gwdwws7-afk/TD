# -*- coding: utf-8 -*-
"""IL2CPP default-backend verification build (v19 task 2.2).

Fresh output dir, scripting_backend=il2cpp via manage_build, poll to
completion, then structural checks: GameAssembly.dll present (IL2CPP
marker), managed game assembly absent, gated symbol absent from the
metadata (spot check).
"""
import http.client, json, time, sys
from pathlib import Path

OUT = "E:/TD/output/builds/il2cpp_verify_20260903"

def parse_sse(raw):
    for line in raw.splitlines():
        line = line.strip()
        if line.startswith("data: "):
            try: return json.loads(line[6:])
            except json.JSONDecodeError: continue
    try: return json.loads(raw)
    except json.JSONDecodeError: return None

class S:
    def __init__(self): self.reconnect()
    def reconnect(self):
        self.conn = http.client.HTTPConnection("127.0.0.1", 8080, timeout=300)
        self.conn.request("POST", "/mcp", body=json.dumps({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"il2cpp","version":"1"}}}).encode(), headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream"})
        r = self.conn.getresponse(); self.sid = r.getheader("Mcp-Session-Id"); r.read()
    def call(self, name, args, tries=6):
        for t in range(tries):
            try:
                self.conn.request("POST", "/mcp", body=json.dumps({"jsonrpc":"2.0","id":50+t,"method":"tools/call","params":{"name":name,"arguments":args}}).encode(), headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream","Mcp-Session-Id":self.sid})
                res = parse_sse(self.conn.getresponse().read().decode())
                if isinstance(res, dict):
                    for tx in ((res.get("result") or {}).get("content") or []):
                        try: return json.loads(tx.get("text","{}"))
                        except Exception: pass
                time.sleep(6); self.reconnect()
            except Exception:
                time.sleep(8)
                try: self.reconnect()
                except Exception: time.sleep(12)
        return {}

s = S()
r = s.call("manage_build", {"action":"build", "development":"false",
    "scripting_backend":"il2cpp",
    "output_path": OUT + "/EmberlineDefense.exe",
    "scenes":"Assets/Scenes/EmberlineBootstrap.unity",
    "target":"windows64"})
print("kick:", json.dumps(r)[:200], flush=True)

deadline = time.time() + 1800
final = None
while time.time() < deadline:
    time.sleep(15)
    st = s.call("manage_build", {"action":"status"})
    t = json.dumps(st)
    if "succeeded" in t.lower():
        final = st; print("BUILD SUCCEEDED", flush=True); break
    if "failed" in t.lower() or "error" in t.lower():
        final = st; print("BUILD FAILED:", t[:400], flush=True); break
    print("...", t[:120], flush=True)

out = Path(OUT)
if final is None:
    print("TIMEOUT"); sys.exit(4)

files = sorted(str(p.name) for p in out.rglob("*") if p.is_file())
ga = any("GameAssembly" in f for f in files)
managed = any(f.startswith("Assembly-CSharp") for f in files)
print(f"files: {len(files)}; GameAssembly.dll: {ga}; Assembly-CSharp: {managed}")

# Gated-symbol spot check: automation probe names must not exist in the
# IL2CPP metadata (they compile away when the define is absent).
probe_found = False
for p in out.rglob("*"):
    if p.is_file() and p.suffix in ("", ".dat") and p.stat().st_size > 1_000_000:
        try:
            blob = p.read_bytes()
            if b"TDStandaloneSmokeProbe" in blob or b"DebugStartP124AutoplayForTest" in blob:
                probe_found = True
                print("LEAK in", p.name)
        except Exception:
            pass
print("gated symbols in metadata:", "FOUND (BAD)" if probe_found else "absent (good)")
sys.exit(0 if (ga and not probe_found) else 1)
