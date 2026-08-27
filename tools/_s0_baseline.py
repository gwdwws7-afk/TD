"""S0 baseline capture: autoplay trajectory archive across seeds.

Freeze-period S0 per gamemanager-split-tech-design.md — captures the
(wave, integrity) trajectory of autoplay runs across three seeds (two
runs each) into design/reviews/freeze-baseline-s0.md. Later split steps
compare against this with the multi-seed median rule (R7: single-wave
diffs are timing noise, not failures).
"""
import http.client, json, sys, time

def parse_sse(raw):
    for line in raw.splitlines():
        line = line.strip()
        if line.startswith("data: "):
            try: return json.loads(line[6:])
            except json.JSONDecodeError: continue
    try: return json.loads(raw)
    except json.JSONDecodeError: return {"raw": raw[:300]}

class Sess:
    def __init__(self):
        self.conn = http.client.HTTPConnection("127.0.0.1", 8080, timeout=180)
        self.conn.request("POST", "/mcp", body=json.dumps({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"s0","version":"1"}}}).encode(), headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream"})
        r = self.conn.getresponse()
        self.sid = r.getheader("Mcp-Session-Id")
        r.read()
        self.i = 2
    def call(self, name, args, tries=4):
        for t in range(tries):
            try:
                self.conn.request("POST", "/mcp", body=json.dumps({"jsonrpc":"2.0","id":self.i,"method":"tools/call","params":{"name":name,"arguments":args}}).encode(), headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream","Mcp-Session-Id":self.sid})
                self.i += 1
                return parse_sse(self.conn.getresponse().read().decode())
            except Exception:
                time.sleep(8)
                try:
                    self.conn = http.client.HTTPConnection("127.0.0.1", 8080, timeout=180)
                    self.conn.request("POST", "/mcp", body=json.dumps({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"s0","version":"1"}}}).encode(), headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream"})
                    r = self.conn.getresponse(); self.sid = r.getheader("Mcp-Session-Id"); r.read()
                except Exception:
                    time.sleep(10)
        return {}
    def text(self, res):
        return " ".join(c.get("text","") for c in res.get("result",{}).get("content",[]))

def stop_play(sess):
    try: sess.call("manage_editor", {"action":"stop"})
    except Exception: pass
    time.sleep(8)

def run_one(sess, seed, max_seconds=170):
    sess.call("manage_editor", {"action":"play"})
    time.sleep(12)
    r = sess.call("execute_code", {"action":"execute","code":
        f"UnityEngine.Random.InitState({seed});\n"
        "var m = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();\n"
        "if (m == null) return \"no-manager\";\n"
        "var d = m.DebugDeployCurrentMissionForTest();\n"
        "var a = m.DebugStartP124AutoplayForTest(\"adaptive_network\", 0, 600f);\n"
        "return d;"})
    kick = sess.text(r).strip()[:100]
    samples = []
    last_wave = -1
    deadline = time.time() + max_seconds
    while time.time() < deadline:
        time.sleep(7)
        try:
            r = sess.call("execute_code", {"action":"execute","code":
                "var m = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();\n"
                "if (m == null) return \"gone\";\n"
                "var s = m.DebugGetP1253RuntimeState();\n"
                "return s.currentWave + \",\" + s.integrity + \",\" + (s.gameOver ? \"OVER\" : (s.victory ? \"WIN\" : \"run\"));"})
            t = sess.text(r).strip().strip('"')
            if "OVER" in t or "WIN" in t or "gone" in t:
                samples.append(t)
                break
            parts = t.split(",")
            if len(parts) >= 3 and parts[0].isdigit():
                w = int(parts[0])
                if w != last_wave:
                    last_wave = w
                    samples.append(t)
        except Exception:
            pass
    stop_play(sess)
    return kick, samples

if __name__ == "__main__":
    sess = Sess()
    results = {}
    for seed in (42, 7, 2024):
        for run in ("A", "B"):
            kick, samples = run_one(sess, seed)
            final = samples[-1] if samples else "?"
            results[f"seed{seed}-{run}"] = {"final": final, "samples": samples}
            print(f"[seed {seed} {run}] final: {final}  ({len(samples)} samples)")
    with open("output/playtest/freeze_baseline_s0.json", "w", encoding="utf-8") as f:
        json.dump(results, f, indent=1, ensure_ascii=False)
    print("archived -> output/playtest/freeze_baseline_s0.json")
