"""Full-campaign automated run with staged screenshots and issue detection.

Runs 5 representative levels (L01/L05/L09/L13/L20) via P124 autoplay,
capturing screenshots at key phases (prep, early, mid, late, result),
reading the console for errors, and collecting WaveStat telemetry.

Usage: .venv/Scripts/python.exe tools/td_full_run_screenshots.py [--passes N]
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
OUTPUT_DIR = os.path.join(ROOT, "output", "playtest", "full_run_5pass")
LEVELS = [1, 5, 9, 13, 20]
LEVEL_NAMES = {
    1: "L01_tutorial",
    5: "L05_first_boss",
    9: "L09_midgame",
    13: "L13_pressure",
    20: "L20_finale",
}
STRATEGIES = ["focused_fire", "control_lattice", "adaptive_network"]
# Phase capture timing (seconds of real wall-clock at 16x time scale)
PHASES = [
    ("prep", 3),      # mission board / prep phase
    ("early", 12),    # first few waves
    ("mid", 35),      # mid-game
    ("late", 65),     # late game
    ("result", 90),   # result / final state
]


def parse_sse(raw_text):
    """Extract all JSON-RPC results from an SSE response."""
    results = []
    for line in raw_text.splitlines():
        line = line.strip()
        if line.startswith("data: "):
            try:
                results.append(json.loads(line[6:]))
            except json.JSONDecodeError:
                continue
    if not results:
        try:
            results.append(json.loads(raw_text))
        except json.JSONDecodeError:
            pass
    return results


class McpClient:
    def __init__(self):
        self.conn = http.client.HTTPConnection(HOST, PORT, timeout=120)
        self.session_id = None
        self.msg_id = 0

    def _next_id(self):
        self.msg_id += 1
        return self.msg_id

    def call(self, method, params=None):
        payload = {"jsonrpc": "2.0", "id": self._next_id(), "method": method}
        if params is not None:
            payload["params"] = params
        headers = dict(HEADERS)
        if self.session_id:
            headers["Mcp-Session-Id"] = self.session_id
        body = json.dumps(payload).encode("utf-8")
        self.conn.request("POST", PATH, body=body, headers=headers)
        resp = self.conn.getresponse()
        raw = resp.read().decode("utf-8")
        if not self.session_id:
            self.session_id = resp.getheader("Mcp-Session-Id")
        results = parse_sse(raw)
        return results[-1] if results else {"error": "no response", "raw": raw[:300]}

    def initialize(self):
        r = self.call("initialize", {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "full-run", "version": "1.0"},
        })
        self.call("notifications/initialized", {})
        return r

    def tool(self, name, arguments=None):
        return self.call("tools/call", {"name": name, "arguments": arguments or {}})

    def execute(self, code):
        return self.tool("execute_code", {"code": code})

    def refresh(self):
        return self.tool("refresh_unity", {})

    def screenshot(self, path):
        escaped = path.replace("\\", "/")
        return self.execute(
            f'UnityEngine.ScreenCapture.CaptureScreenshot("{escaped}");'
            f'"requested"'
        )

    def read_console(self):
        return self.tool("read_console", {})

    def close(self):
        try:
            self.conn.close()
        except Exception:
            pass


def extract_text(result):
    """Extract text content from a tool call result."""
    content = result.get("result", {}).get("content", [])
    texts = []
    for item in content:
        t = item.get("text", "")
        if t:
            texts.append(t)
    return "\n".join(texts)


def run_level_pass(client, level, pass_num, strategy, output_base):
    """Run one level with one strategy, capturing staged screenshots."""
    tag = f"{LEVEL_NAMES[level]}_pass{pass_num}_{strategy}"
    level_dir = os.path.join(output_base, tag)
    os.makedirs(level_dir, exist_ok=True)

    result = {
        "level": level,
        "levelName": LEVEL_NAMES[level],
        "pass": pass_num,
        "strategy": strategy,
        "tag": tag,
        "screenshots": {},
        "waveStats": [],
        "errors": [],
        "audioClips": 0,
    }

    print(f"\n{'='*60}")
    print(f"  {tag} (Level {level})")
    print(f"{'='*60}")

    # 1. Configure level + difficulty
    print(f"  [1] Setting level {level}...")
    code = f"""
var routerType = System.Type.GetType("TDCampaignRouter, Assembly-CSharp");
if (routerType != null) {{
    var method = routerType.GetMethod("SaveLevelIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
    method?.Invoke(null, new object[] {{ {level} }});
}}
"level_set_{level}"
"""
    r = client.execute(code)
    time.sleep(0.5)

    # 2. Start P124 autoplay (this enters the scene and plays automatically)
    print(f"  [2] Starting P124 autoplay ({strategy})...")
    autoplay_code = f"""
var gmType = System.Type.GetType("TDGameManager, Assembly-CSharp");
if (gmType != null) {{
    var instanceField = gmType.GetField("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
    var gm = instanceField?.GetValue(null);
    if (gm != null) {{
        var debugMethod = gmType.GetMethod("DebugStartP124AutoplayForTest",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (debugMethod != null) {{
            debugMethod.Invoke(gm, new object[] {{ "{strategy}", 0, 90f }});
        }}
    }}
}}
"autoplay_started"
"""
    r = client.execute(autoplay_code)

    # 3. Capture screenshots at each phase
    for phase_name, delay_seconds in PHASES:
        print(f"  [3] Phase '{phase_name}' (waiting {delay_seconds}s)...")
        time.sleep(delay_seconds)
        screenshot_path = os.path.join(level_dir, f"{phase_name}.png").replace("\\", "/")
        try:
            client.screenshot(screenshot_path)
            time.sleep(1)  # let file write complete
            if os.path.isfile(screenshot_path):
                size = os.path.getsize(screenshot_path)
                result["screenshots"][phase_name] = {
                    "path": screenshot_path,
                    "size": size,
                }
                print(f"       captured {size} bytes")
            else:
                print(f"       WARNING: screenshot not found at {screenshot_path}")
        except Exception as e:
            print(f"       ERROR capturing: {e}")

        # Read wave stats and errors at mid and late phases
        if phase_name in ("mid", "late", "result"):
            try:
                console_r = client.read_console()
                console_text = extract_text(console_r)
                # Extract WaveStat lines
                for line in console_text.split("\n"):
                    if "WaveStat" in line:
                        result["waveStats"].append(line.strip()[:300])
                    if "error CS" in line.lower() or "exception" in line.lower():
                        result["errors"].append(line.strip()[:300])
            except Exception as e:
                print(f"       console read error: {e}")

    # 4. Check audio clip cache count
    print(f"  [4] Checking audio clip cache...")
    try:
        audio_code = """
var gmType = System.Type.GetType("TDGameManager, Assembly-CSharp");
var instanceField = gmType?.GetField("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
var gm = instanceField?.GetValue(null);
var cacheField = gmType?.GetField("_sfxClipCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var cache = cacheField?.GetValue(gm);
var count = (cache as System.Collections.Generic.Dictionary<string, UnityEngine.AudioClip>)?.Count ?? -1;
count.ToString()
"""
        audio_r = client.execute(audio_code)
        count_str = extract_text(audio_r).strip().strip('"')
        result["audioClips"] = int(count_str) if count_str.isdigit() else 0
        print(f"       audio clips cached: {result['audioClips']}")
    except Exception as e:
        print(f"       audio check error: {e}")

    # 5. Save result JSON
    result_path = os.path.join(level_dir, "result.json")
    with open(result_path, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=2, ensure_ascii=False)

    return result


def main():
    passes = 1
    if "--passes" in sys.argv:
        idx = sys.argv.index("--passes")
        passes = int(sys.argv[idx + 1])

    os.makedirs(OUTPUT_DIR, exist_ok=True)

    client = McpClient()
    print("Initializing MCP session...")
    init = client.initialize()
    server = init.get("result", {}).get("serverInfo", {})
    print(f"  Connected: {server.get('name', '?')} v{server.get('version', '?')}")

    print("Refreshing Unity...")
    client.refresh()
    time.sleep(3)

    all_results = []
    for pass_num in range(1, passes + 1):
        print(f"\n{'#'*60}")
        print(f"# PASS {pass_num} / {passes}")
        print(f"{'#'*60}")

        for level in LEVELS:
            strategy = STRATEGIES[(level + pass_num) % len(STRATEGIES)]
            try:
                result = run_level_pass(client, level, pass_num, strategy, OUTPUT_DIR)
                all_results.append(result)
            except Exception as e:
                print(f"  FAILED: {e}")
                all_results.append({
                    "level": level,
                    "pass": pass_num,
                    "error": str(e),
                })

            # Save aggregate after each level
            summary_path = os.path.join(OUTPUT_DIR, "summary.json")
            with open(summary_path, "w", encoding="utf-8") as f:
                json.dump({
                    "passes": passes,
                    "levels": LEVELS,
                    "results": all_results,
                    "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
                }, f, indent=2, ensure_ascii=False)

    client.close()

    # Print summary
    print(f"\n{'='*60}")
    print(f"RUN COMPLETE: {len(all_results)} runs")
    print(f"{'='*60}")
    total_errors = sum(len(r.get("errors", [])) for r in all_results)
    total_clips = sum(r.get("audioClips", 0) for r in all_results)
    print(f"Total errors detected: {total_errors}")
    print(f"Total audio clips cached: {total_clips}")
    for r in all_results:
        tag = r.get("tag", f"L{r.get('level','?')}")
        shots = len(r.get("screenshots", {}))
        errs = len(r.get("errors", []))
        clips = r.get("audioClips", 0)
        waves = len(r.get("waveStats", []))
        print(f"  {tag}: {shots} screenshots, {waves} wave stats, {errs} errors, {clips} clips")


if __name__ == "__main__":
    main()
