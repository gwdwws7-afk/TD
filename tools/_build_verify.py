"""Single-session release build verification: retry initialize, build, poll."""
import http.client, json, sys, time

def parse_sse(raw):
    for line in raw.splitlines():
        line = line.strip()
        if line.startswith("data: "):
            try:
                return json.loads(line[6:])
            except json.JSONDecodeError:
                continue
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        return {"raw": raw[:400]}

def connect_session():
    for attempt in range(15):
        try:
            conn = http.client.HTTPConnection("127.0.0.1", 8080, timeout=180)
            conn.request("POST", "/mcp", body=json.dumps({
                "jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
                    "protocolVersion": "2024-11-05", "capabilities": {},
                    "clientInfo": {"name": "build-verify", "version": "1"}}}).encode(),
                headers={"Content-Type": "application/json", "Accept": "application/json, text/event-stream"})
            resp = conn.getresponse()
            raw = resp.read().decode()
            sid = resp.getheader("Mcp-Session-Id") or resp.getheader("mcp-session-id")
            if resp.status == 200 and sid:
                conn.request("POST", "/mcp", body=json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}).encode(),
                             headers={"Content-Type": "application/json", "Accept": "application/json, text/event-stream", "Mcp-Session-Id": sid})
                conn.getresponse().read()
                print(f"session ok on attempt {attempt}: {sid[:8]}...")
                return conn, sid
            print(f"attempt {attempt}: status={resp.status} sid={sid} retrying")
        except Exception as e:
            print(f"attempt {attempt}: {type(e).__name__}")
        time.sleep(20)
    return None, None

def call(conn, sid, name, args, i):
    conn.request("POST", "/mcp", body=json.dumps({"jsonrpc": "2.0", "id": i, "method": "tools/call",
                                                  "params": {"name": name, "arguments": args}}).encode(),
                 headers={"Content-Type": "application/json", "Accept": "application/json, text/event-stream", "Mcp-Session-Id": sid})
    return parse_sse(conn.getresponse().read().decode())

def text_of(result):
    return " ".join(c.get("text", "") for c in result.get("result", {}).get("content", []))

conn, sid = connect_session()
if not conn:
    print("BRIDGE UNAVAILABLE")
    sys.exit(2)

r = call(conn, sid, "manage_build", {
    "action": "build", "development": "false",
    "output_path": "E:/TD/output/builds/p3_gating_verify_mono/EmberlineDefense.exe",
    "scenes": "Assets/Scenes/EmberlineBootstrap.unity",
    "target": "windows64"}, 2)
print("kick:", text_of(r)[:300])

deadline = time.time() + 420
i = 3
while time.time() < deadline:
    time.sleep(10)
    try:
        r = call(conn, sid, "manage_build", {"action": "status"}, i)
        i += 1
    except Exception as e:
        print("poll connection lost:", type(e).__name__)
        sys.exit(3)
    t = text_of(r)
    if "succeeded" in t.lower() or "failed" in t.lower():
        print("final:", t[:700])
        sys.exit(0 if "succeeded" in t.lower() and "failed" not in t.lower() else 1)
    print("...", t[:100])
print("TIMEOUT")
sys.exit(4)
