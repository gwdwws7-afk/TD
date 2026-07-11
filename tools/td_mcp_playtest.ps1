param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [int]$LevelIndex = 1,
    [int]$DurationSeconds = 45,
    [int]$UnityReadyTimeoutSeconds = 45,
    [string]$BuildPlan = "1,1:RailLancer;4,2:RailLancer;8,3:RailLancer",
    [string]$UpgradePlan = "",
    [int]$BonusBudget = 0,
    [ValidateSet("", "EmberSurge", "FractureMark")]
    [string]$ResonanceCommand = "",
    [string]$ExpectState = "",
    [string]$ScreenshotPath = "E:/TD/output/playtest/mcp_autorun_latest.png",
    [string]$SummaryPath = "E:/TD/output/playtest/mcp_autorun_latest.json",
    [switch]$SkipAutoBuild,
    [switch]$SkipStartWave,
    [switch]$ResetCodex,
    [switch]$RefreshScripts,
    [switch]$AllowConsoleIssues,
    [switch]$KeepPlaying
)

$ErrorActionPreference = "Stop"
$runStartedUtc = [DateTime]::UtcNow

function ConvertFrom-McpEvent {
    param([string]$Content)
    $dataLine = ($Content -split "`n" | Where-Object { $_ -like "data:*" } | Select-Object -First 1)
    if (-not $dataLine) {
        return $Content | ConvertFrom-Json
    }

    return $dataLine.Substring(5).Trim() | ConvertFrom-Json
}

function New-McpSession {
    param([string]$Url)

    $headers = @{
        Accept = "application/json, text/event-stream"
        "Content-Type" = "application/json"
    }
    $body = @{
        jsonrpc = "2.0"
        id = 1
        method = "initialize"
        params = @{
            protocolVersion = "2025-06-18"
            capabilities = @{}
            clientInfo = @{
                name = "td-mcp-playtest"
                version = "1.0"
            }
        }
    } | ConvertTo-Json -Depth 20

    $response = Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -Body $body -UseBasicParsing -TimeoutSec 20
    $sessionId = $response.Headers["Mcp-Session-Id"]
    if ([string]::IsNullOrWhiteSpace($sessionId)) {
        throw "MCP initialize response did not include Mcp-Session-Id"
    }

    $initializedHeaders = $headers.Clone()
    $initializedHeaders["Mcp-Session-Id"] = $sessionId
    $notification = @{
        jsonrpc = "2.0"
        method = "notifications/initialized"
        params = @{}
    } | ConvertTo-Json -Depth 5

    try {
        Invoke-WebRequest -Uri $Url -Method Post -Headers $initializedHeaders -Body $notification -UseBasicParsing -TimeoutSec 10 | Out-Null
    } catch {
    }

    return $sessionId
}

function Invoke-Mcp {
    param(
        [string]$Url,
        [string]$SessionId,
        [string]$Method,
        [hashtable]$Params,
        [int]$Id = 2,
        [int]$TimeoutSec = 30
    )

    $headers = @{
        Accept = "application/json, text/event-stream"
        "Content-Type" = "application/json"
        "Mcp-Session-Id" = $SessionId
    }
    $body = @{
        jsonrpc = "2.0"
        id = $Id
        method = $Method
        params = $Params
    } | ConvertTo-Json -Depth 80

    $response = Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -Body $body -UseBasicParsing -TimeoutSec $TimeoutSec
    return ConvertFrom-McpEvent $response.Content
}

function Invoke-UnityCode {
    param(
        [string]$Url,
        [string]$SessionId,
        [string]$Code,
        [int]$Id = 100
    )

    return Invoke-Mcp -Url $Url -SessionId $SessionId -Method "tools/call" -Id $Id -TimeoutSec 45 -Params @{
        name = "execute_code"
        arguments = @{
            action = "execute"
            code = $Code
            safety_checks = $true
            compiler = "auto"
        }
    }
}

function Invoke-UnityTool {
    param(
        [string]$Url,
        [string]$SessionId,
        [string]$ToolName,
        [hashtable]$Arguments,
        [int]$Id = 200,
        [int]$TimeoutSec = 30
    )

    return Invoke-Mcp -Url $Url -SessionId $SessionId -Method "tools/call" -Id $Id -TimeoutSec $TimeoutSec -Params @{
        name = $ToolName
        arguments = $Arguments
    }
}

function Get-McpStructuredContent {
    param($Response)

    if ($null -eq $Response -or $null -eq $Response.result) {
        return $null
    }

    return $Response.result.structuredContent
}

function Test-McpToolSuccess {
    param($Response)

    $content = Get-McpStructuredContent -Response $Response
    if ($null -eq $content) {
        return $false
    }

    if ($null -ne $content.PSObject.Properties["success"]) {
        return [bool]$content.success
    }

    if ($null -ne $content.PSObject.Properties["result"] -and
        $null -ne $content.result -and
        $null -ne $content.result.PSObject.Properties["success"]) {
        return [bool]$content.result.success
    }

    return $true
}

function Assert-McpToolSuccess {
    param(
        [string]$Step,
        $Response
    )

    if (Test-McpToolSuccess -Response $Response) {
        return
    }

    $content = Get-McpStructuredContent -Response $Response
    $detail = if ($null -eq $content) { "empty structuredContent" } else { $content | ConvertTo-Json -Depth 12 -Compress }
    throw "$Step failed: $detail"
}

function Wait-UnitySession {
    param(
        [string]$Url,
        [string]$SessionId,
        [int]$TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds([Math]::Max(1, $TimeoutSeconds))
    $lastDetail = "no response"
    do {
        try {
            $probe = Invoke-UnityTool -Url $Url -SessionId $SessionId -ToolName "read_console" -Arguments @{
                action = "get"
                types = @("error")
                count = 1
            } -Id 6 -TimeoutSec 10
            if (Test-McpToolSuccess -Response $probe) {
                return $probe
            }

            $lastDetail = (Get-McpStructuredContent -Response $probe) | ConvertTo-Json -Depth 12 -Compress
        } catch {
            $lastDetail = $_.Exception.Message
        }

        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Unity MCP session was not ready within $TimeoutSeconds seconds. Last response: $lastDetail"
}

function Wait-GameManager {
    param(
        [string]$Url,
        [string]$SessionId,
        [int]$TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds([Math]::Max(1, $TimeoutSeconds))
    $lastDetail = "no response"
    do {
        try {
            $probe = Invoke-UnityCode -Url $Url -SessionId $SessionId -Id 120 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "waiting" : "ready";
'@
            if (Test-McpToolSuccess -Response $probe) {
                $content = Get-McpStructuredContent -Response $probe
                $lastDetail = [string]$content.data.result
                if ($lastDetail -eq "ready") {
                    return $probe
                }
            } else {
                $lastDetail = (Get-McpStructuredContent -Response $probe) | ConvertTo-Json -Depth 12 -Compress
            }
        } catch {
            $lastDetail = $_.Exception.Message
        }

        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "TDGameManager was not ready within $TimeoutSeconds seconds. Last response: $lastDetail"
}

function Escape-CSharpString {
    param([string]$Value)
    return ($Value -replace "\\", "\\") -replace '"', '\"'
}

$summaryDirectory = Split-Path -Parent $SummaryPath
if ($summaryDirectory) {
    New-Item -ItemType Directory -Path $summaryDirectory -Force | Out-Null
}

$sessionId = New-McpSession -Url $McpUrl
Wait-UnitySession -Url $McpUrl -SessionId $sessionId -TimeoutSeconds $UnityReadyTimeoutSeconds | Out-Null

if ($RefreshScripts) {
    $refreshResult = Invoke-UnityTool -Url $McpUrl -SessionId $sessionId -ToolName "refresh_unity" -Arguments @{
        mode = "force"
        scope = "all"
        compile = "request"
        wait_for_ready = $false
    } -Id 7 -TimeoutSec 60
    Assert-McpToolSuccess -Step "refresh Unity scripts" -Response $refreshResult
    Start-Sleep -Seconds 2
    Wait-UnitySession -Url $McpUrl -SessionId $sessionId -TimeoutSeconds $UnityReadyTimeoutSeconds | Out-Null
} else {
    $refreshResult = $null
}

$clearConsoleCode = @'
var logEntries = System.Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
var clear = logEntries == null ? null : logEntries.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
if (clear != null) clear.Invoke(null, null);
return clear != null ? "console cleared" : "console clear unavailable";
'@
$clearConsoleResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $clearConsoleCode -Id 9
Assert-McpToolSuccess -Step "clear console" -Response $clearConsoleResult

$setupCode = @"
UnityEngine.PlayerPrefs.SetInt("td_campaign_selected_level", $LevelIndex);
UnityEngine.PlayerPrefs.Save();
UnityEngine.Application.runInBackground = true;
UnityEditor.PlayerSettings.runInBackground = true;
return "level=$LevelIndex runInBackground=" + UnityEngine.Application.runInBackground;
"@
$setupResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $setupCode -Id 10
Assert-McpToolSuccess -Step "configure playtest" -Response $setupResult

try {
    Invoke-UnityTool -Url $McpUrl -SessionId $sessionId -ToolName "manage_editor" -Arguments @{ action = "stop" } -Id 11 | Out-Null
    Start-Sleep -Seconds 1
} catch {
}

$playResult = Invoke-UnityTool -Url $McpUrl -SessionId $sessionId -ToolName "manage_editor" -Arguments @{ action = "play" } -Id 12
Assert-McpToolSuccess -Step "enter play mode" -Response $playResult
Wait-GameManager -Url $McpUrl -SessionId $sessionId -TimeoutSeconds $UnityReadyTimeoutSeconds | Out-Null

if ($ResetCodex) {
    $resetCodexResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 121 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugResetCodexDiscoveries();
'@
    Assert-McpToolSuccess -Step "reset codex" -Response $resetCodexResult
} else {
    $resetCodexResult = $null
}

if ($BonusBudget -ne 0) {
    $bonusBudgetCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
if (gm == null) return "no TDGameManager";
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var field = gm.GetType().GetField("_defenseBudget", flags);
if (field == null) return "budget field unavailable";
var before = (int)field.GetValue(gm);
field.SetValue(gm, before + $BonusBudget);
return "bonusBudget=$BonusBudget budget=" + (before + $BonusBudget);
"@
    $bonusBudgetResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $bonusBudgetCode -Id 122
    Assert-McpToolSuccess -Step "grant test budget" -Response $bonusBudgetResult
} else {
    $bonusBudgetResult = $null
}

if (-not $SkipAutoBuild) {
    $escapedPlan = Escape-CSharpString $BuildPlan
    $autoBuildCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
if (gm == null) return "no TDGameManager";
var plan = "$escapedPlan";
var built = 0;
var log = new System.Text.StringBuilder();
foreach (var raw in plan.Split(new [] {';'}, System.StringSplitOptions.RemoveEmptyEntries))
{
    var item = raw.Trim();
    var parts = item.Split(':');
    if (parts.Length != 2) continue;
    var xy = parts[0].Split(',');
    if (xy.Length != 2) continue;
    if (!int.TryParse(xy[0], out var x) || !int.TryParse(xy[1], out var y)) continue;
    if (!System.Enum.TryParse<TD.TDTowerKind>(parts[1], true, out var kind)) kind = TD.TDTowerKind.RailLancer;
    var result = gm.DebugBuildTowerAtCell(x, y, kind);
    if (result.StartsWith("built ", System.StringComparison.OrdinalIgnoreCase)) built++;
    log.AppendLine(result);
    }
return "autobuild built=" + built + "\n" + log.ToString();
"@
    $autoBuildResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $autoBuildCode -Id 13
    Assert-McpToolSuccess -Step "auto build" -Response $autoBuildResult
} else {
    $autoBuildResult = $null
}

if (-not [string]::IsNullOrWhiteSpace($UpgradePlan)) {
    $escapedUpgradePlan = Escape-CSharpString $UpgradePlan
    $autoUpgradeCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
if (gm == null) return "no TDGameManager";
var plan = "$escapedUpgradePlan";
var upgraded = 0;
var log = new System.Text.StringBuilder();
foreach (var raw in plan.Split(new [] {';'}, System.StringSplitOptions.RemoveEmptyEntries))
{
    var item = raw.Trim();
    var parts = item.Split(':');
    if (parts.Length != 2) continue;
    var xy = parts[0].Split(',');
    if (xy.Length != 2) continue;
    if (!int.TryParse(xy[0], out var x) || !int.TryParse(xy[1], out var y)) continue;
    foreach (var branchRaw in parts[1].Split(new [] {',', '+', '>'}, System.StringSplitOptions.RemoveEmptyEntries))
    {
        var branchToken = branchRaw.Trim();
        TD.TDTowerUpgradeBranch branch;
        if (branchToken.Equals("D", System.StringComparison.OrdinalIgnoreCase) ||
            branchToken.Equals("Damage", System.StringComparison.OrdinalIgnoreCase))
        {
            branch = TD.TDTowerUpgradeBranch.Damage;
        }
        else if (branchToken.Equals("U", System.StringComparison.OrdinalIgnoreCase) ||
                 branchToken.Equals("Utility", System.StringComparison.OrdinalIgnoreCase))
        {
            branch = TD.TDTowerUpgradeBranch.Utility;
        }
        else
        {
            log.AppendLine("skip: unknown branch " + branchToken + " at " + x + "," + y);
            continue;
        }

        var result = gm.DebugUpgradeTowerAtCell(x, y, branch);
        if (result.StartsWith("upgraded ", System.StringComparison.OrdinalIgnoreCase)) upgraded++;
        log.AppendLine(result);
    }
}
return "autoupgrade upgraded=" + upgraded + "\n" + log.ToString();
"@
    $autoUpgradeResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $autoUpgradeCode -Id 130
    Assert-McpToolSuccess -Step "auto upgrade" -Response $autoUpgradeResult
} else {
    $autoUpgradeResult = $null
}

Start-Sleep -Seconds 2
if (-not $SkipStartWave) {
    $startWaveResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugRequestStartWave();
'@ -Id 131
    Assert-McpToolSuccess -Step "start wave" -Response $startWaveResult
} else {
    $startWaveResult = $null
}

if (-not [string]::IsNullOrWhiteSpace($ResonanceCommand)) {
    $escapedResonanceCommand = Escape-CSharpString $ResonanceCommand
    $resonanceCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
if (gm == null) return "no TDGameManager";
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var chargeField = gm.GetType().GetField("_resonanceCharge", flags);
var commandType = gm.GetType().GetNestedType("TDResonanceCommand", System.Reflection.BindingFlags.NonPublic);
var beginMethod = gm.GetType().GetMethod("BeginResonanceWindow", flags);
var selectMethod = gm.GetType().GetMethod("TrySelectResonanceCommand", flags);
if (chargeField == null || commandType == null || beginMethod == null || selectMethod == null) return "resonance debug hooks unavailable";
chargeField.SetValue(gm, 100f);
beginMethod.Invoke(gm, null);
var command = System.Enum.Parse(commandType, "$escapedResonanceCommand", true);
selectMethod.Invoke(gm, new [] { command });
return "resonanceCommand=$escapedResonanceCommand";
"@
    $resonanceResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $resonanceCode -Id 132
    Assert-McpToolSuccess -Step "trigger resonance" -Response $resonanceResult
} else {
    $resonanceResult = $null
}

if ($DurationSeconds -gt 0) {
    Start-Sleep -Seconds $DurationSeconds
}

$escapedScreenshotPath = Escape-CSharpString $ScreenshotPath
$stateCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
UnityEngine.ScreenCapture.CaptureScreenshot("$escapedScreenshotPath");
var enemies = UnityEngine.Object.FindObjectsByType<TD.TDEnemy>(UnityEngine.FindObjectsSortMode.None);
var towers = UnityEngine.Object.FindObjectsByType<TD.TDTower>(UnityEngine.FindObjectsSortMode.None);
var sb = new System.Text.StringBuilder();
sb.AppendLine("screenshot=$escapedScreenshotPath");
sb.AppendLine("frame=" + UnityEngine.Time.frameCount + " enemies=" + enemies.Length + " towers=" + towers.Length);

var uiRoot = UnityEngine.GameObject.Find("TD Battle UI");
var uiObjects = new System.Collections.Generic.Dictionary<string, UnityEngine.GameObject>(System.StringComparer.Ordinal);
if (uiRoot != null)
{
    foreach (var child in uiRoot.GetComponentsInChildren<UnityEngine.Transform>(true))
    {
        if (child != null && !uiObjects.ContainsKey(child.name)) uiObjects.Add(child.name, child.gameObject);
    }
}

var requiredUi = new []
{
    "Primary HUD", "Start Wave Button", "Wave Intel", "Tactical Feed",
    "Tower Build Bar", "Tower Upgrade Panel", "Run Result Scrim", "Run Result"
};
var missingUi = new System.Collections.Generic.List<string>();
foreach (var name in requiredUi)
{
    if (!uiObjects.TryGetValue(name, out var requiredObject) || requiredObject == null)
    {
        missingUi.Add(name);
        continue;
    }

    sb.AppendLine("uiActive." + name.Replace(' ', '_') + "=" + requiredObject.activeInHierarchy);
}
sb.AppendLine("uiMissing=" + missingUi.Count + (missingUi.Count == 0 ? "" : " names=" + string.Join(",", missingUi)));

var panelNames = new [] {"Primary HUD", "Wave Intel", "Tactical Feed", "Tower Build Bar", "Tower Upgrade Panel", "Run Result"};
var activePanels = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, UnityEngine.Rect>>();
var outOfBounds = new System.Collections.Generic.List<string>();
foreach (var name in panelNames)
{
    if (!uiObjects.TryGetValue(name, out var panel) || panel == null || !panel.activeInHierarchy) continue;
    var rectTransform = panel.GetComponent<UnityEngine.RectTransform>();
    if (rectTransform == null) continue;
    var corners = new UnityEngine.Vector3[4];
    rectTransform.GetWorldCorners(corners);
    var rect = UnityEngine.Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    activePanels.Add(new System.Collections.Generic.KeyValuePair<string, UnityEngine.Rect>(name, rect));
    if (rect.xMin < -1f || rect.yMin < -1f || rect.xMax > UnityEngine.Screen.width + 1f || rect.yMax > UnityEngine.Screen.height + 1f)
    {
        outOfBounds.Add(name);
    }
    sb.AppendLine("uiPanel." + name.Replace(' ', '_') + "=" + rect.xMin.ToString("0") + "," + rect.yMin.ToString("0") + "," + rect.width.ToString("0") + "x" + rect.height.ToString("0"));
}

var overlaps = new System.Collections.Generic.List<string>();
for (var i = 0; i < activePanels.Count; i++)
{
    for (var j = i + 1; j < activePanels.Count; j++)
    {
        if (activePanels[i].Key == "Run Result" || activePanels[j].Key == "Run Result") continue;
        if (activePanels[i].Value.Overlaps(activePanels[j].Value))
        {
            overlaps.Add(activePanels[i].Key + "/" + activePanels[j].Key);
        }
    }
}
sb.AppendLine("uiOutOfBounds=" + outOfBounds.Count + (outOfBounds.Count == 0 ? "" : " names=" + string.Join(",", outOfBounds)));
sb.AppendLine("uiOverlaps=" + (overlaps.Count == 0 ? "none" : string.Join(",", overlaps)));

var criticalTextNames = new []
{
    "Campaign", "Guide", "Wave Intel Body", "Wave Intel Enemy", "Wave Intel Profile",
    "Wave Intel Route", "Wave Intel Counter", "Wave Intel Readiness", "Tower Stats",
    "Tower Upgrade Preview", "Tower Upgrade Hint", "Resonance Label", "Run Result Body",
    "Run Result Failure", "Run Result Recap", "Run Result Recommendation"
};
var textOverflow = new System.Collections.Generic.List<string>();
foreach (var name in criticalTextNames)
{
    if (!uiObjects.TryGetValue(name, out var textObject) || textObject == null || !textObject.activeInHierarchy) continue;
    var label = textObject.GetComponent<UnityEngine.UI.Text>();
    if (label == null) continue;
    var value = (label.text ?? string.Empty).Replace("\r", " ").Replace("\n", " | ");
    sb.AppendLine("uiText." + name.Replace(' ', '_') + "=" + value);
    if (label.preferredHeight > label.rectTransform.rect.height + 1.5f) textOverflow.Add(name);
}
sb.AppendLine("uiTextOverflow=" + textOverflow.Count + (textOverflow.Count == 0 ? "" : " names=" + string.Join(",", textOverflow)));

var startButtonLabel = uiObjects.TryGetValue("Start Wave Button", out var startButtonObject) && startButtonObject != null
    ? startButtonObject.GetComponentInChildren<UnityEngine.UI.Text>(true)
    : null;
var startButton = startButtonObject != null ? startButtonObject.GetComponent<UnityEngine.UI.Button>() : null;
sb.AppendLine("startButton=" + (startButtonLabel == null ? "<missing>" : startButtonLabel.text) + " interactable=" + (startButton != null && startButton.interactable));

var routeLines = UnityEngine.Object.FindObjectsByType<UnityEngine.LineRenderer>(UnityEngine.FindObjectsSortMode.None);
var visibleRouteLines = 0;
for (var i = 0; i < routeLines.Length; i++)
{
    if (routeLines[i] != null && routeLines[i].name.StartsWith("RoutePreview_", System.StringComparison.Ordinal) && routeLines[i].enabled) visibleRouteLines++;
}
sb.AppendLine("routePreviewVisible=" + visibleRouteLines);

var rangePreviewObject = UnityEngine.GameObject.Find("RangePreview");
var rangePreviewRenderer = rangePreviewObject != null ? rangePreviewObject.GetComponent<UnityEngine.SpriteRenderer>() : null;
sb.AppendLine("rangePreviewVisible=" + (rangePreviewRenderer != null && rangePreviewRenderer.enabled));
if (gm != null)
{
    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
    var type = gm.GetType();
    foreach (var name in new [] {"_wave", "_wavesCleared", "_lineIntegrity", "_defenseBudget", "_gameOver", "_victory", "_lastStatus", "_isInPrepPhase", "_waveStartRequested", "_totalKills", "_totalEscapes", "_totalDamageDealt", "_totalIntegrityDamageTaken", "_lastWaveStartReadinessScore", "_lastWaveStartReadinessGrade", "_resonanceCharge", "_resonanceWindowTimer", "_resonanceWindowsTriggered", "_resonanceCommandsUsed", "_emberSurgeUses", "_fractureMarkUses", "_resonanceBonusDamage", "_codexDiscoveriesThisRun", "_budgetSpentOnBuilds", "_budgetSpentOnUpgrades", "_upgradesPurchased"})
    {
        var field = type.GetField(name, flags);
        var value = field == null ? null : field.GetValue(gm);
        sb.AppendLine(name + "=" + (value == null ? "<null>" : value.ToString()));
    }

    var eventsField = type.GetField("_tacticalEvents", flags);
    var events = eventsField == null ? null : eventsField.GetValue(gm) as System.Collections.IEnumerable;
    if (events != null)
    {
        var eventIndex = 0;
        foreach (var item in events)
        {
            if (item == null) continue;
            var messageField = item.GetType().GetField("message", flags);
            var message = messageField == null ? "<unknown>" : messageField.GetValue(item);
            sb.AppendLine("event" + eventIndex + "=" + message);
            eventIndex++;
        }
    }
}

for (var i = 0; i < towers.Length; i++)
{
    var tower = towers[i];
    if (tower == null) continue;
    sb.AppendLine(
        "tower" + i + "=" +
        tower.DisplayName +
        " tier=" + tower.Tier +
        " spec=" + tower.SpecializationLabel +
        " effect=" + tower.SpecializationEffectLabel +
        " D" + tower.DamageBranchCount + "/U" + tower.UtilityBranchCount);
}
return sb.ToString();
"@
$stateResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $stateCode -Id 14
Assert-McpToolSuccess -Step "capture state" -Response $stateResult
Start-Sleep -Seconds 2

$consoleIssuesResult = Invoke-UnityTool -Url $McpUrl -SessionId $sessionId -ToolName "read_console" -Arguments @{
    action = "get"
    types = @("error", "warning")
    count = 120
} -Id 15
Assert-McpToolSuccess -Step "read console issues" -Response $consoleIssuesResult

$consoleLogResult = Invoke-UnityTool -Url $McpUrl -SessionId $sessionId -ToolName "read_console" -Arguments @{
    action = "get"
    types = @("log")
    count = 120
} -Id 151
Assert-McpToolSuccess -Step "read console logs" -Response $consoleLogResult

$autoBuildText = if ($null -eq $autoBuildResult) { "" } else { [string](Get-McpStructuredContent -Response $autoBuildResult).data.result }
$autoUpgradeText = if ($null -eq $autoUpgradeResult) { "" } else { [string](Get-McpStructuredContent -Response $autoUpgradeResult).data.result }
$startWaveText = if ($null -eq $startWaveResult) { "" } else { [string](Get-McpStructuredContent -Response $startWaveResult).data.result }
$stateText = [string](Get-McpStructuredContent -Response $stateResult).data.result
$consoleIssueContent = Get-McpStructuredContent -Response $consoleIssuesResult
$consoleIssueEntries = @($consoleIssueContent.data)
$ignoredConsoleIssuePatterns = @(
    "MCP-FOR-UNITY.*Unexpected receive error: WebSocket is not initialised"
)
$ignoredConsoleIssues = @()
$effectiveConsoleIssues = @()
foreach ($entry in $consoleIssueEntries) {
    $ignored = $false
    foreach ($pattern in $ignoredConsoleIssuePatterns) {
        if ([string]$entry -match $pattern) {
            $ignored = $true
            break
        }
    }

    if ($ignored) {
        $ignoredConsoleIssues += $entry
    } else {
        $effectiveConsoleIssues += $entry
    }
}
$consoleIssueCount = $effectiveConsoleIssues.Count

$plannedBuildCount = if ($SkipAutoBuild) { 0 } else { @($BuildPlan -split ";" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count }
$actualBuildCount = if ($autoBuildText -match "autobuild built=(\d+)") { [int]$Matches[1] } else { -1 }

$plannedUpgradeCount = 0
if (-not [string]::IsNullOrWhiteSpace($UpgradePlan)) {
    foreach ($upgradeItem in $UpgradePlan -split ";") {
        $upgradeParts = $upgradeItem -split ":", 2
        if ($upgradeParts.Count -ne 2) {
            continue
        }

        $plannedUpgradeCount += @($upgradeParts[1] -split "[,\+>]" | Where-Object { $_.Trim() -match "^(D|Damage|U|Utility)$" }).Count
    }
}
$actualUpgradeCount = if ($autoUpgradeText -match "autoupgrade upgraded=(\d+)") { [int]$Matches[1] } elseif ($plannedUpgradeCount -eq 0) { 0 } else { -1 }

$expectationFailures = @()
foreach ($token in $ExpectState -split ";") {
    $trimmedToken = $token.Trim()
    if ($trimmedToken -and -not $stateText.Contains($trimmedToken)) {
        $expectationFailures += $trimmedToken
    }
}

$checks = [ordered]@{
    unitySession = $true
    gameManager = $stateText.Contains("_wave=")
    autoBuild = $SkipAutoBuild -or $actualBuildCount -eq $plannedBuildCount
    autoUpgrade = $actualUpgradeCount -eq $plannedUpgradeCount
    startWave = $SkipStartWave -or $startWaveText.StartsWith("start requested ", [StringComparison]::OrdinalIgnoreCase)
    uiRequired = $stateText.Contains("uiMissing=0")
    uiBounds = $stateText.Contains("uiOutOfBounds=0")
    uiOverlap = $stateText.Contains("uiOverlaps=none")
    uiTextFit = $stateText.Contains("uiTextOverflow=0")
    screenshot = (Test-Path -LiteralPath $ScreenshotPath) -and (Get-Item -LiteralPath $ScreenshotPath).LastWriteTimeUtc -ge $runStartedUtc.AddSeconds(-1)
    consoleClean = $AllowConsoleIssues -or $consoleIssueCount -eq 0
    expectedState = $expectationFailures.Count -eq 0
}

if (-not $KeepPlaying) {
    Invoke-UnityTool -Url $McpUrl -SessionId $sessionId -ToolName "manage_editor" -Arguments @{ action = "stop" } -Id 16 | Out-Null
}

$summary = [ordered]@{
    levelIndex = $LevelIndex
    durationSeconds = $DurationSeconds
    buildPlan = if ($SkipAutoBuild) { "<manual>" } else { $BuildPlan }
    upgradePlan = $UpgradePlan
    bonusBudget = $BonusBudget
    resonanceCommand = $ResonanceCommand
    screenshotPath = $ScreenshotPath
    checks = $checks
    expectationFailures = $expectationFailures
    resetCodex = Get-McpStructuredContent -Response $resetCodexResult
    refresh = Get-McpStructuredContent -Response $refreshResult
    bonusBudgetResult = Get-McpStructuredContent -Response $bonusBudgetResult
    resonanceResult = Get-McpStructuredContent -Response $resonanceResult
    autoBuild = Get-McpStructuredContent -Response $autoBuildResult
    autoUpgrade = Get-McpStructuredContent -Response $autoUpgradeResult
    startWave = Get-McpStructuredContent -Response $startWaveResult
    state = Get-McpStructuredContent -Response $stateResult
    consoleIssues = $consoleIssueContent
    ignoredConsoleIssues = $ignoredConsoleIssues
    effectiveConsoleIssues = $effectiveConsoleIssues
    consoleLogs = Get-McpStructuredContent -Response $consoleLogResult
}

$summary | ConvertTo-Json -Depth 40 | Set-Content -Path $SummaryPath -Encoding UTF8
$summary | ConvertTo-Json -Depth 40

$failedChecks = @($checks.GetEnumerator() | Where-Object { -not [bool]$_.Value } | ForEach-Object { $_.Key })
if ($failedChecks.Count -gt 0) {
    throw "Playtest regression checks failed: $($failedChecks -join ', '). See $SummaryPath"
}
