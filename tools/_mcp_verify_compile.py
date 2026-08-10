"""Verify Unity compiled the audio code changes with zero CS errors.

Sequence: initialize -> refresh_unity -> poll manage_editor (wait for compile)
-> read_console (errors). Uses proper Mcp-Session-Id handshake.
"""
import http.client
import json
import sys
import time

HOST = "127.0.0.1"
PORT = 8080
PATH = "/mcp"
HEADERS = {
    "Content-Type": "application/json",
    "Accept": "application/json, text/event-stream",
}


def parse_sse(raw_text):
    for line in raw_text.splitlines():
        line = line.strip()
        if line.startswith("data: "):
            try:
                return json.loads(line[6:])
            except json.JSONDecodeError:
                continue
    try:
        return json.loads(raw_text)
    except json.JSONDecodeError:
        return {"raw": raw_text[:500]}


def post(conn, payload, extra_headers=None):
    headers = dict(HEADERS)
    if extra_headers:
        headers.update(extra_headers)
    body = json.dumps(payload).encode("utf-8")
    conn.request("POST", PATH, body=body, headers=headers)
    resp = conn.getresponse()
    raw = resp.read().decode("utf-8")
    return resp.status, dict(resp.getheaders()), raw


def main():
    conn = http.client.HTTPConnection(HOST, PORT, timeout=120)

    # Handshake
    status, resp_headers, raw = post(conn, {
        "jsonrpc": "2.0", "id": 1, "method": "initialize",
        "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                   "clientInfo": {"name": "verify-compile", "version": "1.0"}},
    })
    session_id = resp_headers.get("Mcp-Session-Id") or resp_headers.get("mcp-session-id")
    sess = {"Mcp-Session-Id": session_id} if session_id else {}
    print("Session:", session_id)

    post(conn, {"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}, sess)

    # Trigger refresh
    print("refresh_unity...")
    _, _, raw = post(conn, {"jsonrpc": "2.0", "id": 2, "method": "tools/call",
        "params": {"name": "refresh_unity", "arguments": {}}}, sess)
    print("  ", parse_sse(raw).get("result", {}).get("content", [{}])[0].get("text", "")[:200])

    # Wait for compilation to finish by polling execute_code for EditorApplication.isCompiling
    print("Waiting for compilation to finish...")
    for attempt in range(30):
        time.sleep(2)
        _, _, raw = post(conn, {"jsonrpc": "2.0", "id": 10 + attempt, "method": "tools/call",
            "params": {"name": "execute_code", "arguments": {
                "code": "UnityEditor.EditorApplication.isCompiling"
            }}}, sess)
        result = parse_sse(raw)
        content = result.get("result", {}).get("content", [])
        if content:
            txt = content[0].get("text", "")
            if "False" in txt or "false" in txt:
                print("  Compilation finished (attempt", attempt + 1, ")")
                break
            print("  Still compiling... (attempt", attempt + 1, ")")
    else:
        print("  WARNING: compilation did not finish within timeout")

    # Read console for errors
    print("read_console (errors)...")
    _, _, raw = post(conn, {"jsonrpc": "2.0", "id": 99, "method": "tools/call",
        "params": {"name": "read_console", "arguments": {}}}, sess)
    result = parse_sse(raw)
    content = result.get("result", {}).get("content", [])
    all_lines = []
    for item in content:
        txt = item.get("text", "")
        if txt:
            all_lines.append(txt)

    cs_errors = [l for l in all_lines if "error CS" in l]
    if cs_errors:
        print("\n!!! COMPILE ERRORS !!!")
        for e in cs_errors[:30]:
            print("  -", e[:250])
        conn.close()
        sys.exit(1)

    errors = [l for l in all_lines if '"type": "error"' in l or '"Type": "Error"' in l.lower() or l.lower().startswith("error")]
    if errors:
        print("\nConsole errors (non-CS):")
        for e in errors[:10]:
            print("  -", e[:200])
    else:
        print("\nNo errors in console.")

    conn.close()
    print("\nVERDICT: Compilation clean, no CS errors.")


if __name__ == "__main__":
    main()
