"""Minimal MCP Streamable HTTP client to trigger Unity recompile and check errors.

Implements the session handshake: initialize -> capture Mcp-Session-Id ->
notifications/initialized -> tool calls (refresh_unity, read_console).
"""
import http.client
import json
import sys

HOST = "127.0.0.1"
PORT = 8080
PATH = "/mcp"
HEADERS = {
    "Content-Type": "application/json",
    "Accept": "application/json, text/event-stream",
}


def parse_sse(raw_text):
    """Extract the first JSON-RPC result from an SSE response body."""
    for line in raw_text.splitlines():
        line = line.strip()
        if line.startswith("data: "):
            try:
                return json.loads(line[6:])
            except json.JSONDecodeError:
                continue
    # Maybe it's plain JSON (not SSE).
    try:
        return json.loads(raw_text)
    except json.JSONDecodeError:
        return {"raw": raw_text[:500]}


def post(conn, payload, extra_headers=None):
    """POST a JSON-RPC payload and return (parsed_body, response_headers)."""
    headers = dict(HEADERS)
    if extra_headers:
        headers.update(extra_headers)
    body = json.dumps(payload).encode("utf-8")
    conn.request("POST", PATH, body=body, headers=headers)
    resp = conn.getresponse()
    raw = resp.read().decode("utf-8")
    return resp.status, dict(resp.getheaders()), raw


def main():
    conn = http.client.HTTPConnection(HOST, PORT, timeout=60)

    print("1. Initialize...")
    status, resp_headers, raw = post(conn, {
        "jsonrpc": "2.0", "id": 1, "method": "initialize",
        "params": {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "compile-check", "version": "1.0"},
        },
    })
    session_id = resp_headers.get("Mcp-Session-Id") or resp_headers.get("mcp-session-id")
    if not session_id:
        print("   WARNING: no Mcp-Session-Id in response headers")
        print("   headers:", resp_headers)
    else:
        print("   Session:", session_id)

    init_result = parse_sse(raw)
    server_info = init_result.get("result", {}).get("serverInfo", {})
    print("   Server:", server_info)

    sess_headers = {"Mcp-Session-Id": session_id} if session_id else {}

    print("2. Send initialized notification...")
    status, _, raw = post(conn, {
        "jsonrpc": "2.0", "method": "notifications/initialized", "params": {},
    }, extra_headers=sess_headers)
    print("   Status:", status)

    print("3. Call refresh_unity...")
    status, _, raw = post(conn, {
        "jsonrpc": "2.0", "id": 2, "method": "tools/call",
        "params": {"name": "refresh_unity", "arguments": {}},
    }, extra_headers=sess_headers)
    result = parse_sse(raw)
    content = result.get("result", {}).get("content", [])
    if content:
        for item in content:
            print("   ", item.get("text", "")[:300])
    elif result.get("error"):
        print("   ERROR:", result["error"])
    else:
        print("   (status", status, ")", raw[:200])

    print("4. Read Unity console (errors only)...")
    status, _, raw = post(conn, {
        "jsonrpc": "2.0", "id": 3, "method": "tools/call",
        "params": {"name": "read_console",
                   "arguments": {"includeStackTrace": False}},
    }, extra_headers=sess_headers)
    result = parse_sse(raw)
    content = result.get("result", {}).get("content", [])
    all_lines = []
    for item in content:
        txt = item.get("text", "")
        if txt:
            all_lines.append(txt)

    # Filter for compile errors (CS####) and Error type entries.
    cs_errors = [l for l in all_lines if "CS" in l and "error" in l.lower()]
    if cs_errors:
        print("\n   !!! COMPILE ERRORS DETECTED !!!")
        for e in cs_errors[:30]:
            print("   -", e[:200])
        conn.close()
        sys.exit(1)
    elif all_lines:
        print("   Console has", len(all_lines), "entries (none are CS compile errors).")
        for l in all_lines[:5]:
            print("   -", l[:150])
    else:
        print("   Console is clean (no entries).")

    conn.close()
    print("\nRESULT: refresh triggered, no CS compile errors in console.")


if __name__ == "__main__":
    main()
