"""Read Unity console via MCP bridge; exit 1 on CS errors."""
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

conn = http.client.HTTPConnection("127.0.0.1", 8080, timeout=120)
_, h, raw = post(conn, {"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
    "protocolVersion": "2024-11-05", "capabilities": {},
    "clientInfo": {"name": "console-check", "version": "1"}}})
sid = h.get("Mcp-Session-Id") or h.get("mcp-session-id")
post(conn, {"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}, sid)

print("Waiting for editor to finish compile...")
time.sleep(20)
result = call(conn, sid, "read_console", {"action": "get", "count": 50, "types": ["error", "warning"]}, 2)
content = result.get("result", {}).get("content", [])
lines = [c.get("text", "") for c in content if c.get("text")]
cs = [l for l in lines if "error CS" in l]
print(f"entries={len(lines)} cs_errors={len(cs)}")
for l in lines[:10]:
    print(" -", l[:160])
sys.exit(1 if cs else 0)
