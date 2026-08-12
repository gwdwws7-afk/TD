"""Read current TDGameManager runtime state via MCP."""
import http.client
import json

HOST = "127.0.0.1"
PORT = 8080
PATH = "/mcp"
H = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}


def parse(raw):
    for line in raw.splitlines():
        line = line.strip()
        if line.startswith("data: "):
            try:
                return json.loads(line[6:])
            except json.JSONDecodeError:
                pass
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        return {"raw": raw[:500]}


def post(c, p, sid=None):
    h = dict(H)
    if sid:
        h["Mcp-Session-Id"] = sid
    c.request("POST", PATH, body=json.dumps(p).encode(), headers=h)
    r = c.getresponse()
    raw = r.read().decode()
    so = r.getheader("Mcp-Session-Id")
    c.close()
    return so, parse(raw)


def main():
    c = http.client.HTTPConnection(HOST, PORT, timeout=30)
    sid, _ = post(c, {
        "jsonrpc": "2.0", "id": 1, "method": "initialize",
        "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                   "clientInfo": {"name": "state", "version": "1"}},
    })
    post(c, {"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}, sid)

    code = (
        'var t = System.Type.GetType("TDGameManager, Assembly-CSharp");'
        ' var f = t.GetField("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);'
        ' var gm = f != null ? f.GetValue(null) : null;'
        ' if (gm == null) return "Instance=null";'
        ' var fields = new string[] {"_wave", "_gameOver", "_victory",'
        ' "_missionBoardOpen", "_formationPanelOpen", "_campaignProfileOpen",'
        ' "_defenseBudget", "_lineIntegrity", "_isInPrepPhase", "_prepCountdown",'
        ' "_builtTowerCount", "_campaignDeploymentConfirmed", "_waveStartRequested"};'
        ' var sb = new System.Text.StringBuilder();'
        ' foreach (var fn in fields) {'
        '   var fld = t.GetField(fn, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);'
        '   if (fld != null) sb.Append(fn).Append("=").Append(fld.GetValue(gm)).Append("\\n");'
        ' }'
        ' return sb.ToString();'
    )

    c2 = http.client.HTTPConnection(HOST, PORT, timeout=30)
    _, r = post(c2, {
        "jsonrpc": "2.0", "id": 2, "method": "tools/call",
        "params": {"name": "execute_code", "arguments": {"action": "execute", "code": code}},
    }, sid)

    content = r.get("result", {}).get("content", [])
    for item in content:
        txt = item.get("text", "")
        try:
            data = json.loads(txt)
            result = data.get("data", {}).get("result", "")
            print(result)
        except (json.JSONDecodeError, KeyError, TypeError):
            print("RAW:", txt[:600])


if __name__ == "__main__":
    main()
