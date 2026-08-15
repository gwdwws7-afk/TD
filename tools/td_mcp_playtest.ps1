param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [int]$LevelIndex = 1,
    [int]$DurationSeconds = 45,
    [float]$TimeScale = 1.0,
    [int]$RandomSeed = 1337,
    [int]$UnityReadyTimeoutSeconds = 45,
    [int]$ViewportWidth = 0,
    [int]$ViewportHeight = 0,
    # Default plan targets grayline_junction (L1): (4,6) entry + (9,4) core + (11,4) exit.
    # All three are authored build sites within RailLancer range 3.0 of the path,
    # covering 13/16 path cells. The previous default (1,1;4,2;8,3) used non-authored
    # cells that autoBuild silently skipped, and (8,3) had no path coverage at all.
    [string]$BuildPlan = "4,6:RailLancer;9,4:RailLancer;11,4:RailLancer",
    [string]$UpgradePlan = "",
    [int]$BonusBudget = 0,
    [int]$MinTacticalScore = -1,
    [int]$MaxTacticalScore = -1,
    [ValidateSet("", "EmberSurge", "FractureMark")]
    [string]$ResonanceCommand = "",
    [ValidateSet("Adaptive", "EmberSurge", "FractureMark")]
    [string]$FormationDoctrine = "Adaptive",
    [ValidateSet("Standard", "Veteran", "EmberTrial")]
    [string]$FormationDifficulty = "Standard",
    [string]$EnemyPlan = "",
    [string]$ExpectUltimateId = "",
    [int]$MinUltimateProcs = 1,
    [int]$MinMatrixFullMatches = 0,
    [int]$MinConvergenceTriggers = 0,
    [string]$ExpectState = "",
    [string]$ScreenshotPath = "E:/TD/output/playtest/mcp_autorun_latest.png",
    [string]$SummaryPath = "E:/TD/output/playtest/mcp_autorun_latest.json",
    [switch]$SkipAutoBuild,
    [switch]$SkipStartWave,
    [switch]$FreezeConfiguredWaves,
    [switch]$ForceRunResult,
    [switch]$ForceVictoryResult,
    [switch]$KeepMissionBoardOpen,
    [switch]$KeepFormationOpen,
    [switch]$KeepCampaignProfileOpen,
    [switch]$PrepareP84ChapterBoard,
    [switch]$PrepareP84CampaignCompletion,
    [switch]$PrepareP85Difficulty,
    [switch]$PrepareP85CampaignPerfected,
    [switch]$PrepareP86Scenario,
    [switch]$PrepareP9Presentation,
    [switch]$PrepareP112Presentation,
    [switch]$PrepareP112Combat,
    [switch]$PrepareP113Presentation,
    [switch]$PrepareP133Combat,
    [switch]$PrepareP134Combat,
    [switch]$PrepareP121Presentation,
    [switch]$PrepareP122Exam,
    [switch]$PrepareP123Campaign,
    [switch]$PrepareP123Settings,
    [switch]$PrepareP123Formation,
    [switch]$PrepareP123Profile,
    [ValidateSet("English", "Chinese")]
    [string]$P123Language = "English",
    [switch]$PrepareP101Meta,
    [switch]$RunCampaignProgressAudit,
    [switch]$RunP84Audit,
    [switch]$RunP85Audit,
    [switch]$RunP86Audit,
    [switch]$RunP9Audit,
    [switch]$RunP111Audit,
    [switch]$RunP112Audit,
    [switch]$RunP113Audit,
    [switch]$RunP133Audit,
    [switch]$RunP134Audit,
    [switch]$RunP120GeometryAudit,
    [switch]$RunP121Audit,
    [switch]$RunP122Audit,
    [switch]$RunP123Audit,
    [switch]$RunP124Audit,
    [switch]$RunP125EconomyAudit,
    [switch]$RunP101Audit,
    [switch]$RunP102Audit,
    [ValidateSet("", "focused_fire", "control_lattice", "adaptive_network")]
    [string]$P124AutoplayStrategy = "",
    [ValidateRange(0, 2)]
    [int]$P124SiteVariant = 0,
    [ValidateRange(15, 300)]
    [int]$P124MaxRealSeconds = 95,
    [string]$P124RunReportPath = "E:/TD/output/playtest/p124_real_run_latest.json",
    [ValidateSet("", "adaptive", "engage", "hold")]
    [string]$P135MechanicPolicy = "",
    [string]$P135RunReportPath = "E:/TD/output/playtest/p135_real_run_latest.json",
    [switch]$ResetCodex,
    [switch]$RefreshScripts,
    [switch]$AllowConsoleIssues,
    [switch]$WaitFullDuration,
    [switch]$PreserveCampaignProgress,
    [switch]$KeepPlaying
)

$ErrorActionPreference = "Stop"
$runStartedUtc = [DateTime]::UtcNow
$p124Enabled = -not [string]::IsNullOrWhiteSpace($P124AutoplayStrategy)
$p135Enabled = -not [string]::IsNullOrWhiteSpace($P135MechanicPolicy)
if ($p135Enabled -and -not $p124Enabled) {
    throw "P135MechanicPolicy requires P124AutoplayStrategy because P13.5 measures complete runtime autoplay."
}
if ($PreserveCampaignProgress -and $KeepPlaying) {
    throw "PreserveCampaignProgress cannot be combined with KeepPlaying because restoration requires Edit Mode."
}

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
        [int]$TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds([Math]::Max(1, $TimeoutSeconds))
    $lastDetail = "no response"
    do {
        try {
            # Entering play mode can reload Unity's domain and invalidate the HTTP
            # session that issued the play command. Probe with a fresh session so
            # automation follows the reconnected editor bridge.
            $candidateSessionId = New-McpSession -Url $Url
            $probe = Invoke-UnityCode -Url $Url -SessionId $candidateSessionId -Id 120 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "waiting" : "ready";
'@
            if (Test-McpToolSuccess -Response $probe) {
                $content = Get-McpStructuredContent -Response $probe
                $lastDetail = [string]$content.data.result
                if ($lastDetail -eq "ready") {
                    return $candidateSessionId
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

$profileSnapshotEncoded = ""
$profileSelectedLevel = 1
if ($PreserveCampaignProgress) {
    $profileCaptureResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 140 -Code @'
var snapshot = TD.TDCampaignProgression.ExportSnapshot(20);
var encoded = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(snapshot));
var selected = UnityEngine.PlayerPrefs.GetInt("td_campaign_selected_level", 1);
return selected + "|" + encoded;
'@
    Assert-McpToolSuccess -Step "capture campaign profile" -Response $profileCaptureResult
    $profileCaptureContent = Get-McpStructuredContent -Response $profileCaptureResult
    $profileCaptureText = [string]$profileCaptureContent.data.result
    $profileCaptureParts = $profileCaptureText -split "\|", 2
    if ($profileCaptureParts.Count -ne 2 -or -not [int]::TryParse($profileCaptureParts[0], [ref]$profileSelectedLevel)) {
        throw "Campaign profile snapshot was malformed: $profileCaptureText"
    }

    $profileSnapshotEncoded = $profileCaptureParts[1]
} else {
    $profileCaptureResult = $null
}

$timeScaleLiteral = $TimeScale.ToString([System.Globalization.CultureInfo]::InvariantCulture)
$setupCode = @"
UnityEngine.PlayerPrefs.SetInt("td_campaign_selected_level", $LevelIndex);
UnityEngine.PlayerPrefs.Save();
UnityEngine.Application.runInBackground = true;
UnityEditor.PlayerSettings.runInBackground = true;
UnityEditor.EditorSettings.enterPlayModeOptionsEnabled = true;
UnityEditor.EditorSettings.enterPlayModeOptions = UnityEditor.EnterPlayModeOptions.DisableDomainReload;
return "level=$LevelIndex runInBackground=" + UnityEngine.Application.runInBackground +
       " enterPlayModeOptions=" + UnityEditor.EditorSettings.enterPlayModeOptions;
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
$sessionId = Wait-GameManager -Url $McpUrl -TimeoutSeconds $UnityReadyTimeoutSeconds

$runtimeSetupCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
if (gm != null)
{
    // Dismiss the title screen and force-deploy for automation.
    // The wave loop coroutine has a 60s timeout safeguard that will also
    // force-resume if _campaignDeploymentConfirmed is never set.
    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    var t = typeof(TD.TDGameManager);
    var tsField = t.GetField("_titleScreen", flags);
    var ts = tsField != null ? tsField.GetValue(gm) : null;
    if (ts != null)
    {
        var tsType = ts.GetType();
        tsType.GetMethod("Hide")?.Invoke(ts, null);
    }
    var dcField = t.GetField("_campaignDeploymentConfirmed", flags);
    if (dcField != null) dcField.SetValue(gm, true);
    var mbField = t.GetField("_missionBoardOpen", flags);
    if (mbField != null) mbField.SetValue(gm, false);
    var ffField = t.GetField("_formationPanelOpen", flags);
    if (ffField != null) ffField.SetValue(gm, false);
    var cpField = t.GetField("_campaignProfileOpen", flags);
    if (cpField != null) cpField.SetValue(gm, false);
    // Restart the wave loop coroutine if it died during the title-screen wait.
    gm.GetType().GetMethod("EnsureWaveRoutineRunning", flags | System.Reflection.BindingFlags.NonPublic)?.Invoke(gm, null);
}
UnityEngine.Random.InitState($RandomSeed);
UnityEngine.Time.timeScale = 1f;
UnityEngine.QualitySettings.vSyncCount = 0;
UnityEngine.Application.targetFrameRate = 120;
var viewportStatus = "current";
if ($ViewportWidth > 0 && $ViewportHeight > 0)
{
    try
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var editorAssembly = typeof(UnityEditor.Editor).Assembly;
        var sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
        var singletonType = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
        var instance = singletonType.GetProperty("instance", flags).GetValue(null);
        var groupType = sizesType.GetProperty("currentGroupType", flags).GetValue(instance);
        var group = sizesType.GetMethod("GetGroup", flags).Invoke(instance, new [] { groupType });
        var groupRuntimeType = group.GetType();
        var displayTexts = (string[])groupRuntimeType.GetMethod("GetDisplayTexts", flags).Invoke(group, null);
        var label = "$ViewportWidth" + "x" + "$ViewportHeight" + " P9";
        var selectedIndex = -1;
        for (var i = 0; i < displayTexts.Length; i++)
        {
            if (displayTexts[i] != null && displayTexts[i].Contains(label)) { selectedIndex = i; break; }
        }
        if (selectedIndex < 0)
        {
            var sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
            var sizeKindType = editorAssembly.GetType("UnityEditor.GameViewSizeType");
            var fixedKind = System.Enum.Parse(sizeKindType, "FixedResolution");
            var constructors = sizeType.GetConstructors(flags);
            System.Reflection.ConstructorInfo constructor = null;
            for (var i = 0; i < constructors.Length; i++)
            {
                if (constructors[i].GetParameters().Length == 4) { constructor = constructors[i]; break; }
            }
            if (constructor == null) throw new System.InvalidOperationException("GameViewSize constructor unavailable");
            var size = constructor.Invoke(new object[] { fixedKind, $ViewportWidth, $ViewportHeight, label });
            groupRuntimeType.GetMethod("AddCustomSize", flags).Invoke(group, new [] { size });
            displayTexts = (string[])groupRuntimeType.GetMethod("GetDisplayTexts", flags).Invoke(group, null);
            selectedIndex = displayTexts.Length - 1;
        }
        var gameViewType = editorAssembly.GetType("UnityEditor.GameView");
        var gameView = UnityEditor.EditorWindow.GetWindow(gameViewType);
        gameViewType.GetProperty("selectedSizeIndex", flags).SetValue(gameView, selectedIndex);
        gameView.Repaint();
        viewportStatus = "selected:" + label;
    }
    catch (System.Exception ex)
    {
        UnityEngine.Screen.SetResolution($ViewportWidth, $ViewportHeight, false);
        viewportStatus = "fallback:" + ex.GetType().Name;
    }
}
return "seed=$RandomSeed setupTimeScale=" + UnityEngine.Time.timeScale + " targetFrameRate=" + UnityEngine.Application.targetFrameRate + " viewport=" + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + " status=" + viewportStatus;
"@
$runtimeSetupResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $runtimeSetupCode -Id 119
Assert-McpToolSuccess -Step "configure runtime determinism" -Response $runtimeSetupResult
if ($ViewportWidth -gt 0 -and $ViewportHeight -gt 0) {
    Start-Sleep -Milliseconds 650
}

if ($p124Enabled) {
    $p124ProgressionResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 155 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPrepareP124RepresentativeProgressionForTest();
'@
    Assert-McpToolSuccess -Step "prepare P12.4 representative progression" -Response $p124ProgressionResult
    Start-Sleep -Milliseconds 300
} else {
    $p124ProgressionResult = $null
}

if ($PrepareP86Scenario) {
    $p86ScenarioResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 145 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugActivateP86ScenarioForTest();
'@
    Assert-McpToolSuccess -Step "activate P8.6 scenario mechanic" -Response $p86ScenarioResult
    $p86ScenarioContent = Get-McpStructuredContent -Response $p86ScenarioResult
    $p86ScenarioApplied = ([string]$p86ScenarioContent.data.result).Contains("p8.6.fixture.applied=True")
    Start-Sleep -Milliseconds 300
} else {
    $p86ScenarioResult = $null
    $p86ScenarioApplied = $true
}

if ($PrepareP85Difficulty) {
    $escapedFormationDifficulty = Escape-CSharpString $FormationDifficulty
    $p85DifficultyResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 144 -Code @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPrepareP85DifficultyForTest("$escapedFormationDifficulty");
"@
    Assert-McpToolSuccess -Step "prepare P8.5 difficulty" -Response $p85DifficultyResult
    Start-Sleep -Milliseconds 300
} else {
    $p85DifficultyResult = $null
}

if ($PrepareP84ChapterBoard) {
    $p84ChapterBoardResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 141 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPrepareP84ChapterBoardForTest();
'@
    Assert-McpToolSuccess -Step "prepare P8.4 chapter board" -Response $p84ChapterBoardResult
    Start-Sleep -Milliseconds 300
} else {
    $p84ChapterBoardResult = $null
}

if ($KeepCampaignProfileOpen) {
    $campaignProfileOpenResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 142 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugOpenCampaignProfileForTest();
'@
    Assert-McpToolSuccess -Step "open campaign profile" -Response $campaignProfileOpenResult
    Start-Sleep -Milliseconds 300
} else {
    $campaignProfileOpenResult = $null
}

if ($PrepareP84CampaignCompletion) {
    $p84CampaignCompletionResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 143 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPrepareP84CampaignCompletionForTest();
'@
    Assert-McpToolSuccess -Step "prepare P8.4 campaign completion" -Response $p84CampaignCompletionResult
    Start-Sleep -Milliseconds 300
} else {
    $p84CampaignCompletionResult = $null
}

if ($PrepareP85CampaignPerfected) {
    $p85CampaignPerfectedResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 145 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPrepareP85CampaignPerfectedForTest();
'@
    Assert-McpToolSuccess -Step "prepare P8.5 campaign perfected" -Response $p85CampaignPerfectedResult
    Start-Sleep -Milliseconds 300
} else {
    $p85CampaignPerfectedResult = $null
}

if ($PrepareP101Meta) {
    $p101MetaResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 152 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPrepareP101MetaForTest();
'@
    Assert-McpToolSuccess -Step "prepare P10.1 meta progression" -Response $p101MetaResult
    if ($KeepCampaignProfileOpen) {
        $campaignProfileOpenResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 153 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugOpenCampaignProfileForTest();
'@
        Assert-McpToolSuccess -Step "reopen P10.1 campaign profile" -Response $campaignProfileOpenResult
    }
    Start-Sleep -Milliseconds 300
} else {
    $p101MetaResult = $null
}

if (-not $KeepMissionBoardOpen -and -not $KeepFormationOpen -and -not $KeepCampaignProfileOpen -and
    -not $PrepareP84ChapterBoard -and -not $PrepareP84CampaignCompletion -and -not $PrepareP85CampaignPerfected -and -not $PrepareP101Meta) {
    $missionDeployResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 118 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugDeployCurrentMissionForTest();
'@
    Assert-McpToolSuccess -Step "deploy current mission" -Response $missionDeployResult
    Start-Sleep -Milliseconds 1400
} else {
    $missionDeployResult = $null
}

if ($KeepFormationOpen) {
    $formationOpenResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 138 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugOpenFormationForTest();
'@
    Assert-McpToolSuccess -Step "open prebattle formation" -Response $formationOpenResult
    Start-Sleep -Milliseconds 300
} else {
    $formationOpenResult = $null
}

if (-not $p124Enabled -and -not $SkipAutoBuild -and -not $KeepFormationOpen -and -not $KeepCampaignProfileOpen -and
    -not $PrepareP84ChapterBoard -and -not $PrepareP84CampaignCompletion -and -not $PrepareP85CampaignPerfected -and -not $PrepareP101Meta) {
    $escapedFormationPlan = Escape-CSharpString $BuildPlan
    $escapedFormationDoctrine = Escape-CSharpString $FormationDoctrine
    $escapedFormationDifficulty = Escape-CSharpString $FormationDifficulty
    $formationSetupCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
if (gm == null) return "no TDGameManager";
var kinds = new System.Collections.Generic.List<string>();
foreach (var raw in "$escapedFormationPlan".Split(new [] {';'}, System.StringSplitOptions.RemoveEmptyEntries))
{
    var parts = raw.Trim().Split(':');
    if (parts.Length != 2) continue;
    if (!System.Enum.TryParse<TD.TDTowerKind>(parts[1], true, out var kind)) continue;
    var name = kind.ToString();
    if (!kinds.Contains(name)) kinds.Add(name);
}
return gm.DebugConfigureFormationForTest(string.Join(",", kinds), "$escapedFormationDoctrine", "$escapedFormationDifficulty");
"@
    $formationSetupResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $formationSetupCode -Id 139
    Assert-McpToolSuccess -Step "configure test formation" -Response $formationSetupResult
} else {
    $formationSetupResult = $null
}

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

if (-not $p124Enabled -and -not $SkipAutoBuild) {
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

if ($FreezeConfiguredWaves) {
    $freezeWavesResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 136 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPauseConfiguredWavesForTest();
'@
    Assert-McpToolSuccess -Step "freeze configured waves" -Response $freezeWavesResult
} else {
    $freezeWavesResult = $null
}

if (-not $p124Enabled -and -not $SkipStartWave) {
    $startDeadline = [DateTime]::UtcNow.AddSeconds(6)
    do {
        $startWaveResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugRequestStartWave();
'@ -Id 131
        Assert-McpToolSuccess -Step "start wave" -Response $startWaveResult
        $startWaveContent = Get-McpStructuredContent -Response $startWaveResult
        $startWaveProbeText = [string]$startWaveContent.data.result
        if (-not $startWaveProbeText.StartsWith("skip: not in prep", [StringComparison]::OrdinalIgnoreCase)) {
            break
        }

        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $startDeadline)
} else {
    $startWaveResult = $null
}

if (-not [string]::IsNullOrWhiteSpace($ResonanceCommand)) {
    $escapedResonanceCommand = Escape-CSharpString $ResonanceCommand
    $resonanceCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugActivateResonanceCommand("$escapedResonanceCommand");
"@
    $resonanceResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $resonanceCode -Id 132
    Assert-McpToolSuccess -Step "trigger resonance" -Response $resonanceResult
} else {
    $resonanceResult = $null
}

if (-not [string]::IsNullOrWhiteSpace($EnemyPlan)) {
    $escapedEnemyPlan = Escape-CSharpString $EnemyPlan
    $enemySpawnCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
if (gm == null) return "no TDGameManager";
var plan = "$escapedEnemyPlan";
var spawned = 0;
var log = new System.Text.StringBuilder();
foreach (var raw in plan.Split(new [] {';'}, System.StringSplitOptions.RemoveEmptyEntries))
{
    var parts = raw.Trim().Split(':');
    if (parts.Length < 2) continue;
    var enemyId = parts[0].Trim();
    if (!int.TryParse(parts[1], out var count)) count = 1;
    var spawnCount = System.Math.Min(64, System.Math.Max(1, count));
    var lane = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2].Trim() : "default";
    var progress = 0.30f;
    var healthMultiplier = 8f;
    if (parts.Length > 3) float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out progress);
    if (parts.Length > 4) float.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out healthMultiplier);
    var result = gm.DebugSpawnEnemyForTest(enemyId, spawnCount, lane, progress, healthMultiplier);
    if (result.StartsWith("spawned ", System.StringComparison.OrdinalIgnoreCase)) spawned += spawnCount;
    log.AppendLine(result);
}
return "autospawn spawned=" + spawned + "\n" + log.ToString();
"@
    $enemySpawnResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $enemySpawnCode -Id 135
    Assert-McpToolSuccess -Step "spawn test enemies" -Response $enemySpawnResult
} else {
    $enemySpawnResult = $null
}

$applyTimeScaleCode = @"
UnityEngine.Time.timeScale = UnityEngine.Mathf.Clamp(${timeScaleLiteral}f, 0.1f, 32f);
return "playTimeScale=" + UnityEngine.Time.timeScale;
"@
$timeScaleResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $applyTimeScaleCode -Id 134
Assert-McpToolSuccess -Step "apply playtest time scale" -Response $timeScaleResult

if ($p124Enabled) {
    $escapedP124Strategy = Escape-CSharpString $P124AutoplayStrategy
    $escapedP135MechanicPolicy = Escape-CSharpString $P135MechanicPolicy
    $p135ConfigureCode = if ($p135Enabled) {
        "var configured = gm.DebugConfigureP135ForTest(`"$escapedP135MechanicPolicy`");"
    } else {
        "var configured = `"p13.5.configured=False`";"
    }
    $p124StartCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
if (gm == null) return "no TDGameManager";
$p135ConfigureCode
return configured + "\n" + gm.DebugStartP124AutoplayForTest("$escapedP124Strategy", $P124SiteVariant, ${P124MaxRealSeconds}f);
"@
    $p124StartResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $p124StartCode -Id 157
    Assert-McpToolSuccess -Step "start P12.4 autoplay" -Response $p124StartResult
} else {
    $p124StartResult = $null
}

$p123PreparationResult = $null
$p133PreparationResult = $null
$p134PreparationResult = $null
if ($PrepareP134Combat) {
    $p134PreparationResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 160 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPrepareP134ForTest();
'@
    Assert-McpToolSuccess -Step "prepare P13.4 audio visual input feel" -Response $p134PreparationResult
    Start-Sleep -Milliseconds 280
    $p122PreparationResult = $null
    $p121PreparationResult = $null
    $p112PreparationResult = $null
    $p113PreparationResult = $null
} elseif ($PrepareP133Combat) {
    $p133PreparationResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 159 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPrepareP133ForTest();
'@
    Assert-McpToolSuccess -Step "prepare P13.3 combat readability" -Response $p133PreparationResult
    Start-Sleep -Milliseconds 260
    $p122PreparationResult = $null
    $p121PreparationResult = $null
    $p112PreparationResult = $null
    $p113PreparationResult = $null
} elseif ($PrepareP123Campaign -or $PrepareP123Settings -or $PrepareP123Formation -or $PrepareP123Profile) {
    $p123ChineseLiteral = if ($P123Language -eq "Chinese") { "true" } else { "false" }
    $p123Surface = if ($PrepareP123Settings) { "settings" } elseif ($PrepareP123Formation) { "formation" } elseif ($PrepareP123Profile) { "profile" } else { "campaign" }
    $p123PreparationCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPrepareP123ForTest($p123ChineseLiteral, "$p123Surface");
"@
    $p123PreparationResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 156 -Code $p123PreparationCode
    Assert-McpToolSuccess -Step "prepare P12.3 surface" -Response $p123PreparationResult
    Start-Sleep -Milliseconds 260
    $p122PreparationResult = $null
    $p121PreparationResult = $null
    $p112PreparationResult = $null
    $p113PreparationResult = $null
} elseif ($PrepareP122Exam) {
    $p122PreparationResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 155 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPrepareP122ExamForTest();
'@
    Assert-McpToolSuccess -Step "prepare P12.2 exam" -Response $p122PreparationResult
    Start-Sleep -Milliseconds 260
    $p121PreparationResult = $null
    $p112PreparationResult = $null
    $p113PreparationResult = $null
} elseif ($PrepareP121Presentation) {
    $p122PreparationResult = $null
    $p121PreparationResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 154 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPrepareP121ForTest();
'@
    Assert-McpToolSuccess -Step "prepare P12.1 presentation" -Response $p121PreparationResult
    Start-Sleep -Milliseconds 240
    $p112PreparationResult = $null
    $p113PreparationResult = $null
} elseif ($PrepareP113Presentation) {
    $p122PreparationResult = $null
    $p121PreparationResult = $null
    $p113PreparationResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 153 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPrepareP113ForTest();
'@
    Assert-McpToolSuccess -Step "prepare P11.3 presentation" -Response $p113PreparationResult
    Start-Sleep -Milliseconds 180
    $p112PreparationResult = $null
} elseif ($PrepareP112Combat -or $PrepareP112Presentation) {
    $p122PreparationResult = $null
    $p121PreparationResult = $null
    $p113PreparationResult = $null
    $p112Method = if ($PrepareP112Combat) { "DebugPrepareP112CombatForTest" } else { "DebugPrepareP112PresentationForTest" }
    $p112PreparationCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.$p112Method();
"@
    $p112PreparationResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $p112PreparationCode -Id 152
    Assert-McpToolSuccess -Step "prepare P11.2 presentation" -Response $p112PreparationResult
    Start-Sleep -Milliseconds 180
} else {
    $p122PreparationResult = $null
    $p112PreparationResult = $null
    $p113PreparationResult = $null
    $p121PreparationResult = $null
}

$playtestWaitStarted = [DateTime]::UtcNow
if ($DurationSeconds -gt 0) {
    $waitDeadline = $playtestWaitStarted.AddSeconds($DurationSeconds)
    do {
        $remainingMilliseconds = [Math]::Max(1, [Math]::Round(($waitDeadline - [DateTime]::UtcNow).TotalMilliseconds))
        Start-Sleep -Milliseconds ([Math]::Min(500, $remainingMilliseconds))
        if (-not $WaitFullDuration) {
            $gameOverProbe = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 133 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm != null && (gm.IsGameOver || gm.IsP124AutoplayTerminal);
'@
            if (Test-McpToolSuccess -Response $gameOverProbe) {
                $probeContent = Get-McpStructuredContent -Response $gameOverProbe
                if ([string]$probeContent.data.result -eq "True") {
                    break
                }
            }
        }
    } while ([DateTime]::UtcNow -lt $waitDeadline)
}
$actualDurationSeconds = [Math]::Round(([DateTime]::UtcNow - $playtestWaitStarted).TotalSeconds, 2)

$forceRunResultRequested = $ForceRunResult -or $ForceVictoryResult
if ($forceRunResultRequested) {
    $forceVictoryLiteral = if ($ForceVictoryResult) { "true" } else { "false" }
    $forcedRunResultCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugShowRunResultForTest($forceVictoryLiteral);
"@
    $forcedRunResultResponse = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 137 -Code $forcedRunResultCode
    Assert-McpToolSuccess -Step "show run result" -Response $forcedRunResultResponse
    Start-Sleep -Milliseconds 250
} else {
    $forcedRunResultResponse = $null
}

if ($PrepareP9Presentation) {
    $p9PresentationResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 151 -Code @'
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugPrepareP9PresentationForTest();
'@
    Assert-McpToolSuccess -Step "prepare P9 presentation" -Response $p9PresentationResult
    Start-Sleep -Milliseconds 120
} else {
    $p9PresentationResult = $null
}

if ($p124Enabled) {
    $escapedP124ReportPath = Escape-CSharpString $P124RunReportPath
    $p124WriteCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugWriteP124RunJson("$escapedP124ReportPath");
"@
    $p124WriteResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $p124WriteCode -Id 158
    Assert-McpToolSuccess -Step "write P12.4 real run" -Response $p124WriteResult
} else {
    $p124WriteResult = $null
}

if ($p135Enabled) {
    $escapedP135ReportPath = Escape-CSharpString $P135RunReportPath
    $p135WriteCode = @"
var gm = UnityEngine.Object.FindFirstObjectByType<TD.TDGameManager>();
return gm == null ? "no TDGameManager" : gm.DebugWriteP135RunJson("$escapedP135ReportPath");
"@
    $p135WriteResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $p135WriteCode -Id 159
    Assert-McpToolSuccess -Step "write P13.5 real run" -Response $p135WriteResult
} else {
    $p135WriteResult = $null
}

$escapedScreenshotPath = Escape-CSharpString $ScreenshotPath
$campaignAuditAppendParts = New-Object System.Collections.Generic.List[string]
if ($RunCampaignProgressAudit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditCampaignProgressionForTest()); sb.Append(gm.DebugAuditCampaignContentForTest()); sb.Append(gm.DebugAuditP82MissionRulesForTest()); sb.Append(gm.DebugAuditP83FormationForTest()); sb.Append(gm.DebugAuditP84CampaignForTest()); sb.Append(gm.DebugAuditP85DifficultyForTest()); sb.Append(gm.DebugAuditP86ForTest()); sb.Append(gm.DebugAuditP9ForTest()); sb.Append(gm.DebugAuditP101ForTest()); sb.Append(gm.DebugAuditP102ForTest());")
} elseif ($RunP84Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP84CampaignForTest());")
} elseif ($RunP85Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP85DifficultyForTest());")
} elseif ($RunP86Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP86ForTest());")
} elseif ($RunP9Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP9ForTest());")
} elseif ($RunP111Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP111ForTest());")
} elseif ($RunP112Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP112ForTest());")
} elseif ($RunP113Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP113ForTest());")
} elseif ($RunP134Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP134ForTest());")
} elseif ($RunP133Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP133ForTest());")
} elseif ($RunP120GeometryAudit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP120GeometryForTest());")
} elseif ($RunP123Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP123ForTest());")
} elseif ($RunP124Audit -or $p124Enabled) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP124ForTest()); sb.Append(gm.DebugAuditP130ForTest()); sb.Append(gm.DebugAuditP131ForTest());")
    if ($p135Enabled) {
        $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP135ForTest());")
    }
    if ($RunP125EconomyAudit) {
        $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP125EconomyForTest());")
    }
} elseif ($RunP125EconomyAudit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP125EconomyForTest());")
} elseif ($RunP122Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP121ForTest()); sb.Append(gm.DebugAuditP122ForTest());")
} elseif ($RunP121Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP121ForTest());")
} elseif ($RunP101Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP101ForTest());")
} elseif ($RunP102Audit) {
    $campaignAuditAppendParts.Add("sb.Append(gm.DebugAuditP102ForTest());")
}
$campaignAuditAppendCode = [string]::Join(" ", $campaignAuditAppendParts)

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
    "Tower Upgrade Panel", "Resonance Command Panel", "Scenario Mechanic", "Scenario Mechanic Command",
    "Ember Command Button", "Fracture Command Button", "Run Result Scrim", "Run Result",
    "Mission Contract", "Mission Board Button", "Mission Board Scrim", "Mission Board", "Mission Deploy Button",
    "Mission Intel Contract", "Prebattle Formation", "Formation Deploy", "Formation Auto Fit",
    "Difficulty Standard", "Difficulty Veteran", "Difficulty EmberTrial", "Formation Difficulty",
    "Campaign Profile Button", "Campaign Profile", "Campaign Save Copy", "Campaign Save Import", "Campaign Save Reset",
    "Campaign Save Slot 1", "Campaign Save Slot 2", "Campaign Save Slot 3", "Campaign Cloud Copy", "Campaign Cloud Merge",
    "Tactical Protocol baseline", "Tactical Protocol forward_recon", "Tactical Protocol salvage_mandate", "Tactical Protocol field_control", "Tactical Protocol modular_reserve",
    "Result Mission Button", "Next Mission Button", "Playback And Accessibility", "Playback II",
    "Playback 1x", "Playback 2x", "Playback 3x", "Colorblind Markers", "Large Text",
    "Interactive Tutorial", "Tutorial Confirm", "Tutorial Skip", "Combat Feedback Signals", "Combat Cinematic"
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

var panelNames = new [] {"Primary HUD", "Wave Intel", "Tactical Feed", "Tower Build Bar", "Tower Upgrade Panel", "Resonance Command Panel", "Scenario Mechanic", "Run Result", "Mission Board", "Prebattle Formation", "Campaign Profile"};
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
        if (activePanels[i].Key == "Run Result" || activePanels[j].Key == "Run Result" ||
            activePanels[i].Key == "Mission Board" || activePanels[j].Key == "Mission Board" ||
            activePanels[i].Key == "Prebattle Formation" || activePanels[j].Key == "Prebattle Formation" ||
            activePanels[i].Key == "Campaign Profile" || activePanels[j].Key == "Campaign Profile") continue;
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
    "Campaign", "Guide", "Mission Contract", "Wave Intel Body", "Wave Intel Enemy", "Wave Intel Profile",
    "Wave Intel Route", "Wave Intel Counter", "Wave Intel Readiness", "Tower Stats",
    "Tower Upgrade Preview", "Tower Upgrade Hint", "Resonance Label", "Run Result Body",
    "Resonance Command Title", "Resonance Command Forecast", "Scenario Mechanic Title", "Scenario Mechanic Body",
    "Run Result Title", "Run Result Score", "Run Result Lanes", "Run Result Towers", "Run Result Heatmap", "Run Result Failure",
    "Run Result Recap", "Run Result Recommendation", "Mission Board Progress", "Mission Intel Title",
    "Mission Intel Brief", "Mission Intel Threat", "Mission Intel Contract", "Mission Intel Counter", "Mission Intel Record",
    "Formation Title", "Formation Threat", "Formation Roster", "Formation Fit Title", "Formation Fit Body", "Formation Matrix", "Formation Lock State", "Formation Difficulty",
    "Campaign Profile Title", "Campaign Profile Summary", "Campaign Profile Chapters", "Campaign Profile Bonuses", "Campaign Save Details", "Campaign Save Status",
    "Mission Chapter 1 Progress", "Mission Chapter 2 Progress", "Mission Chapter 3 Progress", "Mission Chapter 4 Progress",
    "Tutorial Progress", "Tutorial Title", "Tutorial Body", "Signal Title", "Signal Body"
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

var missionCloseLabel = uiObjects.TryGetValue("Mission Close Button", out var missionCloseObject) && missionCloseObject != null
    ? missionCloseObject.GetComponentInChildren<UnityEngine.UI.Text>(true)
    : null;
var missionCloseButton = missionCloseObject != null ? missionCloseObject.GetComponent<UnityEngine.UI.Button>() : null;
sb.AppendLine("missionCloseButton=" + (missionCloseLabel == null ? "<missing>" : missionCloseLabel.text) + " interactable=" + (missionCloseButton != null && missionCloseButton.interactable));

var nextMissionLabel = uiObjects.TryGetValue("Next Mission Button", out var nextMissionObject) && nextMissionObject != null
    ? nextMissionObject.GetComponentInChildren<UnityEngine.UI.Text>(true)
    : null;
var nextMissionButton = nextMissionObject != null ? nextMissionObject.GetComponent<UnityEngine.UI.Button>() : null;
sb.AppendLine("nextMissionButton=" + (nextMissionLabel == null ? "<missing>" : nextMissionLabel.text) + " interactable=" + (nextMissionButton != null && nextMissionButton.interactable));

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
    foreach (var name in new [] {"_wave", "_wavesCleared", "_lineIntegrity", "_defenseBudget", "_gameOver", "_victory", "_lastStatus", "_isInPrepPhase", "_waveStartRequested", "_totalKills", "_totalEscapes", "_totalDamageDealt", "_totalIntegrityDamageTaken", "_lastWaveStartReadinessScore", "_lastWaveStartReadinessGrade", "_resonanceCharge", "_resonanceWindowTimer", "_resonanceWindowsTriggered", "_resonanceCommandsUsed", "_resonanceMatchedCommands", "_emberSurgeUses", "_fractureMarkUses", "_resonanceBonusDamage", "_codexDiscoveriesThisRun", "_budgetSpentOnBuilds", "_budgetSpentOnUpgrades", "_upgradesPurchased"})
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

    sb.Append(gm.DebugGetP6AnalyticsReport());
    sb.Append(gm.DebugGetP8CampaignReport());
    sb.AppendLine(gm.DebugGetP9PresentationReport());
    sb.AppendLine(gm.DebugGetP101MetaReport());
    $campaignAuditAppendCode
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
$enemySpawnText = if ($null -eq $enemySpawnResult) { "" } else { [string](Get-McpStructuredContent -Response $enemySpawnResult).data.result }
$startWaveText = if ($null -eq $startWaveResult) { "" } else { [string](Get-McpStructuredContent -Response $startWaveResult).data.result }
$stateText = [string](Get-McpStructuredContent -Response $stateResult).data.result
$consoleIssueContent = Get-McpStructuredContent -Response $consoleIssuesResult
$consoleIssueEntries = @($consoleIssueContent.data)
$ignoredConsoleIssuePatterns = @(
    "MCP-FOR-UNITY.*Unexpected receive error: WebSocket is not initialised",
    "MCP-FOR-UNITY.*Keep-alive failed: The remote party closed the WebSocket connection",
    "MCP-FOR-UNITY.*Receive loop error: The remote party closed the WebSocket connection",
    "MCP-FOR-UNITY.*Connection closed: The remote party closed the WebSocket connection"
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
$plannedEnemyCount = 0
if (-not [string]::IsNullOrWhiteSpace($EnemyPlan)) {
    foreach ($enemyItem in $EnemyPlan -split ";") {
        $enemyParts = $enemyItem -split ":"
        if ($enemyParts.Count -ge 2 -and $enemyParts[1].Trim() -match "^-?\d+$") {
            $plannedEnemyCount += [Math]::Min(64, [Math]::Max(1, [int]$enemyParts[1]))
        }
    }
}
$actualEnemyCount = if ($enemySpawnText -match "autospawn spawned=(\d+)") { [int]$Matches[1] } elseif ($plannedEnemyCount -eq 0) { 0 } else { -1 }
$tacticalScore = if ($stateText -match "p6\.score\.total=(\d+)") { [int]$Matches[1] } else { -1 }
$tacticalScoreInRange = $tacticalScore -ge 0
if ($MinTacticalScore -ge 0) {
    $tacticalScoreInRange = $tacticalScoreInRange -and $tacticalScore -ge $MinTacticalScore
}
if ($MaxTacticalScore -ge 0) {
    $tacticalScoreInRange = $tacticalScoreInRange -and $tacticalScore -le $MaxTacticalScore
}

$expectationFailures = @()
foreach ($token in $ExpectState -split ";") {
    $trimmedToken = $token.Trim()
    if ($trimmedToken -and -not $stateText.Contains($trimmedToken)) {
        $expectationFailures += $trimmedToken
    }
}

$ultimateProcCount = -1
$ultimateFullMatchCount = -1
if (-not [string]::IsNullOrWhiteSpace($ExpectUltimateId)) {
    $ultimatePattern = "p7\.ultimate\.\d+=id:$([Regex]::Escape($ExpectUltimateId)),procs:(\d+),fullMatches:(\d+)"
    $ultimateMatch = [Regex]::Match($stateText, $ultimatePattern)
    if ($ultimateMatch.Success) {
        $ultimateProcCount = [int]$ultimateMatch.Groups[1].Value
        $ultimateFullMatchCount = [int]$ultimateMatch.Groups[2].Value
    }
}
$matrixFullMatchCount = if ($stateText -match "p7\.matrix\.runtime=opportunities:\d+,traitMatches:\d+,resonanceMatches:\d+,fullMatches:(\d+)") { [int]$Matches[1] } else { -1 }
$convergenceTriggerCount = if ($stateText -match "p7\.convergence=triggers:(\d+)") { [int]$Matches[1] } else { -1 }
$matrixCountForCheck = if ($ultimateFullMatchCount -ge 0) { $ultimateFullMatchCount } else { $matrixFullMatchCount }

$checks = [ordered]@{
    unitySession = $true
    gameManager = $stateText.Contains("_wave=")
    autoBuild = $p124Enabled -or $SkipAutoBuild -or $actualBuildCount -eq $plannedBuildCount
    autoUpgrade = $actualUpgradeCount -eq $plannedUpgradeCount
    enemySpawn = $actualEnemyCount -eq $plannedEnemyCount
    startWave = $p124Enabled -or $SkipStartWave -or
        $startWaveText.StartsWith("start requested ", [StringComparison]::OrdinalIgnoreCase) -or
        ($startWaveText.StartsWith("skip: not in prep", [StringComparison]::OrdinalIgnoreCase) -and
         $stateText.Contains("_isInPrepPhase=False") -and
         -not $stateText.Contains("_wave=0"))
    uiRequired = $stateText.Contains("uiMissing=0")
    uiBounds = $stateText.Contains("uiOutOfBounds=0")
    uiOverlap = $stateText.Contains("uiOverlaps=none")
    uiTextFit = $stateText.Contains("uiTextOverflow=0")
    p6Analytics = $stateText.Contains("p6.score.total=") -and
        $stateText.Contains("p6.lane.count=") -and
        $stateText.Contains("p6.tower.count=") -and
        $stateText.Contains("p6.segment.count=") -and
        $stateText.Contains("p6.hotspot.count=") -and
        $stateText.Contains("p6.recommendation.count=3") -and
        $stateText.Contains("p6.audit.consistent=True")
    p7Matrix = $stateText.Contains("p7.matrix.count=16") -and
        $stateText.Contains("p7.convergence=triggers:") -and
        $stateText.Contains("p7.window.best=sync:") -and
        $stateText.Contains("p7.audit.uniqueIds=True") -and
        $stateText.Contains("p7.audit.allBranches=True")
    p8Campaign = $stateText.Contains("p8.save.version=2") -and
        $stateText.Contains("p8.ui.levelButtons=20") -and
        $stateText.Contains("p8.intel.valid=True") -and
        $stateText.Contains("p8.progress.contracts=") -and
        $stateText.Contains("p8.audit.progressConsistent=True") -and
        $stateText.Contains("p8.audit.currentUnlocked=True")
    p82MissionRules = $stateText.Contains("p8.2.contract.id=") -and
        -not $stateText.Contains("p8.2.contract.id=none") -and
        $stateText.Contains("p8.2.mutators=1") -and
        $stateText.Contains("p8.2.runtime.start=") -and
        $stateText.Contains("p8.2.runtime.enemy=") -and
        $stateText.Contains("p8.2.runtime.economy=")
    p83Formation = $stateText.Contains("p8.3.available=true") -and
        $stateText.Contains("p8.3.formation.slots=") -and
        $stateText.Contains("p8.3.doctrine=") -and
        $stateText.Contains("p8.3.doctrine.livePower=") -and
        $stateText.Contains("p8.3.fit.total=") -and
        $stateText.Contains("p8.3.fit.coverage=") -and
        $stateText.Contains("p8.3.fit.matrix=")
    p84Campaign = $stateText.Contains("p8.4.chapters.total=4") -and
        $stateText.Contains("p8.4.rewards.claimed=") -and
        $stateText.Contains("p8.4.runtime.legacy=") -and
        $stateText.Contains("p8.4.campaign.rank=") -and
        $stateText.Contains("p8.4.profile.previewValid=True") -and
        $stateText.Contains("p8.4.profile.codeLength=")
    p85Difficulty = $stateText.Contains("p8.5.config.tiers=3") -and
        $stateText.Contains("p8.5.config.remixes=4/4") -and
        $stateText.Contains("p8.5.active=") -and
        $stateText.Contains("p8.5.runtime=") -and
        $stateText.Contains("p8.5.audit.runtimeMatches=True")
    p86Scenario = $stateText.Contains("p8.6.save.slots=3") -and
        $stateText.Contains("p8.6.maps.mechanics=5") -and
        $stateText.Contains("p8.6.exams=5") -and
        $stateText.Contains("p8.6.runtime.mechanic=")
    p86Runtime = $p86ScenarioApplied
    p8ProgressAudit = -not $RunCampaignProgressAudit -or
        ($stateText.Contains("p8.audit.initialLock=True") -and
         $stateText.Contains("p8.audit.defeatKeepsLock=True") -and
         $stateText.Contains("p8.audit.firstClearUnlocks=True") -and
         $stateText.Contains("p8.audit.bestIsMonotonic=True") -and
         $stateText.Contains("p8.audit.secondClearUnlocks=True") -and
         $stateText.Contains("p8.2.audit.contractPersists=True") -and
         $stateText.Contains("p8.content.valid=20/20") -and
         $stateText.Contains("p8.2.content.contracts=20/20") -and
         $stateText.Contains("p8.2.content.mutators=20/20") -and
         $stateText.Contains("p8.2.audit.runtimeMatches=True") -and
         $stateText.Contains("p8.2.audit.enemyCloneIsolation=True") -and
         $stateText.Contains("p8.2.audit.contractBoundary=True") -and
         $stateText.Contains("p8.2.audit.pass=True") -and
         $stateText.Contains("p8.3.audit.autoFits=20/20") -and
         $stateText.Contains("p8.3.audit.scoresBounded=20/20") -and
         $stateText.Contains("p8.3.audit.persistence=True") -and
         $stateText.Contains("p8.3.audit.snapshotRoundTrip=True") -and
         $stateText.Contains("p8.3.audit.doctrinePower=True") -and
         $stateText.Contains("p8.3.audit.activeFormationLimit=True") -and
         $stateText.Contains("p8.3.audit.allFormationTextFit=True") -and
         $stateText.Contains("p8.3.audit.pass=True") -and
         $stateText.Contains("p8.audit.allMissionIntel=True") -and
         $stateText.Contains("p8.audit.allMissionTextFit=True") -and
         $stateText.Contains("p8.audit.pass=True"))
    p84Audit = (-not $RunCampaignProgressAudit -and -not $RunP84Audit) -or
        ($stateText.Contains("p8.4.audit.chapterMastery=True") -and
         $stateText.Contains("p8.4.audit.autoClaim=True") -and
         $stateText.Contains("p8.4.audit.rewardPersistence=True") -and
         $stateText.Contains("p8.4.audit.rewardRuntime=True") -and
         $stateText.Contains("p8.4.audit.portablePreview=True") -and
         $stateText.Contains("p8.4.audit.portableRoundTrip=True") -and
         $stateText.Contains("p8.4.audit.tamperRejected=True") -and
         $stateText.Contains("p8.4.audit.unknownRewardRejected=True") -and
         $stateText.Contains("p8.4.audit.reset=True") -and
         $stateText.Contains("p8.4.audit.campaignCompletion=True") -and
         $stateText.Contains("p8.4.audit.clipboard=True") -and
         $stateText.Contains("p8.4.audit.doubleConfirm=True") -and
         $stateText.Contains("p8.4.audit.allTextFit=True") -and
         $stateText.Contains("p8.4.audit.pass=True"))
    p85Audit = (-not $RunCampaignProgressAudit -and -not $RunP85Audit) -or
        ($stateText.Contains("p8.5.audit.content=True") -and
         $stateText.Contains("p8.5.audit.initialLocks=True") -and
         $stateText.Contains("p8.5.audit.veteranUnlock=True") -and
         $stateText.Contains("p8.5.audit.emberUnlock=True") -and
         $stateText.Contains("p8.5.audit.standardRuntime=True") -and
         $stateText.Contains("p8.5.audit.veteranRuntime=True") -and
         $stateText.Contains("p8.5.audit.emberRuntime=True") -and
         $stateText.Contains("p8.5.audit.preference=True") -and
         $stateText.Contains("p8.5.audit.recordMonotonic=True") -and
         $stateText.Contains("p8.5.audit.portableRoundTrip=True") -and
         $stateText.Contains("p8.5.audit.ui=True") -and
         $stateText.Contains("p8.5.audit.fullChallenge=True") -and
         $stateText.Contains("p8.5.audit.pass=True"))
    p86Audit = (-not $RunCampaignProgressAudit -and -not $RunP86Audit) -or
        ($stateText.Contains("p8.6.audit.mechanics=True") -and
         $stateText.Contains("p8.6.audit.grammar20=True") -and
         $stateText.Contains("p8.6.audit.exams=True") -and
         $stateText.Contains("p8.6.audit.slotIsolation=True") -and
         $stateText.Contains("p8.6.audit.cloudMerge=True") -and
         $stateText.Contains("p8.6.audit.keepLocal=True") -and
         $stateText.Contains("p8.6.audit.useCloud=True") -and
         $stateText.Contains("p8.6.audit.legacyMigration=True") -and
         $stateText.Contains("p8.6.audit.pass=True"))
    p9Presentation = $stateText.Contains("p9.presentation.initialized=True") -and
        $stateText.Contains("p9.presentation.markers=") -and
        $stateText.Contains("p9.playback.speed=") -and
        $stateText.Contains("p9.tutorial.step=")
    p9Audit = (-not $RunCampaignProgressAudit -and -not $RunP9Audit) -or
        ($stateText.Contains("p9.audit.playback=True") -and
         $stateText.Contains("p9.audit.feedback6=True") -and
         $stateText.Contains("p9.audit.cinematics=True") -and
         $stateText.Contains("p9.audit.accessibility=True") -and
         $stateText.Contains("p9.audit.tutorialFlow=True") -and
         $stateText.Contains("p9.audit.tutorialSkip=True") -and
         $stateText.Contains("p9.audit.ui=True") -and
         $stateText.Contains("p9.audit.textOverflow=none") -and
         $stateText.Contains("p9.audit.pass=True"))
    p111Audit = (-not $RunP111Audit) -or
        ($stateText.Contains("p11.1.audit.resources=ready") -and
         $stateText.Contains("p11.1.audit.metricIcons=True") -and
         $stateText.Contains("p11.1.audit.towerIcons=True") -and
         $stateText.Contains("p11.1.audit.formationIcons=True [8/8]") -and
         $stateText.Contains("p11.1.audit.identities=True [icons=8,roles=8,colors=8]") -and
         $stateText.Contains("p11.1.audit.typography=True") -and
         $stateText.Contains("p11.1.audit.textOverflow=none") -and
         $stateText.Contains("p11.1.audit.pass=True"))
    p112Audit = (-not $RunP112Audit) -or
        ($stateText.Contains("p11.2.audit.resources=ready") -and
         $stateText.Contains("p11.2.audit.projectiles=True [projectiles=8,impacts=8]") -and
         $stateText.Contains("p11.2.audit.threatMatrix=True [levels=4,categories=6]") -and
         $stateText.Contains("p11.2.audit.outlines=True") -and
         $stateText.Contains("p11.2.audit.markers=True") -and
         $stateText.Contains("p11.2.audit.statusStrip=True [max=5]") -and
         $stateText.Contains("p11.2.audit.shader=True") -and
         $stateText.Contains("p11.2.audit.pass=True"))
    p113Audit = (-not $RunP113Audit) -or
        ($stateText.Contains("p11.3.audit.foundation=True [8/8]") -and
         $stateText.Contains("p11.3.audit.buildSpots=True [total=12,authored=True") -and
         $stateText.Contains("p11.3.audit.routeIntegrity=True") -and
         $stateText.Contains("p11.3.audit.silhouetteRepair=True [3/3,shader=TD/EnemyBodyRepair]") -and
         $stateText.Contains("p11.3.audit.threatMarkerIntegration=True") -and
         $stateText.Contains("p11.3.audit.charge=True [visible=8/8") -and
         $stateText.Contains("p11.3.audit.upgrade=True") -and
         $stateText.Contains("p11.3.audit.ordering=True") -and
         $stateText.Contains("p11.3.audit.pass=True"))
    p133Audit = (-not $RunP133Audit) -or
        ($stateText.Contains("p13.3.audit.route=True") -and
         $stateText.Contains("p13.3.audit.buildSites=True") -and
         $stateText.Contains("p13.3.audit.towerPlacement=True") -and
         $stateText.Contains("p13.3.audit.enemyMotion=True") -and
         $stateText.Contains("p13.3.audit.interaction=True") -and
         $stateText.Contains("p13.3.audit.traits=True") -and
         $stateText.Contains("p13.3.audit.status=True") -and
         $stateText.Contains("p13.3.audit.occlusion=True") -and
         $stateText.Contains("p13.3.audit.density=True") -and
          $stateText.Contains("p13.3.audit.pass=True"))
    p134Audit = (-not $RunP134Audit) -or
        ($stateText.Contains("p13.4.audit.feedback8=True") -and
         $stateText.Contains("p13.4.audit.signalBudget=True") -and
         $stateText.Contains("p13.4.audit.cinematics=True") -and
         $stateText.Contains("p13.4.audit.towerIdentity=True") -and
         $stateText.Contains("p13.4.audit.projectileResources=True") -and
         $stateText.Contains("p13.4.audit.audio=True") -and
         $stateText.Contains("p13.4.audit.input=True") -and
         $stateText.Contains("p13.4.audit.fxBudget=True") -and
         $stateText.Contains("p13.4.audit.pass=True"))
    p120GeometryAudit = (-not $RunP120GeometryAudit) -or
        ($stateText.Contains("p12.0.geometry.routes=True") -and
         $stateText.Contains("p12.0.geometry.buildSites=True") -and
         $stateText.Contains("p12.0.geometry.runtime=True") -and
         $stateText.Contains("p12.0.geometry.pass=True"))
    p121Audit = (-not $RunP121Audit) -or
        ($stateText.Contains("p12.1.audit.animation=True") -and
         $stateText.Contains("p12.1.audit.audio=True") -and
         $stateText.Contains("p12.1.audit.feedbackReduced=True") -and
         $stateText.Contains("p12.1.audit.scoreChart=True") -and
         $stateText.Contains("p12.1.audit.breakdowns=True") -and
         $stateText.Contains("p12.1.audit.frameAspect=True") -and
         $stateText.Contains("p12.1.audit.textOverflow=none") -and
         $stateText.Contains("p12.1.audit.pass=True"))
    p122Audit = (-not $RunP122Audit) -or
        ($stateText.Contains("p12.1.audit.pass=True") -and
         $stateText.Contains("p12.2.audit.catalog=True") -and
         $stateText.Contains("p12.2.audit.currentProfile=True") -and
         $stateText.Contains("p12.2.audit.device=True") -and
         $stateText.Contains("p12.2.audit.beats=True") -and
         $stateText.Contains("p12.2.audit.resultIdentity=True") -and
         $stateText.Contains("p12.2.audit.strategies=True") -and
         $stateText.Contains("p12.2.audit.textOverflow=none") -and
         $stateText.Contains("p12.2.audit.pass=True"))
    p123Audit = (-not $RunP123Audit) -or
        ($stateText.Contains("p12.3.audit.localization=True") -and
         $stateText.Contains("p12.3.audit.font=True") -and
         $stateText.Contains("p12.3.audit.campaignSurface=True") -and
         $stateText.Contains("p12.3.audit.focus=True") -and
         $stateText.Contains("p12.3.audit.input=True") -and
         $stateText.Contains("p12.3.audit.accessibility=True") -and
         $stateText.Contains("p12.3.audit.resolution=True") -and
         $stateText.Contains("p12.3.audit.textOverflow=none") -and
         $stateText.Contains("p12.3.audit.pass=True"))
    p124Audit = (-not $RunP124Audit -and -not $p124Enabled) -or
        ($stateText.Contains("p12.4.audit.complete=True") -and
         $stateText.Contains("p12.4.audit.analytics=True") -and
         $stateText.Contains("p12.4.audit.contribution=True") -and
         $stateText.Contains("p12.4.audit.explainable=True") -and
         $stateText.Contains("p12.4.audit.duration=True") -and
         $stateText.Contains("p12.4.audit.pass=True"))
    p135Audit = (-not $p135Enabled) -or
        ($stateText.Contains("p13.5.audit.complete=True") -and
         $stateText.Contains("p13.5.audit.firstWave=True") -and
         $stateText.Contains("p13.5.audit.telemetry=True") -and
         $stateText.Contains("p13.5.audit.mechanic=True") -and
         $stateText.Contains("p13.5.audit.boss=True") -and
         $stateText.Contains("p13.5.audit.pass=True"))
    p130Audit = (-not $RunP124Audit -and -not $p124Enabled) -or
        ($stateText.Contains("p13.0.audit.rating=True") -and
         $stateText.Contains("p13.0.audit.recommendations=True") -and
         $stateText.Contains("p13.0.audit.modalSuppression=True") -and
         $stateText.Contains("p13.0.audit.pass=True"))
    p131Audit = (-not $RunP124Audit -and -not $p124Enabled) -or
        ($stateText.Contains("p13.1.audit.firstWave=True") -and
         $stateText.Contains("p13.1.audit.cliffPacing=True") -and
         $stateText.Contains("p13.1.audit.sitePolicy=True") -and
         $stateText.Contains("p13.1.audit.runtime=True") -and
         $stateText.Contains("p13.1.audit.pass=True"))
    p125EconomyAudit = (-not $RunP125EconomyAudit) -or
        ($stateText.Contains("p12.5.0.audit.telemetry=True") -and
         $stateText.Contains("p12.5.0.audit.saturation=True") -and
         $stateText.Contains("p12.5.0.audit.reserve=True") -and
         $stateText.Contains("p12.5.0.audit.decisions=True") -and
         $stateText.Contains("p12.5.0.audit.enemyHpUnchanged=True") -and
         $stateText.Contains("p12.5.0.audit.pass=True"))
    p101Meta = $stateText.Contains("p10.1.config.protocols=5") -and
        $stateText.Contains("p10.1.config.rewards=4") -and
        $stateText.Contains("p10.1.progress.enemyDossiers=") -and
        $stateText.Contains("p10.1.progress.towerDossiers=") -and
        $stateText.Contains("p10.1.runtime=")
    p101Audit = (-not $RunCampaignProgressAudit -and -not $RunP101Audit) -or
        ($stateText.Contains("p10.1.audit.content=True") -and
         $stateText.Contains("p10.1.audit.sidegrades=True") -and
         $stateText.Contains("p10.1.audit.runtimeSignatures=True") -and
         $stateText.Contains("p10.1.audit.runtimeApplication=True") -and
         $stateText.Contains("p10.1.audit.examReplayPlans=True") -and
         $stateText.Contains("p10.1.audit.importWhitelist=True") -and
         $stateText.Contains("p10.1.audit.duplicateClaim=True") -and
         $stateText.Contains("p10.1.audit.observationOr=True") -and
         $stateText.Contains("p10.1.audit.snapshotRoundTrip=True") -and
         $stateText.Contains("p10.1.audit.cloudMerge=True") -and
         $stateText.Contains("p10.1.audit.archive=True") -and
         $stateText.Contains("p10.1.audit.ui=True") -and
         $stateText.Contains("p10.1.audit.textOverflow=none") -and
         $stateText.Contains("p10.1.audit.pass=True"))
    p102Audit = (-not $RunCampaignProgressAudit -and -not $RunP102Audit) -or
        ($stateText.Contains("p10.2.audit.matrix=180/180") -and
         $stateText.Contains("p10.2.audit.stalls=0") -and
         $stateText.Contains("p10.2.audit.strategies=3") -and
         $stateText.Contains("p10.2.audit.exams=5/5") -and
         $stateText.Contains("p10.2.audit.standardSmooth=True") -and
         $stateText.Contains("p10.2.audit.difficultyOrder=True") -and
         $stateText.Contains("p10.2.audit.deterministic=True") -and
         $stateText.Contains("p10.2.audit.pass=True"))
    ultimateProc = [string]::IsNullOrWhiteSpace($ExpectUltimateId) -or $ultimateProcCount -ge $MinUltimateProcs
    matrixFullMatch = $MinMatrixFullMatches -le 0 -or $matrixCountForCheck -ge $MinMatrixFullMatches
    matrixConvergence = $MinConvergenceTriggers -le 0 -or $convergenceTriggerCount -ge $MinConvergenceTriggers
    tacticalScoreRange = $tacticalScoreInRange
    screenshot = (Test-Path -LiteralPath $ScreenshotPath) -and (Get-Item -LiteralPath $ScreenshotPath).LastWriteTimeUtc -ge $runStartedUtc.AddSeconds(-1)
    consoleClean = $AllowConsoleIssues -or $consoleIssueCount -eq 0
    expectedState = $expectationFailures.Count -eq 0
}

if (-not $KeepPlaying) {
    Invoke-UnityTool -Url $McpUrl -SessionId $sessionId -ToolName "manage_editor" -Arguments @{ action = "stop" } -Id 16 | Out-Null
    if ($PreserveCampaignProgress) {
        Wait-UnitySession -Url $McpUrl -SessionId $sessionId -TimeoutSeconds $UnityReadyTimeoutSeconds | Out-Null
        $restoreCode = @"
var json = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String("$profileSnapshotEncoded"));
TD.TDCampaignProgression.ImportSnapshot(json, 20);
UnityEngine.PlayerPrefs.SetInt("td_campaign_selected_level", $profileSelectedLevel);
UnityEngine.PlayerPrefs.Save();
return "restored selected=$profileSelectedLevel";
"@
        $profileRestoreResult = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Id 150 -Code $restoreCode
        Assert-McpToolSuccess -Step "restore campaign profile" -Response $profileRestoreResult
    } else {
        $profileRestoreResult = $null
    }
} else {
    $profileRestoreResult = $null
}

$summary = [ordered]@{
    levelIndex = $LevelIndex
    viewportWidth = $ViewportWidth
    viewportHeight = $ViewportHeight
    durationSeconds = $DurationSeconds
    actualDurationSeconds = $actualDurationSeconds
    waitFullDuration = [bool]$WaitFullDuration
    freezeConfiguredWaves = [bool]$FreezeConfiguredWaves
    forceRunResult = [bool]$ForceRunResult
    forceVictoryResult = [bool]$ForceVictoryResult
    keepMissionBoardOpen = [bool]$KeepMissionBoardOpen
    keepFormationOpen = [bool]$KeepFormationOpen
    keepCampaignProfileOpen = [bool]$KeepCampaignProfileOpen
    prepareP84ChapterBoard = [bool]$PrepareP84ChapterBoard
    prepareP84CampaignCompletion = [bool]$PrepareP84CampaignCompletion
    prepareP85Difficulty = [bool]$PrepareP85Difficulty
    prepareP85CampaignPerfected = [bool]$PrepareP85CampaignPerfected
    prepareP86Scenario = [bool]$PrepareP86Scenario
    prepareP9Presentation = [bool]$PrepareP9Presentation
    prepareP112Presentation = [bool]$PrepareP112Presentation
    prepareP112Combat = [bool]$PrepareP112Combat
    prepareP113Presentation = [bool]$PrepareP113Presentation
    prepareP133Combat = [bool]$PrepareP133Combat
    prepareP134Combat = [bool]$PrepareP134Combat
    prepareP121Presentation = [bool]$PrepareP121Presentation
    prepareP122Exam = [bool]$PrepareP122Exam
    prepareP123Campaign = [bool]$PrepareP123Campaign
    prepareP123Settings = [bool]$PrepareP123Settings
    prepareP123Formation = [bool]$PrepareP123Formation
    prepareP123Profile = [bool]$PrepareP123Profile
    p123Language = $P123Language
    prepareP101Meta = [bool]$PrepareP101Meta
    runCampaignProgressAudit = [bool]$RunCampaignProgressAudit
    runP84Audit = [bool]$RunP84Audit
    runP85Audit = [bool]$RunP85Audit
    runP86Audit = [bool]$RunP86Audit
    runP9Audit = [bool]$RunP9Audit
    runP111Audit = [bool]$RunP111Audit
    runP112Audit = [bool]$RunP112Audit
    runP113Audit = [bool]$RunP113Audit
    runP133Audit = [bool]$RunP133Audit
    runP134Audit = [bool]$RunP134Audit
    runP120GeometryAudit = [bool]$RunP120GeometryAudit
    runP121Audit = [bool]$RunP121Audit
    runP122Audit = [bool]$RunP122Audit
    runP123Audit = [bool]$RunP123Audit
    runP124Audit = [bool]$RunP124Audit
    runP125EconomyAudit = [bool]$RunP125EconomyAudit
    runP101Audit = [bool]$RunP101Audit
    runP102Audit = [bool]$RunP102Audit
    preserveCampaignProgress = [bool]$PreserveCampaignProgress
    timeScale = $TimeScale
    randomSeed = $RandomSeed
    buildPlan = if ($SkipAutoBuild) { "<manual>" } else { $BuildPlan }
    upgradePlan = $UpgradePlan
    bonusBudget = $BonusBudget
    tacticalScore = $tacticalScore
    minTacticalScore = $MinTacticalScore
    maxTacticalScore = $MaxTacticalScore
    resonanceCommand = $ResonanceCommand
    formationDoctrine = $FormationDoctrine
    formationDifficulty = $FormationDifficulty
    p124AutoplayStrategy = $P124AutoplayStrategy
    p124SiteVariant = $P124SiteVariant
    p124RunReportPath = $P124RunReportPath
    p135MechanicPolicy = $P135MechanicPolicy
    p135RunReportPath = $P135RunReportPath
    enemyPlan = $EnemyPlan
    expectUltimateId = $ExpectUltimateId
    ultimateProcCount = $ultimateProcCount
    ultimateFullMatchCount = $ultimateFullMatchCount
    matrixFullMatchCount = $matrixFullMatchCount
    convergenceTriggerCount = $convergenceTriggerCount
    minUltimateProcs = $MinUltimateProcs
    minMatrixFullMatches = $MinMatrixFullMatches
    minConvergenceTriggers = $MinConvergenceTriggers
    screenshotPath = $ScreenshotPath
    checks = $checks
    expectationFailures = $expectationFailures
    resetCodex = Get-McpStructuredContent -Response $resetCodexResult
    runtimeSetup = Get-McpStructuredContent -Response $runtimeSetupResult
    missionDeploy = Get-McpStructuredContent -Response $missionDeployResult
    p84ChapterBoard = Get-McpStructuredContent -Response $p84ChapterBoardResult
    p85Difficulty = Get-McpStructuredContent -Response $p85DifficultyResult
    campaignProfileOpen = Get-McpStructuredContent -Response $campaignProfileOpenResult
    p84CampaignCompletion = Get-McpStructuredContent -Response $p84CampaignCompletionResult
    p85CampaignPerfected = Get-McpStructuredContent -Response $p85CampaignPerfectedResult
    p86Scenario = Get-McpStructuredContent -Response $p86ScenarioResult
    p9Presentation = Get-McpStructuredContent -Response $p9PresentationResult
    p112Preparation = Get-McpStructuredContent -Response $p112PreparationResult
    p113Preparation = Get-McpStructuredContent -Response $p113PreparationResult
    p133Preparation = Get-McpStructuredContent -Response $p133PreparationResult
    p134Preparation = Get-McpStructuredContent -Response $p134PreparationResult
    p121Preparation = Get-McpStructuredContent -Response $p121PreparationResult
    p122Preparation = Get-McpStructuredContent -Response $p122PreparationResult
    p123Preparation = Get-McpStructuredContent -Response $p123PreparationResult
    p124Progression = Get-McpStructuredContent -Response $p124ProgressionResult
    p124Start = Get-McpStructuredContent -Response $p124StartResult
    p124Write = Get-McpStructuredContent -Response $p124WriteResult
    p101Meta = Get-McpStructuredContent -Response $p101MetaResult
    timeScaleResult = Get-McpStructuredContent -Response $timeScaleResult
    refresh = Get-McpStructuredContent -Response $refreshResult
    profileCapture = Get-McpStructuredContent -Response $profileCaptureResult
    profileRestore = Get-McpStructuredContent -Response $profileRestoreResult
    bonusBudgetResult = Get-McpStructuredContent -Response $bonusBudgetResult
    resonanceResult = Get-McpStructuredContent -Response $resonanceResult
    enemySpawnResult = Get-McpStructuredContent -Response $enemySpawnResult
    autoBuild = Get-McpStructuredContent -Response $autoBuildResult
    autoUpgrade = Get-McpStructuredContent -Response $autoUpgradeResult
    freezeWaves = Get-McpStructuredContent -Response $freezeWavesResult
    forcedRunResult = Get-McpStructuredContent -Response $forcedRunResultResponse
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
