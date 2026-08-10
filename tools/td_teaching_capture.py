"""Capture teaching screenshots during a single P124 autoplay session.

Enters play mode once, starts P124 autoplay on a representative level,
and captures screenshots at staged intervals. Much faster than running
the full playtest script per phase.
"""
import http.client
import json
import os
import sys
import time

HOST = "127.0.0.1"
PORT = 8080
PATH = "/mcp"
HEADERS = {
    "Content-Type": "application/json",
    "Accept": "application/json, text/event-stream",
}
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "output", "playtest", "teaching_shots")


def parse_sse(raw):
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


class Client:
    def __init__(self):
        self.conn = None
        self.sid = None
        self.id = 0
        self._connect()

    def _connect(self):
        try:
            if self.conn:
                self.conn.close()
        except Exception:
            pass
        self.conn = http.client.HTTPConnection(HOST, PORT, timeout=120)

    def call(self, method, params=None):
        self.id += 1
        payload = {"jsonrpc": "2.0", "id": self.id, "method": method}
        if params is not None:
            payload["params"] = params
        headers = dict(HEADERS)
        if self.sid:
            headers["Mcp-Session-Id"] = self.sid

        # Retry with reconnect on connection errors (domain reload kills the socket)
        for attempt in range(5):
            try:
                self.conn.request("POST", PATH, body=json.dumps(payload).encode(), headers=headers)
                resp = self.conn.getresponse()
                raw = resp.read().decode()
                if not self.sid:
                    self.sid = resp.getheader("Mcp-Session-Id")
                return parse_sse(raw)
            except (ConnectionAbortedError, ConnectionResetError, OSError, http.client.HTTPException):
                if attempt < 4:
                    time.sleep(3)
                    self._connect()
                    # Re-initialize if we lost the session
                    if attempt == 0:
                        self.sid = None
                        self._reinit()
                else:
                    raise

    def _reinit(self):
        """Re-initialize the MCP session after a domain reload."""
        try:
            self.call("initialize", {
                "protocolVersion": "2024-11-05", "capabilities": {},
                "clientInfo": {"name": "teaching-capture", "version": "1.0"},
            })
            self.call("notifications/initialized", {})
        except Exception:
            pass

    def tool(self, name, args=None):
        return self.call("tools/call", {"name": name, "arguments": args or {}})

    def execute(self, code):
        return self.tool("execute_code", {"action": "execute", "code": code})

    def manage_editor(self, action):
        return self.tool("manage_editor", {"action": action})

    def screenshot(self, path):
        p = path.replace("\\", "/")
        return self.execute(
            'UnityEngine.ScreenCapture.CaptureScreenshot("%s"); "ok"' % p
        )

    def close(self):
        try:
            self.conn.close()
        except Exception:
            pass


def main():
    os.makedirs(OUT, exist_ok=True)
    c = Client()

    print("1. Initialize...")
    c.call("initialize", {
        "protocolVersion": "2024-11-05", "capabilities": {},
        "clientInfo": {"name": "teaching-capture", "version": "1.0"},
    })
    c.call("notifications/initialized", {})

    # Check current state
    print("2. Check play mode...")
    r = c.execute(
        'return "isPlaying=" + UnityEditor.EditorApplication.isPlaying'
        + " + \" scene=\" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;"
    )
    state = ""
    for item in r.get("result", {}).get("content", []):
        state = item.get("text", "")
    print("   State:", state[:200])

    # Set level to L09 BEFORE entering play mode (SaveLevelIndex persists to PlayerPrefs)
    print("3. Setting level 9...")
    c.execute(
        'var t = System.Type.GetType("TDCampaignRouter, Assembly-CSharp");'
        ' var m = t.GetMethod("SaveLevelIndex");'
        ' m.Invoke(null, new object[]{9});'
        ' return "level_set";'
    )
    time.sleep(1)

    # Enter play mode (this causes domain reload + scene load)
    print("4. Entering play mode (expecting disconnect+reconnect)...")
    try:
        c.manage_editor("play")
    except Exception:
        pass  # Expected: domain reload kills the connection
    time.sleep(20)  # wait for scene load + MCP server restart

    # Reconnect with fresh session
    print("5. Reconnecting after domain reload...")
    c._connect()
    c.sid = None
    c._reinit()
    time.sleep(2)

    # Phase 1: Mission board / prep state
    print("6. Capture: prep/mission board state...")
    c.screenshot(os.path.join(OUT, "01_prep_board.png"))
    time.sleep(2)

    # Start P124 autoplay
    print("7. Starting P124 autoplay...")
    c.execute(
        'var t = System.Type.GetType("TDGameManager, Assembly-CSharp");'
        ' var f = t.GetField("Instance", System.Reflection.BindingFlags.Public'
        ' | System.Reflection.BindingFlags.Static);'
        ' var gm = f.GetValue(null);'
        ' var m = t.GetMethod("DebugStartP124AutoplayForTest",'
        ' System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);'
        ' m.Invoke(gm, new object[]{"focused_fire", 0, 80f});'
        ' return "autoplay_started";'
    )

    # Phase 2: Early combat (wave 1-3)
    print("8. Capture: early combat (8s)...")
    time.sleep(8)
    c.screenshot(os.path.join(OUT, "02_combat_early.png"))
    time.sleep(1)

    # Phase 3: Mid combat (wave 5-8)
    print("9. Capture: mid combat (20s)...")
    time.sleep(12)
    c.screenshot(os.path.join(OUT, "03_combat_mid.png"))
    time.sleep(1)

    # Phase 4: Late combat (wave 10+)
    print("10. Capture: late combat (35s)...")
    time.sleep(15)
    c.screenshot(os.path.join(OUT, "04_combat_late.png"))
    time.sleep(1)

    # Phase 5: Final state
    print("11. Capture: final state (50s)...")
    time.sleep(15)
    c.screenshot(os.path.join(OUT, "05_final_state.png"))
    time.sleep(1)

    # Read wave stats
    print("12. Reading wave stats...")
    r = c.tool("read_console", {})
    content = r.get("result", {}).get("content", [])
    all_lines = []
    for item in content:
        t = item.get("text", "")
        if t:
            all_lines.append(t)

    wave_lines = [l for l in all_lines if "WaveStat" in l]
    errors = [l for l in all_lines if "Exception" in l or "NullReference" in l]
    print("   WaveStat lines: %d" % len(wave_lines))
    print("   Errors: %d" % len(errors))
    if wave_lines:
        print("   Last wave:", wave_lines[-1][:200])

    # Stop play mode
    print("13. Stopping play mode...")
    c.manage_editor("stop")
    time.sleep(3)

    c.close()

    # List captured screenshots
    print("\n=== Captured screenshots ===")
    for name in sorted(os.listdir(OUT)):
        if name.endswith(".png"):
            path = os.path.join(OUT, name)
            size = os.path.getsize(path)
            print("  %s: %.1fMB" % (name, size / 1048576))


if __name__ == "__main__":
    main()
