# -*- coding: utf-8 -*-
"""Post-tuning verification: L01-A anchor rerun + L13 control + L13 adaptive."""
import http.client, json, time

def post(body, sid=None):
    c = http.client.HTTPConnection("127.0.0.1", 8080, timeout=300)
    h = {"Content-Type":"application/json","Accept":"application/json, text/event-stream"}
    if sid: h["Mcp-Session-Id"] = sid
    c.request("POST", "/mcp", body=json.dumps(body).encode(), headers=h)
    r = c.getresponse(); sid_out = r.getheader("Mcp-Session-Id"); raw = r.read().decode(); c.close()
    for line in raw.splitlines():
        line = line.strip()
        if line.startswith("data: "):
            try: return json.loads(line[6:]), sid_out
            except Exception: pass
    try: return json.loads(raw), sid_out
    except Exception: return None, sid_out

class S:
    def __init__(self):
        self.reconnect()
    def reconnect(self):
        _, self.sid = post({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"vrf","version":"1"}}})
    def call(self, name, args, tries=8):
        for attempt in range(tries):
            try:
                res, _ = post({"jsonrpc":"2.0","id":800+attempt,"method":"tools/call","params":{"name":name,"arguments":args}}, self.sid)
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

def run_terminal(level, strategy, seed=42, max_seconds=920, tag=""):
    s.call("manage_editor", {"action":"stop"})
    time.sleep(8)
    s.call("execute_code", {"action":"execute","code":f"TD.TDCampaignRouter.SaveLevelIndex({level}); return \"pin\";"})
    s.call("manage_editor", {"action":"play"})
    time.sleep(16)
    r = s.call("execute_code", {"action":"execute","code":
        f"UnityEngine.Random.InitState({seed});\n"
        "var m = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();\n"
        "if (m == null) return \"no-manager\";\n"
        "return m.DebugDeployCurrentMissionForTest() + \" | \" + m.DebugStartP124AutoplayForTest(\"" + strategy + "\", 0, 1200f);"})
    dep = str((r.get("data") or {}).get("result",""))
    if "deployed level" not in dep:
        print(f"[{tag}] DEPLOY-FAIL: {dep[:100]}", flush=True)
        return None
    last = "?"
    deadline = time.time() + max_seconds
    while time.time() < deadline:
        time.sleep(10)
        st = s.call("execute_code", {"action":"execute","code":
            "var m = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();\n"
            "if (m == null) return \"gone\";\n"
            "var st = m.DebugGetP1253RuntimeState();\n"
            "var t = UnityEngine.Object.FindObjectsByType<TD.TDTower>(UnityEngine.FindObjectsSortMode.None).Length;\n"
            "return st.currentWave + \",\" + st.integrity + \",\" + t + \"t,\" + (st.gameOver ? (st.victory ? \"WIN\" : \"OVER\") : \"run\");"})
        last = str((st.get("data") or {}).get("result",""))
        if "WIN" in last or "OVER" in last or "gone" in last:
            break
    print(f"[{tag}] terminal: {last}", flush=True)
    return last

# L13 file changed -> ensure a fresh asset read; run order: control (the
# failing strategy), adaptive re-verify, L01-A anchor completion.
run_terminal(13, "control_lattice", 42, 920, "L13-control(tuned)")
run_terminal(13, "adaptive_network", 42, 920, "L13-adaptive(tuned)")
run_terminal(1, "adaptive_network", 42, 300, "L01-A(anchor-pair)")
