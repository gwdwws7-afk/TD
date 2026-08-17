"""Trigger a Windows release build via the Unity MCP bridge and poll status."""
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
        return {"raw": raw[:500]}

def post(conn, payload, sid=None):
    headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
    if sid:
        headers["Mcp-Session-Id"] = sid
    conn.request("POST", "/mcp", body=json.dumps(payload).encode(), headers=headers)
    resp = conn.getresponse()
    return resp.status, dict(resp.getheaders()), resp.read().decode()

def call(conn, sid, name, args, i):
    status, _, raw = post(conn, {"jsonrpc": "2.0", "id": i, "method": "tools/call",
                                 "params": {"name": name, "arguments": args}}, sid)
    return parse_sse(raw)

def text_of(result):
    return " ".join(c.get("text", "") for c in result.get("result", {}).get("content", []))

conn = http.client.HTTPConnection("127.0.0.1", 8080, timeout=180)
_, h, raw = post(conn, {"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
    "protocolVersion": "2024-11-05", "capabilities": {},
    "clientInfo": {"name": "build-verify", "version": "1"}}})
sid = h.get("Mcp-Session-Id") or h.get("mcp-session-id")
post(conn, {"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}, sid)

r = call(conn, sid, "manage_build", {
    "action": "build", "development": False,
    "output_path": r"E:\TD\output\builds\p3_gating_verify\EmberlineDefense.exe",
    "scenes": "Assets/Scenes/EmberlineBootstrap.unity",
    "target": "windows64"}, 2)
print("kick:", text_of(r)[:300])

deadline = time.time() + 420
while time.time() < deadline:
    time.sleep(10)
    r = call(conn, sid, "manage_build", {"action": "status"}, 3)
    t = text_of(r)
    if "succeeded" in t.lower() or "failed" in t.lower():
        print("final:", t[:600])
        sys.exit(0 if "succeeded" in t.lower() and "failed" not in t.lower() else 1)
    print("...", t[:120])
print("TIMEOUT waiting for build")
sys.exit(2)
