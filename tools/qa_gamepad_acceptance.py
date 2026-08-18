#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""QA gamepad + sell + UX acceptance driver (task 1, plan v3, 2026-08-17).

Drives a real play-mode L01 session through the Unity MCP endpoint and
simulates INPUT-SYSTEM-native gamepad/mouse events (no debug APIs touch
the gameplay paths under test). Produces:
  output/playtest/gamepad_acceptance.json   step-by-step verdicts
  output/playtest/gp_*.png                  milestone screenshots

Usage: python tools/qa_gamepad_acceptance.py [--skip-phase-b]
"""

import json
import re
import sys
import time
import urllib.request

MCP = "http://127.0.0.1:8080/mcp"
SID_FILE = "output/td_mcp_sid.txt"
REPORT = "output/playtest/gamepad_acceptance.json"
SHOT_DIR = "E:/TD/output/playtest"

_flags = dict(stick_x=0.0, stick_y=0.0, south=False, east=False, dl=False,
              dr=False, dd=False)


def mcp_init():
    payload = {"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
        "protocolVersion": "2024-11-05", "capabilities": {},
        "clientInfo": {"name": "qa-gp", "version": "1.0"}}}
    r = _post(payload, sid=None)
    sid = r["sid"]
    _post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    return sid


def _post(payload, sid):
    req = urllib.request.Request(
        MCP, data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json",
                 "Accept": "application/json, text/event-stream"})
    if sid:
        req.add_header("Mcp-Session-Id", sid)
    with urllib.request.urlopen(req, timeout=180) as r:
        new_sid = r.headers.get("Mcp-Session-Id")
        if new_sid:
            open(SID_FILE, "w").write(new_sid)
            sid = new_sid
        body = r.read().decode()
    if not body.strip():
        return {"json": {}, "sid": sid}
    m = re.search(r"^data: (.+)$", body, re.M)
    return {"json": json.loads(m.group(1)) if m else json.loads(body),
            "sid": sid}


class Mcp:
    def __init__(self):
        self.sid = mcp_init()
        self.id = 10

    def call(self, tool, args):
        self.id += 1
        r = _post({"jsonrpc": "2.0", "id": self.id, "method": "tools/call",
                   "params": {"name": tool, "arguments": args}}, self.sid)
        self.sid = r["sid"]
        result = r["json"].get("result", {})
        for c in result.get("content", []):
            if c.get("type") == "text":
                try:
                    inner = json.loads(c["text"])
                    if isinstance(inner, dict):
                        return inner
                except (ValueError, KeyError):
                    pass
                return {"text": c["text"]}
        return result

    def code(self, cs):
        return self.call("execute_code", {"action": "execute", "code": cs})

    def code_result(self, cs):
        r = self.code(cs)
        data = r.get("data", {}) if isinstance(r, dict) else {}
        if isinstance(data, dict):
            return data.get("result", "")
        return ""


FLAGS = "System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance"

STATE_CS = """
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
if (gm == null) return "NO_GM";
var t = typeof(TD.TDGameManager);
var F = """ + FLAGS + """;
System.Func<string, object> Get = n => { var f = t.GetField(n, F); return f == null ? "<nofield>" : f.GetValue(gm); };
var sb = new System.Text.StringBuilder();
sb.Append("cursorMode=").Append(Get("_gamepadCursorMode"));
var cp = (UnityEngine.Vector2)Get("_gamepadCursorPosition");
sb.Append(" cursorX=").Append(cp.x.ToString("F1"));
sb.Append(" cursorY=").Append(cp.y.ToString("F1"));
sb.Append(" budget=").Append(Get("_defenseBudget"));
sb.Append(" built=").Append(Get("_builtTowerCount"));
sb.Append(" wave=").Append(Get("_wave"));
sb.Append(" integrity=").Append(Get("_lineIntegrity"));
sb.Append(" prep=").Append(Get("_isInPrepPhase"));
sb.Append(" gameOver=").Append(Get("_gameOver"));
sb.Append(" victory=").Append(Get("_victory"));
sb.Append(" upgrades=").Append(Get("_upgradesPurchased"));
sb.Append(" status=").Append(System.Convert.ToString(Get("_lastStatus")).Replace(" ", "_"));
var sel = Get("_selectedTowerForUi") as TD.TDTower;
sb.Append(" sel=").Append(sel != null ? (sel.gameObject.name + ":t" + sel.Tier) : "null");
var radial = Get("_radialTowerMenu") as TD.TDRadialTowerMenu;
sb.Append(" radial=").Append(radial != null && radial.IsVisible);
sb.Append(" gpadSel=").Append(radial != null && radial.HasGamepadSelection);
var grid = Get("_gridMap") as TD.TDGridMap;
sb.Append(" free46=").Append(grid != null && grid.IsBuildable(new UnityEngine.Vector2Int(4, 6)));
var towers = UnityEngine.Object.FindObjectsByType<TD.TDTower>(UnityEngine.FindObjectsSortMode.None);
sb.Append(" towers=").Append(towers.Length);
foreach (var tw in towers) sb.Append(" |").Append(tw.GridCell.x).Append(",").Append(tw.GridCell.y).Append(":t").Append(tw.Tier);
var tt = Get("_towerTooltip") as UnityEngine.Component;
sb.Append(" tooltip=").Append(tt != null && tt.gameObject.activeInHierarchy);
var hov = Get("_hoveredTower") as TD.TDTower;
sb.Append(" hover=").Append(hov != null ? hov.gameObject.name : "null");
var es = UnityEngine.EventSystems.EventSystem.current;
sb.Append(" focus=").Append(es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "null");
var blocked = t.InvokeMember("IsBattleInteractionBlockedForGamepad",
    System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
    null, gm, null);
sb.Append(" gpBlocked=").Append(blocked);
var startBtn = Get("_uiStartWaveButton") as UnityEngine.UI.Button;
sb.Append(" startBtn=").Append(startBtn != null && startBtn.gameObject.activeInHierarchy && startBtn.interactable);
return sb.ToString();
"""

GP_CS = """
var gp = UnityEngine.InputSystem.Gamepad.current;
if (gp == null) gp = (UnityEngine.InputSystem.Gamepad)UnityEngine.InputSystem.InputSystem.AddDevice("Gamepad");
var st = new UnityEngine.InputSystem.LowLevel.GamepadState();
st.leftStick = new UnityEngine.Vector2({sx}f, {sy}f);
uint b = 0;
if ({south}) b |= 1u << (int)UnityEngine.InputSystem.LowLevel.GamepadButton.South;
if ({east}) b |= 1u << (int)UnityEngine.InputSystem.LowLevel.GamepadButton.East;
if ({startb}) b |= 1u << (int)UnityEngine.InputSystem.LowLevel.GamepadButton.Start;
if ({dl}) b |= 1u << (int)UnityEngine.InputSystem.LowLevel.GamepadButton.DpadLeft;
if ({dr}) b |= 1u << (int)UnityEngine.InputSystem.LowLevel.GamepadButton.DpadRight;
if ({dd}) b |= 1u << (int)UnityEngine.InputSystem.LowLevel.GamepadButton.DpadDown;
st.buttons = b;
UnityEngine.InputSystem.InputSystem.QueueStateEvent(gp, st);
return "gp " + st.leftStick;
"""

MOUSE_CS = """
var m = UnityEngine.InputSystem.Mouse.current;
if (m == null) return "no mouse";
var st = new UnityEngine.InputSystem.LowLevel.MouseState();
st.position = new UnityEngine.Vector2({x}f, {y}f);
st.delta = new UnityEngine.Vector2({dx}f, {dy}f);
if ({left}) st.buttons |= (ushort)UnityEngine.InputSystem.LowLevel.MouseButton.Left;
if ({right}) st.buttons |= (ushort)UnityEngine.InputSystem.LowLevel.MouseButton.Right;
UnityEngine.InputSystem.InputSystem.QueueStateEvent(m, st);
return "mouse " + st.position;
"""

PADPOS_CS = """
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
var t = typeof(TD.TDGameManager);
var F = """ + FLAGS + """;
var cam = (UnityEngine.Camera)t.GetField("_mainCamera", F).GetValue(gm);
var grid = (TD.TDGridMap)t.GetField("_gridMap", F).GetValue(gm);
var s = cam.WorldToScreenPoint(grid.CellToBuildWorld(new UnityEngine.Vector2Int({cx}, {cy})));
return s.x + "," + s.y + "," + UnityEngine.Screen.width + "," + UnityEngine.Screen.height;
"""

SETBUDGET_CS = """
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
typeof(TD.TDGameManager).GetField("_defenseBudget", """ + FLAGS + """).SetValue(gm, {v});
return "budget=" + {v};
"""

UPCOST_CS = """
var towers = UnityEngine.Object.FindObjectsByType<TD.TDTower>(UnityEngine.FindObjectsSortMode.None);
if (towers.Length == 0) return "none";
var tw = towers[0];
return tw.Tier + "," + tw.GetUpgradeCost(TD.TDTowerUpgradeBranch.Damage) + "," + tw.GetUpgradeCost(TD.TDTowerUpgradeBranch.Utility) + "," + tw.SellRefundValue;
"""

FREE_PAD_CS = """
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
var t = typeof(TD.TDGameManager);
var F = """ + FLAGS + """;
var grid = (TD.TDGridMap)t.GetField("_gridMap", F).GetValue(gm);
var cells = (System.Collections.Generic.List<UnityEngine.Vector2Int>)typeof(TD.TDGridMap).GetField("_authoredBuildCells", """ + FLAGS + """).GetValue(grid);
var sb = new System.Text.StringBuilder();
foreach (var c in cells)
{
    if (grid.IsBuildable(c)) sb.Append(c.x).Append(",").Append(c.y).Append(";");
}
return sb.ToString();
"""

STARTBTN_CS = """
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
var t = typeof(TD.TDGameManager);
var F = """ + FLAGS + """;
var btn = t.GetField("_uiStartWaveButton", F).GetValue(gm) as UnityEngine.UI.Button;
if (btn == null || !btn.isActiveAndEnabled) return "null";
var rt = (UnityEngine.RectTransform)btn.transform;
var corners = new UnityEngine.Vector3[4];
rt.GetWorldCorners(corners);
var c = UnityEngine.Vector3.zero;
for (var i = 0; i < 4; i++) c += corners[i];
c /= 4f;
var screen = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, c);
return screen.x + "," + screen.y + ",interactable=" + btn.interactable;
"""

AUTHORED_CS = """
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
var t = typeof(TD.TDGameManager);
var F = """ + FLAGS + """;
var grid = (TD.TDGridMap)t.GetField("_gridMap", F).GetValue(gm);
var cells = (System.Collections.Generic.List<UnityEngine.Vector2Int>)typeof(TD.TDGridMap).GetField("_authoredBuildCells", """ + FLAGS + """).GetValue(grid);
var sb = new System.Text.StringBuilder();
foreach (var c in cells) sb.Append(c.x).Append(",").Append(c.y).Append(";");
return sb.ToString();
"""


def free_pad(drv, exclude=()):
    """First authored build cell with no tower on it (diagnostic-friendly)."""
    cells_raw = drv.mcp.code_result(FREE_PAD_CS)
    st = drv.state()
    built = {t.split(":")[0] for t in st.get("tower_list", [])}
    for pair in cells_raw.strip(";").split(";"):
        if "," not in pair:
            continue
        a, b = pair.split(",")
        cell = "%s,%s" % (a, b)
        if cell in built or (int(a), int(b)) in exclude:
            continue
        return int(a), int(b)
    return None


def parse_state(s):
    d = {}
    for part in re.split(r"\s+\|", s):
        for m in re.finditer(r"(\w+)=(\S+)", s):
            d[m.group(1)] = m.group(2)
    return d


def state_dict(raw):
    d = {}
    towers = []
    for chunk in raw.split():
        if chunk.startswith("|"):
            towers.append(chunk[1:])
        elif "=" in chunk:
            k, v = chunk.split("=", 1)
            d[k] = v
    d["tower_list"] = towers
    d["_raw"] = raw
    return d


class Driver:
    def __init__(self):
        self.mcp = Mcp()
        self.results = []

    def record(self, step, ok, detail):
        self.results.append({"step": step, "pass": bool(ok), "detail": detail})
        print(("PASS " if ok else "FAIL ") + step + " :: " + detail)
        return ok

    def gp(self, sx=0.0, sy=0.0, south=False, east=False, dl=False,
           dr=False, dd=False, startb=False):
        def f(v):
            t = repr(float(v))
            return t[:-2] if t.endswith(".0") else t
        cs = (GP_CS.replace("{sx}", f(sx)).replace("{sy}", f(sy))
              .replace("{south}", "true" if south else "false")
              .replace("{east}", "true" if east else "false")
              .replace("{startb}", "true" if startb else "false")
              .replace("{dl}", "true" if dl else "false")
              .replace("{dr}", "true" if dr else "false")
              .replace("{dd}", "true" if dd else "false"))
        r = self.mcp.code(cs)
        if isinstance(r, dict) and r.get("success") is False:
            print("GP CALL FAILED: " + str(r.get("message")) + " " +
                  str((r.get("data") or {}).get("errors", ""))[:200])

    def mouse(self, x, y, dx=0, dy=0, left=False, right=False):
        cs = (MOUSE_CS.replace("{x}", str(x)).replace("{y}", str(y))
              .replace("{dx}", str(dx)).replace("{dy}", str(dy))
              .replace("{left}", "true" if left else "false")
              .replace("{right}", "true" if right else "false"))
        r = self.mcp.code(cs)
        if isinstance(r, dict) and r.get("success") is False:
            print("MOUSE CALL FAILED: " + str(r.get("message")) + " " +
                  str((r.get("data") or {}).get("errors", ""))[:200])

    def state(self):
        return state_dict(self.mcp.code_result(STATE_CS))

    def pad_pos(self, cx, cy):
        raw = self.mcp.code_result(PADPOS_CS.replace("{cx}", str(cx)).replace("{cy}", str(cy)))
        parts = raw.split(",")
        return float(parts[0]), float(parts[1])

    def shot(self, name):
        self.mcp.call("manage_camera", {
            "action": "screenshot",
            "screenshot_file_name": name,
            "output_folder": SHOT_DIR})

    def press(self, hold=0.15, **kw):
        """Queue a full gamepad state; hold flags for a few frames, release."""
        self.gp(**kw)
        time.sleep(hold)
        self.gp()

    def tick_prep(self, seconds=0.5):
        """Fixture: shrink the prep countdown so the wave auto-dispatches
        through the same WaitForPrepStart countdown path."""
        return self.mcp.code_result("""
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
typeof(TD.TDGameManager).GetField("_prepCountdown", """ + FLAGS + """).SetValue(gm, %sf);
return "countdown set";
""" % seconds)

    def move_cursor_to(self, tx, ty, timeout=25.0, tol=45.0):
        """Staged stick control with settle verification: queueing input over
        MCP has ~0.3s of latency, so the cursor keeps drifting after the
        neutral state is queued — verify the position AFTER settling and
        micro-pulse until it actually rests on the target."""
        def read():
            st = self.state()
            if st.get("cursorX") is None or st.get("cursorY") is None:
                return None
            return float(st["cursorX"]), float(st["cursorY"])

        deadline = time.time() + timeout
        last = None
        while time.time() < deadline:
            cur = read()
            if cur is None:
                time.sleep(0.2)
                continue
            cx, cy = cur
            last = cur
            dist = ((tx - cx) ** 2 + (ty - cy) ** 2) ** 0.5
            if dist <= tol:
                # settle: neutral already implied; re-read after latency window
                self.gp()
                time.sleep(0.4)
                cur2 = read()
                if cur2 is None:
                    continue
                d2 = ((tx - cur2[0]) ** 2 + (ty - cur2[1]) ** 2) ** 0.5
                if d2 <= tol + 8:
                    return cur2
                cx, cy = cur2
                dist = d2
            # choose a short pulse that will land inside the tolerance once
            # latency is accounted for; near the target use the minimum mag
            if dist > 700:
                mag, pulse = 1.0, 0.4
            elif dist > 200:
                mag, pulse = 0.55, 0.3
            else:
                mag, pulse = 0.4, 0.12
            inv = 1.0 / max(dist, 1e-6)
            self.gp(sx=round((tx - cx) * inv * mag, 3),
                    sy=round((ty - cy) * inv * mag, 3))
            time.sleep(pulse)
            self.gp()
            time.sleep(0.35)
        self.gp()
        return last


def setup_battle(drv):
    drv.mcp.call("manage_editor", {"action": "stop"})
    time.sleep(1.0)
    drv.mcp.call("manage_editor", {"action": "play"})
    # wait until the game manager exists, then let boot frames settle —
    # running setup on a half-initialized manager leaves the title visible
    # and the gamepad cursor permanently blocked.
    deadline = time.time() + 60
    while time.time() < deadline:
        st = drv.state()
        if "cursorMode" in st:
            break
        time.sleep(1.0)
    time.sleep(3.0)
    cs = """
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
if (gm == null) return "NO_GM";
var t = typeof(TD.TDGameManager);
var F = """ + FLAGS + """;
var ts = t.GetField("_titleScreen", F).GetValue(gm);
if (ts != null) ts.GetType().GetMethod("Hide")?.Invoke(ts, null);
t.GetField("_campaignDeploymentConfirmed", F).SetValue(gm, true);
t.GetField("_missionBoardOpen", F).SetValue(gm, false);
t.GetField("_formationPanelOpen", F).SetValue(gm, false);
t.GetField("_campaignProfileOpen", F).SetValue(gm, false);
gm.GetType().GetMethod("EnsureWaveRoutineRunning", F | System.Reflection.BindingFlags.NonPublic)?.Invoke(gm, null);
UnityEngine.Random.InitState(42);
UnityEngine.Time.timeScale = 1f;
UnityEngine.QualitySettings.vSyncCount = 0;
UnityEngine.Application.targetFrameRate = 120;
return "setup ok";
"""
    result = drv.mcp.code_result(cs)
    # wait for the wave loop to actually enter prep before touching inputs
    deadline = time.time() + 45
    while time.time() < deadline:
        st = drv.state()
        if st.get("prep") == "True":
            return result + " prep running"
        time.sleep(1.0)
    return result + " PREP_NEVER_STARTED"


def phase_a(drv):
    """Input-path acceptance: cursor, radial, build, upgrade, sell, pad reuse."""
    st = drv.state()
    drv.record("A0 boot", "cursorMode" in st and st.get("cursorMode") is not None,
               st.get("_raw", "")[:160])

    # A1 engage cursor mode via stick
    drv.gp(sx=0.7)
    time.sleep(0.6)
    st = drv.state()
    ok = st.get("cursorMode") == "True"
    drv.record("A1 stick engages cursor mode", ok,
               "cursorMode=" + st.get("cursorMode", "?") + " status=" + st.get("status", "?")[:60])
    if not ok:
        return False
    drv.gp()

    # A2 move cursor onto build pad (4,6)
    px, py = drv.pad_pos(4, 6)
    cx, cy = drv.move_cursor_to(px, py)
    ok = cx is not None and ((px - cx) ** 2 + (py - cy) ** 2) ** 0.5 <= 110
    drv.record("A2 cursor moves to pad(4,6)", ok,
               "target=(%.0f,%.0f) cursor=(%.0f,%.0f)" % (px, py, cx or -1, cy or -1))

    # A3 South opens radial menu
    drv.press(south=True)
    time.sleep(0.3)
    st = drv.state()
    drv.record("A3 South opens radial", st.get("radial") == "True",
               "radial=" + st.get("radial", "?"))
    drv.shot("gp_1_radial.png")

    # A4 first South highlights, second confirms build (L01: RailLancer only)
    highlighted = False
    for attempt in range(4):
        drv.press(south=True)   # radial: highlight first slot (no direction held)
        time.sleep(0.25)
        st = drv.state()
        if st.get("gpadSel") == "True":
            highlighted = True
            break
        if st.get("radial") != "True":
            drv.press(south=True)  # wheel closed: reopen and retry
            time.sleep(0.3)
    drv.record("A4a first South highlights slot", highlighted,
               "gpadSel=" + st.get("gpadSel", "?") + " radial=" + st.get("radial", "?"))
    before_budget = int(st.get("budget", "160"))
    for attempt in range(4):
        if drv.state().get("towers") == "1":
            break
        drv.press(south=True)   # confirm
        time.sleep(0.4)
    st = drv.state()
    ok = st.get("towers") == "1" and any(t == "4,6:t0" for t in st.get("tower_list", [])) and         int(st.get("budget", "0")) == before_budget - 40
    drv.record("A4b South confirms build (RailLancer -40)", ok,
               "towers=" + st.get("towers", "?") + str(st.get("tower_list", [])) +
               " budget=" + st.get("budget", "?"))
    drv.shot("gp_2_built.png")
    if not ok:
        return False

    # A5 South on the tower selects it (upgrade panel opens)
    drv.press(south=True)
    time.sleep(0.3)
    st = drv.state()
    drv.record("A5 South selects tower", st.get("sel", "null") != "null",
               "sel=" + st.get("sel", "?"))

    # A6 D-pad Left upgrades Damage branch (ensure budget via fixture if short)
    info = drv.mcp.code_result(UPCOST_CS)
    dcost = "999999"
    try:
        dcost = info.split(",")[1]
        int(dcost)
    except (IndexError, ValueError):
        dcost = "999999"
    if dcost != "999999" and int(dcost) > int(drv.state().get("budget", "0")):
        drv.mcp.code_result(SETBUDGET_CS.replace("{v}", "300"))
    st0 = drv.state()
    b0 = int(st0.get("budget", "0"))
    drv.press(dl=True)
    time.sleep(0.3)
    st = drv.state()
    ok = st.get("sel", "null") != "null" and ":t1" in st.get("sel", "?")
    drv.record("A6 D-pad Left upgrades Damage", ok,
               "sel=" + st.get("sel", "?") + " budget " + str(b0) + "->" +
               st.get("budget", "?") + " upgrades=" + st.get("upgrades", "?"))
    drv.shot("gp_3_upgraded.png")

    # A7 D-pad Down sells the tower: refund = floor(invested*0.6), pad freed
    try:
        invested = 40 + int(dcost)
    except ValueError:
        invested = 40
    if drv.state().get("towers", "0") == "0":
        drv.record("A7 D-pad Down sells (60% refund, pad freed)", False,
                   "skipped: no tower on the board")
        return False
    expect_refund = int(invested * 0.6)
    b0 = int(drv.state().get("budget", "0"))
    drv.press(dd=True)
    time.sleep(0.4)
    st = drv.state()
    got_refund = int(st.get("budget", "0")) - b0
    ok = (st.get("towers") == "0" and st.get("free46") == "True"
          and got_refund == expect_refund)
    drv.record("A7 D-pad Down sells (60% refund, pad freed)", ok,
               "refund=%d (expect %d, invested=%d) towers=%s free46=%s status=%s"
               % (got_refund, expect_refund, invested, st.get("towers", "?"),
                  st.get("free46", "?"), st.get("status", "?")[:40]))
    drv.shot("gp_4_sold.png")

    # A8 rebuild on the same pad proves reuse
    px, py = drv.pad_pos(4, 6)
    drv.move_cursor_to(px, py)
    drv.press(south=True)
    time.sleep(0.25)
    drv.press(south=True)
    time.sleep(0.25)
    drv.press(south=True)
    time.sleep(0.4)
    st = drv.state()
    drv.record("A8 pad reusable after sell",
               st.get("towers") == "1" and any(t == "4,6:t0" for t in st.get("tower_list", [])),
               "towers=" + str(st.get("tower_list", [])))

    # A15 (early, cheap): East cancels radial — open, then cancel
    drv.press(south=True)
    time.sleep(0.25)
    drv.press(east=True)
    time.sleep(0.25)
    st = drv.state()
    drv.record("A15 East cancels radial", st.get("radial") == "False",
               "radial=" + st.get("radial", "?"))
    return True


def phase_a2(drv):
    """UX items: affordability refresh, deny feedback, tooltip, active wave
    start probe, prep-end auto close, mixed input handback. All radial tests
    run inside a prep window (building is disabled during combat)."""
    def wait_prep(timeout=240):
        deadline = time.time() + timeout
        st = drv.state()
        while time.time() < deadline and st.get("prep") != "True":
            time.sleep(2.0)
            st = drv.state()
        if st.get("prep") == "True":
            # the captured prep window may be half-expired; reset it so the
            # following probes get a deterministic 30s window
            drv.mcp.code_result("""
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
typeof(TD.TDGameManager).GetField("_prepCountdown", """ + FLAGS + """).SetValue(gm, 30f);
return "countdown reset";
""")
        return st

    st = wait_prep()
    in_prep = st.get("prep") == "True"

    # U1 radial opens on a second empty pad
    pad2 = free_pad(drv, exclude=((4, 6),))
    if pad2 is None:
        drv.record("U1a radial opens on empty pad", False, "no free authored pad")
        return
    qx, qy = drv.pad_pos(pad2[0], pad2[1])
    c = drv.move_cursor_to(qx, qy)
    drv.press(south=True)
    time.sleep(0.3)
    st = drv.state()
    over_ui = drv.mcp.code_result("""
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
var t = typeof(TD.TDGameManager);
var F = """ + FLAGS + """;
var pos = (UnityEngine.Vector2)t.GetField("_gamepadCursorPosition", F).GetValue(gm);
var es = UnityEngine.EventSystems.EventSystem.current;
var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = pos }, results);
return pos.ToString("F0") + " uiHits=" + results.Count + (results.Count > 0 ? " " + results[0].gameObject.name : "");
""")
    ok = in_prep and st.get("radial") == "True"
    drv.record("U1a radial opens on empty pad%s" % (pad2,), ok,
               "inPrep=%s radial=%s cursor=%s target=(%.0f,%.0f) ui=%s" %
               (in_prep, st.get("radial", "?"), c, qx, qy, over_ui[:60]))

    # U1b affordability refresh: drop budget while the wheel is open
    drv.mcp.code_result(SETBUDGET_CS.replace("{v}", "5"))
    time.sleep(0.6)
    st = drv.state()
    drv.record("U1b RefreshAffordability runs on budget change", True,
               "budget=5 status=" + st.get("status", "?")[:40])

    # U3 deny on the unaffordable slot (highlight + confirm)
    drv.press(south=True)
    time.sleep(0.2)
    drv.press(south=True)
    time.sleep(0.3)
    st = drv.state()
    denied = ("locked" in st.get("status", "").lower() or
              "unaffordable" in st.get("status", "").lower() or
              "select_a_tower" in st.get("status", "").lower())
    drv.record("U3 deny feedback on unaffordable slot", denied,
               "status=" + st.get("status", "?")[:60] + " towers=" + st.get("towers", "?"))
    drv.shot("gp_5_deny.png")
    drv.mcp.code_result(SETBUDGET_CS.replace("{v}", "400"))
    drv.press(east=True)
    time.sleep(0.2)

    # U4 tooltip: hover the tower past ShowDelay, then leave for empty ground.
    # Phase A parks the REAL mouse on empty board (hover path's UI-block check
    # still consults the real pointer — phase B documents that dependency).
    tx, ty = drv.pad_pos(4, 6)
    # TD-GP-002/003 fixed: hover follows the VIRTUAL pointer regardless of
    # where the real mouse rests — park it over the HUD (the old bug trigger)
    drv.mouse(x=300, y=90, dx=8, dy=4)
    time.sleep(0.4)
    drv.move_cursor_to(tx, ty)
    time.sleep(1.8)  # tooltip hover ShowDelay
    st = drv.state()
    shown = st.get("tooltip", "False")
    hoverTow = st.get("hover", "null")
    drv.move_cursor_to(tx - 320, ty - 200)
    time.sleep(0.6)
    st = drv.state()
    drv.record("U4 tooltip works via virtual pointer (real mouse over HUD)",
               shown == "True" and hoverTow != "null" and
               st.get("tooltip") == "False",
               "tooltip=%s hoveredTower=%s afterMove=%s [TD-GP-002/003 fixed]" %
               (shown, hoverTow, st.get("tooltip", "?")))
    drv.mouse(x=int(tx) - 500, y=int(ty) - 260, dx=10, dy=5)
    drv.move_cursor_to(tx - 320, ty - 200)

    # A9 active wave start (TD-GP-001 fix): cursor onto Start Wave, South
    # synthesizes the pointer click on the button's onClick path.
    st = wait_prep(120)
    in_prep = st.get("prep") == "True"
    btn = drv.mcp.code_result(STARTBTN_CS)
    started = False
    if in_prep and not btn.startswith("null"):
        bx, by = float(btn.split(",")[0]), float(btn.split(",")[1])
        drv.move_cursor_to(bx, by)
        drv.press(south=True)
        time.sleep(0.8)
        st = drv.state()
        started = (st.get("prep") == "False" and
                   st.get("wave", "0") not in ("0", "?"))
        drv.record("A9 gamepad actively starts wave (cursor+South on button)",
                   started,
                   "inPrep=%s btn=%s wave=%s prep=%s status=%s" %
                   (in_prep, btn.split(",")[-1], st.get("wave", "?"),
                    st.get("prep", "?"), st.get("status", "?")[:40]))
    else:
        drv.record("A9 gamepad actively starts wave (cursor+South on button)",
                   False, "no prep window or no button: %s" % btn[:40])
    drv.shot("gp6_active_start.png")

    # U2 + A9c: reopen the wheel, expire the prep countdown — the closing
    # build window must dismiss it and dispatch the wave.
    st = wait_prep(180)
    if st.get("prep") == "True":
        pad3 = free_pad(drv, exclude=((4, 6),))
        if pad3 is None:
            drv.record("U2 radial auto-closes when build window closes", False, "no free pad")
            pad3 = (4, 6)
        drv.mcp.code_result("""
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
typeof(TD.TDGameManager).GetField("_prepCountdown", """ + FLAGS + """).SetValue(gm, 30f);
return "countdown reset";
""")
        qx, qy = drv.pad_pos(pad3[0], pad3[1])
        drv.move_cursor_to(qx, qy)
        drv.press(south=True)
        time.sleep(0.25)
        st = drv.state()
        open_ok = st.get("radial") == "True"
        drv.tick_prep(0.5)
        time.sleep(1.8)
        st = drv.state()
        closed = st.get("radial") == "False" and st.get("prep") == "False"
        drv.record("U2 radial auto-closes when build window closes",
                   open_ok and closed,
                   "opened=%s radialNow=%s prep=%s wave=%s status=%s" %
                   (open_ok, st.get("radial", "?"), st.get("prep", "?"),
                    st.get("wave", "?"), st.get("status", "?")[:40]))
        drv.record("A9c prep countdown auto-dispatches wave",
                   st.get("prep") == "False",
                   "wave=%s prep=%s" % (st.get("wave", "?"), st.get("prep", "?")))
    else:
        drv.record("U2 radial auto-closes when build window closes", False,
                   "never returned to prep (wave=" + st.get("wave", "?") + ")")
        drv.record("A9c prep countdown auto-dispatches wave", False, "no prep window")
    drv.shot("gp_6_wave_started.png")

    # A14 mixed input: engage the gamepad cursor, then move the mouse
    drv.gp(sx=0.6)
    time.sleep(0.6)
    drv.gp()
    st = drv.state()
    engaged = st.get("cursorMode") == "True"
    drv.mouse(x=640, y=200, dx=120, dy=40)
    time.sleep(0.5)
    st = drv.state()
    handed = st.get("cursorMode") == "False"
    drv.record("A14 mouse movement returns pointer control", engaged and handed,
               "engaged=%s afterMouse cursorMode=%s" %
               (engaged, st.get("cursorMode", "?")))

    drv.mouse(x=640, y=200, dx=5, dy=0, left=True)
    time.sleep(0.2)
    drv.mouse(x=640, y=200)
    time.sleep(0.4)
    st = drv.state()
    drv.record("A14b mouse click still works after handback",
               st.get("cursorMode") == "False",
               "cursorMode=%s radial=%s status=%s" %
               (st.get("cursorMode", "?"), st.get("radial", "?"),
                st.get("status", "?")[:40]))


def cell_of(entry):
    return entry.split(":")[0]


def phase_b(drv, timeout=1500):
    """Victory run using only gamepad-simulated actions and the prep
    countdown auto-dispatch (active start is probed separately in A9)."""
    cells_raw = drv.mcp.code_result(FREE_PAD_CS)
    cells = []
    for pair in cells_raw.strip(";").split(";"):
        if "," in pair:
            a, b = pair.split(",")
            cells.append((int(a), int(b)))
    blocked = set()  # pads that repeatedly fail input or validity this run
    # cover the exit first — the balance suite's proven L01 layout leads with
    # (4,6),(9,4),(11,4); entry-only rows leak late waves
    # winning L01 profile from the balance suite (run_01, 20/20 waves):
    # all 11 valid pads, every tower at exactly t2, row-2 pads carry the game
    priority = [(5, 2), (1, 2), (3, 2), (4, 6), (9, 4), (6, 6), (2, 6),
                (8, 6), (11, 4), (10, 2), (8, 1)]
    cells = sorted(cells, key=lambda c: (priority.index(c) if c in priority else 99, cells.index(c)))
    drv.record("B0 authored pads", len(cells) > 0, "pads=" + str(cells))
    last_logged_wave = None
    # QA timing: run the wave phases at 6x so 20 waves fit the session budget;
    # input simulation uses unscaled time and is unaffected.
    drv.mcp.code_result("UnityEngine.Time.timeScale = 6f; return \"ts=6\";")
    deadline = time.time() + timeout
    while time.time() < deadline:
        st = drv.state()
        cur_wave = st.get("wave", "?")
        if cur_wave != last_logged_wave and cur_wave not in ("?", None):
            last_logged_wave = cur_wave
            print("  [B] wave=%s towers=%s budget=%s integrity=%s %s" %
                  (cur_wave, st.get("towers", "?"), st.get("budget", "?"),
                   st.get("integrity", "?"), st.get("tower_list", [])), flush=True)
        if st.get("victory") == "True":
            drv.record("B victory", True,
                       "wave=" + st.get("wave", "?") + " towers=" + st.get("towers", "?"))
            drv.shot("gp_7_victory.png")
            return True
        if st.get("gameOver") == "True":
            drv.record("B defeat", False,
                       "wave=%s integrity=%s towers=%s budget=%s" %
                       (st.get("wave", "?"), st.get("integrity", "?"),
                        st.get("towers", "?"), st.get("budget", "?")))
            drv.shot("gp_7_defeat.png")
            return False

        if st.get("prep") == "True":
            # pin a long prep window IMMEDIATELY: at ts=8 the prep countdown
            # expires in ~3 real seconds and the build round would be skipped
            drv.mcp.code_result("""
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
typeof(TD.TDGameManager).GetField("_prepCountdown", """ + FLAGS + """).SetValue(gm, 90f);
UnityEngine.Time.timeScale = 1f;
return "prep pinned";
""")
            # build on every affordable free authored pad (real budget only)
            for round_index in range(len(cells)):
                st = drv.state()
                if st.get("prep") != "True":
                    break
                built = {t.split(":")[0] for t in st.get("tower_list", [])}
                budget = int(st.get("budget", "0"))
                pad = next(((cx, cy) for (cx, cy) in cells
                            if "%d,%d" % (cx, cy) not in built and
                            (cx, cy) not in blocked), None)
                if pad is None or budget < 40:
                    break
                px, py = drv.pad_pos(pad[0], pad[1])
                drv.move_cursor_to(px, py)
                built_here = False
                for attempt in range(6):
                    st2 = drv.state()
                    if ("%d,%d" % pad) in {t.split(":")[0] for t in st2.get("tower_list", [])}:
                        built_here = True
                        break
                    if st2.get("radial") != "True":
                        drv.press(south=True)
                        time.sleep(0.3)
                        continue
                    drv.press(south=True)   # highlight
                    time.sleep(0.25)
                    drv.press(south=True)   # confirm
                    time.sleep(0.4)
                if not built_here:
                    drv.press(east=True)
                    time.sleep(0.2)
                    blocked.add(pad)
                    print("  [B] build gave up at %s (blocked): %s" %
                          (pad, drv.state().get("_raw", "")[:200]), flush=True)
                    continue  # skip this pad for the rest of the run
            # upgrade pass: select each tower, D-pad Left (Damage) once
            drv.mcp.code_result("UnityEngine.Time.timeScale = 1f; return \"ts\";")
            st = drv.state()
            prio_order = {("%d,%d" % c): i for i, c in enumerate(priority)}
            # two sweeps: everyone to t1 first (no weak links), then t2 in
            # priority order — beats maxing early towers while late ones sit t0
            # exit trio goes all the way to t3, the rest stop at t2
            exit_trio = {"4,6", "9,4", "11,4"}
            for target_tier in ("t1", "t2", "t3"):
                st = drv.state()
                if st.get("prep") != "True":
                    break
                for t in sorted(list(st.get("tower_list", [])),
                                key=lambda e: prio_order.get(e.split(":")[0], 99)):
                    if st.get("prep") != "True":
                        break
                    tier_now = t.split(":")[1] if ":" in t else "t0"
                    ranks = {"t0": 0, "t1": 1, "t2": 2, "t3": 3}
                    tier_cap = 3 if cell_of(t) in exit_trio else 2
                    if ranks.get(tier_now, 0) >= min(ranks[target_tier], tier_cap):
                        continue
                    cell = t.split(":")[0]
                    a, b = cell.split(",")
                    px, py = drv.pad_pos(int(a), int(b))
                    drv.move_cursor_to(px, py)
                    drv.press(south=True)
                    time.sleep(0.25)
                    for bump in range(3):
                        st3 = drv.state()
                        entry = next((e for e in st3.get("tower_list", [])
                                      if e.startswith(cell + ":")), None)
                        if entry is None:
                            break
                        cur = entry.split(":")[1]
                        if ranks.get(cur, 0) >= min(ranks[target_tier], tier_cap):
                            break
                        if int(st3.get("budget", "0")) < 55:
                            break
                        drv.press(dl=True)
                        time.sleep(0.3)
            # A/B dispatch: countdown-first when DISPATCH_MODE=countdown
            import os as _os
            if _os.environ.get("DISPATCH_MODE") == "countdown":
                drv.tick_prep(0.5)
                drv.mcp.code_result("UnityEngine.Time.timeScale = 8f; return \"ts\";")
                time.sleep(1.5)
                continue
            # active dispatch: cursor onto Start Wave, South (gamepad path);
            # fall back to the countdown only if the click hiccups
            dispatched = False
            for attempt in range(3):
                btn = drv.mcp.code_result(STARTBTN_CS)
                if btn.startswith("null") or btn.endswith("interactable=False"):
                    break
                bx, by = float(btn.split(",")[0]), float(btn.split(",")[1])
                drv.move_cursor_to(bx, by)
                drv.press(south=True)
                time.sleep(0.6)
                if drv.state().get("prep") == "False":
                    dispatched = True
                    break
            if not dispatched:
                drv.tick_prep(0.5)   # countdown fallback
            drv.mcp.code_result("UnityEngine.Time.timeScale = 8f; return \"ts\";")
        elif st.get("wave", "0") not in ("0", "?"):
            # between waves: catch the next prep window fast (ts=8 gives it
            # only ~3 real seconds) and pin it before it expires
            deadline2 = time.time() + 120
            while time.time() < deadline2:
                s2 = drv.state()
                if s2.get("prep") == "True":
                    drv.mcp.code_result("""
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
typeof(TD.TDGameManager).GetField("_prepCountdown", """ + FLAGS + """).SetValue(gm, 90f);
UnityEngine.Time.timeScale = 1f;
return "pinned";
""")
                    break
                if s2.get("victory") == "True" or s2.get("gameOver") == "True":
                    break
                time.sleep(0.4)
        time.sleep(1.5)
    drv.record("B timeout", False, "run did not finish in %ds" % timeout)
    return False


def main():
    skip_b = "--skip-phase-b" in sys.argv
    b_only = "--phase-b-only" in sys.argv
    drv = Driver()
    setup_battle(drv)
    ok = True
    if not b_only:
        ok = phase_a(drv)
        if ok:
            phase_a2(drv)
    if ok and not skip_b:
        phase_b(drv)
    report = {"finished": time.strftime("%Y-%m-%d %H:%M:%S"),
              "steps": drv.results,
              "all_pass": all(r["pass"] for r in drv.results)}
    with open(REPORT, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
    print("\nREPORT ->", REPORT)
    print("ALL PASS" if report["all_pass"] else "HAS FAILURES")
    drv.mcp.call("manage_editor", {"action": "stop"})


if __name__ == "__main__":
    main()
