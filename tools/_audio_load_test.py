"""Test that audio clips load correctly via Resources.Load in Unity."""
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
    sid, r = post(c, {
        "jsonrpc": "2.0", "id": 1, "method": "initialize",
        "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                   "clientInfo": {"name": "audio-load-test", "version": "1"}},
    })
    post(c, {"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}, sid)

    code = (
        'var sb = new System.Text.StringBuilder();'
        'var paths = new string[] {'
        '  "Audio/SFX/Tower/fire_rail_lancer",'
        '  "Audio/SFX/Hit/routine_hit",'
        '  "Audio/SFX/UI/tower_place",'
        '  "Audio/Music/menu_theme",'
        '  "Audio/Music/combat_chapter_a",'
        '  "Audio/Ambience/grayline_junction",'
        '  "Audio/SFX/Enemy/death_generic",'
        '  "Audio/SFX/Resonance/window_open",'
        '  "Audio/SFX/Status/specialization_ult",'
        '  "Audio/SFX/Scenario/route_switch",'
        '};'
        'foreach (var p in paths) {'
        '  var clip = UnityEngine.Resources.Load<UnityEngine.AudioClip>(p);'
        '  sb.Append(p).Append(": ").Append(clip != null'
        '    ? "OK " + clip.length.ToString("F2") + "s"'
        '    : "MISSING").Append("\\n");'
        '}'
        'return sb.ToString();'
    )

    c2 = http.client.HTTPConnection(HOST, PORT, timeout=30)
    _, r = post(c2, {
        "jsonrpc": "2.0", "id": 2, "method": "tools/call",
        "params": {"name": "execute_code",
                   "arguments": {"action": "execute", "code": code}},
    }, sid)

    content = r.get("result", {}).get("content", [])
    for item in content:
        txt = item.get("text", "")
        try:
            data = json.loads(txt)
            result = data.get("data", {}).get("result", txt)
            print(result)
        except (json.JSONDecodeError, AttributeError):
            print(txt[:800])


if __name__ == "__main__":
    main()
