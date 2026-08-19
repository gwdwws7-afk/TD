"""Deterministic autoplay A/B runner for TD-WINDUP-001.

Enters play mode, seeds Random(42), starts P124 autoplay, samples
(wave, integrity) until defeat/victory or timeout. Prints the series.
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
        self.conn.request("POST", "/mcp", body=json.dumps({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"ab","version":"1"}}}).encode(), headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream"})
        r = self.conn.getresponse()
        self.sid = r.getheader("Mcp-Session-Id")
        r.read()
        self.i = 2
    def call(self, name, args):
        self.conn.request("POST", "/mcp", body=json.dumps({"jsonrpc":"2.0","id":self.i,"method":"tools/call","params":{"name":name,"arguments":args}}).encode(), headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream","Mcp-Session-Id":self.sid})
        self.i += 1
        return parse_sse(self.conn.getresponse().read().decode())
    def text(self, res):
        return " ".join(c.get("text","") for c in res.get("result",{}).get("content",[]))

def robust_call(sess, name, args, tries=6):
    last = None
    for t in range(tries):
        try:
            return sess.call(name, args)
        except Exception as e:
            last = e
            time.sleep(8)
            try:
                sess.conn = http.client.HTTPConnection("127.0.0.1", 8080, timeout=180)
                sess.conn.request("POST", "/mcp", body=json.dumps({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"ab","version":"1"}}}).encode(), headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream"})
                r = sess.conn.getresponse(); sess.sid = r.getheader("Mcp-Session-Id"); r.read()
            except Exception:
                time.sleep(10)
    raise last

def wait_not_playing(sess, timeout=180):
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            r = sess.call("manage_editor", {"action":"stop"})
            t = sess.text(r)
            if "not in play mode" in t.lower():
                return
        except Exception:
            pass
        time.sleep(6)

def run_series(label, max_seconds=200):
    sess = Sess()
    robust_call(sess, "manage_editor", {"action":"play"})
    time.sleep(12)
    r = robust_call(sess, "execute_code", {"action":"execute","code":
        "UnityEngine.Random.InitState(42);\n"
        "var manager = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();\n"
        "if (manager == null) return \"no-manager\";\n"
        "var deploy = manager.DebugDeployCurrentMissionForTest();\n"
        "var auto = manager.DebugStartP124AutoplayForTest(\"adaptive_network\", 0, 600f);\n"
        "return \"seed=42 deploy=\" + deploy + \" auto=\" + auto;"})
    print(f"[{label}] kick:", sess.text(r)[:220])
    series = []
    deadline = time.time() + max_seconds
    last_wave = -1
    while time.time() < deadline:
        time.sleep(6)
        try:
            r = sess.call("execute_code", {"action":"execute","code":
                "var m = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();\n"
                "if (m == null) return \"gone\";\n"
                "var s = m.DebugGetP1253RuntimeState();\n"
                "return s.currentWave + \",\" + s.integrity + \",\" + (s.gameOver ? \"OVER\" : (s.victory ? \"WIN\" : \"run\"));"})
            t = sess.text(r).strip().strip('"')
            if "OVER" in t or "WIN" in t or "gone" in t:
                series.append(t)
                print(f"[{label}] final: {t}")
                break
            parts = t.split(",")
            if len(parts) >= 3:
                w = int(parts[0])
                if w != last_wave:
                    last_wave = w
                    series.append(t)
                    print(f"[{label}] {t}")
        except Exception:
            pass
    wait_not_playing(sess)
    return series

if __name__ == "__main__":
    label = sys.argv[1] if len(sys.argv) > 1 else "run"
    run_series(label)
