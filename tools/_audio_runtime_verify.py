"""Verify audio playback at runtime: enter play mode, autoplay briefly, inspect audio state."""
import http.client
import json
import os
import sys
import time

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


def connect():
    return http.client.HTTPConnection(HOST, PORT, timeout=30)


def post(conn, payload, sid=None):
    headers = dict(H)
    if sid:
        headers["Mcp-Session-Id"] = sid
    conn.request("POST", PATH, body=json.dumps(payload).encode(), headers=headers)
    resp = conn.getresponse()
    raw = resp.read().decode()
    sid_out = resp.getheader("Mcp-Session-Id")
    return sid_out, parse(raw)


def init_session(conn):
    sid, r = post(conn, {
        "jsonrpc": "2.0", "id": 1, "method": "initialize",
        "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                   "clientInfo": {"name": "audio-verify", "version": "1.0"}},
    })
    post(conn, {"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}, sid)
    return sid


def execute(conn, sid, code):
    _, r = post(conn, {
        "jsonrpc": "2.0", "id": 99, "method": "tools/call",
        "params": {"name": "execute_code", "arguments": {"action": "execute", "code": code}},
    }, sid)
    content = r.get("result", {}).get("content", [])
    for item in content:
        txt = item.get("text", "")
        if txt:
            # Parse the JSON response
            try:
                data = json.loads(txt)
                return data.get("data", {}).get("result", txt)
            except json.JSONDecodeError:
                return txt
    return ""


def main():
    conn = connect()
    print("1. Initialize MCP session...")
    sid = init_session(conn)
    print("   Session:", sid)

    # Check state
    print("2. Check current state...")
    state = execute(conn, sid,
        'return "isPlaying=" + UnityEditor.EditorApplication.isPlaying'
        + ' + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;')
    print("   ", state)

    if "isPlaying=False" in str(state):
        print("3. Entering play mode...")
        try:
            post(conn, {"jsonrpc": "2.0", "id": 3, "method": "tools/call",
                        "params": {"name": "manage_editor", "arguments": {"action": "play"}}}, sid)
        except Exception:
            pass
        time.sleep(20)
        # Reconnect after domain reload
        conn = connect()
        sid = init_session(conn)
        print("   Reconnected after domain reload")

    # Set level 1
    print("4. Setting level 1...")
    execute(conn, sid,
        'var t = System.Type.GetType("TDCampaignRouter, Assembly-CSharp");'
        ' var m = t.GetMethod("SaveLevelIndex");'
        ' m.Invoke(null, new object[]{1});'
        ' return "level_set";')

    # Start P124 autoplay (this may disconnect MCP if it reloads the domain)
    print("5. Starting P124 autoplay...")
    try:
        execute(conn, sid,
            'var t = System.Type.GetType("TDGameManager, Assembly-CSharp");'
            ' var f = t.GetField("Instance", System.Reflection.BindingFlags.Public'
            ' | System.Reflection.BindingFlags.Static);'
            ' var gm = f.GetValue(null);'
            ' var m = t.GetMethod("DebugStartP124AutoplayForTest",'
            ' System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);'
            ' m.Invoke(gm, new object[]{"focused_fire", 0, 30f});'
            ' return "autoplay_started";')
    except Exception:
        pass

    # Wait for combat to generate SFX events, then reconnect
    print("6. Waiting 20s for combat to generate audio events...")
    time.sleep(20)
    conn = connect()
    sid = init_session(conn)
    print("   Reconnected after autoplay start")

    # Inspect audio state
    print("7. Inspecting audio system state...")
    audio_code = """
var t = System.Type.GetType("TDGameManager, Assembly-CSharp");
var f = t.GetField("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
var gm = f.GetValue(null);
var sb = new System.Text.StringBuilder();

// Audio sources
var sfxField = t.GetField("_sfxSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var tacField = t.GetField("_tacticalSfxSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var critField = t.GetField("_criticalSfxSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var musicField = t.GetField("_musicSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var ambField = t.GetField("_ambienceSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

var sfx = (UnityEngine.AudioSource)sfxField.GetValue(gm);
var tac = (UnityEngine.AudioSource)tacField.GetValue(gm);
var crit = (UnityEngine.AudioSource)critField.GetValue(gm);
var music = (UnityEngine.AudioSource)musicField.GetValue(gm);
var amb = (UnityEngine.AudioSource)ambField.GetValue(gm);

sb.Append("sfx=").Append(sfx != null).Append(" vol=").Append(sfx != null ? sfx.volume : -1);
sb.Append(" tac=").Append(tac != null).Append(" vol=").Append(tac != null ? tac.volume : -1);
sb.Append(" crit=").Append(crit != null).Append(" vol=").Append(crit != null ? crit.volume : -1);
sb.Append("\\n");
sb.Append("music=").Append(music != null).Append(" vol=").Append(music != null ? music.volume : -1)
  .Append(" clip=").Append(music != null && music.clip != null ? music.clip.name : "null")
  .Append(" isPlaying=").Append(music != null && music.isPlaying);
sb.Append("\\n");
sb.Append("ambience=").Append(amb != null).Append(" vol=").Append(amb != null ? amb.volume : -1)
  .Append(" clip=").Append(amb != null && amb.clip != null ? amb.clip.name : "null")
  .Append(" isPlaying=").Append(amb != null && amb.isPlaying);
sb.Append("\\n");

// Clip cache
var cacheField = t.GetField("_sfxClipCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var cache = (System.Collections.Generic.Dictionary<string, UnityEngine.AudioClip>)cacheField.GetValue(gm);
sb.Append("clipCacheCount=").Append(cache.Count).Append("\\n");
int realClips = 0;
int synthClips = 0;
foreach (var kvp in cache) {
    if (kvp.Value != null) {
        if (kvp.Value.name.StartsWith("td_sfx_")) synthClips++;
        else realClips++;
    }
}
sb.Append("realClips=").Append(realClips).Append(" synthClips=").Append(synthClips).Append("\\n");

// List first 10 cached clip names
int shown = 0;
foreach (var kvp in cache) {
    if (shown >= 10) break;
    sb.Append("  ").Append(kvp.Key).Append(" -> ").Append(kvp.Value != null ? kvp.Value.name : "null")
      .Append(" (").Append(kvp.Value != null ? kvp.Value.length.ToString("F2") : "0").Append("s)\\n");
    shown++;
}

// Game state
var waveField = t.GetField("_wave", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var wave = waveField.GetValue(gm);
sb.Append("wave=").Append(wave);
return sb.ToString();
"""
    result = execute(conn, sid, audio_code)
    print("   Audio state:")
    for line in result.split("\\n"):
        print("   ", line.replace("\\n", ""))

    # Read console for audio-related messages
    print("\n8. Reading console for audio/errors...")
    _, r = post(conn, {
        "jsonrpc": "2.0", "id": 50, "method": "tools/call",
        "params": {"name": "read_console", "arguments": {}},
    }, sid)
    content = r.get("result", {}).get("content", [])
    all_lines = []
    for item in content:
        txt = item.get("text", "")
        if txt:
            all_lines.append(txt)

    wave_lines = [l for l in all_lines if "WaveStat" in l]
    error_lines = [l for l in all_lines if "Exception" in l or "error" in l.lower()]
    audio_lines = [l for l in all_lines if "audio" in l.lower() or "clip" in l.lower() or "Resources.Load" in l]

    print("   WaveStat lines:", len(wave_lines))
    print("   Error lines:", len(error_lines))
    print("   Audio-related lines:", len(audio_lines))
    for l in error_lines[:5]:
        print("   ERROR:", l[:200])
    for l in audio_lines[:3]:
        print("   AUDIO:", l[:200])

    # Stop play mode
    print("\n9. Stopping play mode...")
    try:
        post(conn, {"jsonrpc": "2.0", "id": 60, "method": "tools/call",
                    "params": {"name": "manage_editor", "arguments": {"action": "stop"}}}, sid)
    except Exception:
        pass

    conn.close()
    print("\nDone.")


if __name__ == "__main__":
    main()
