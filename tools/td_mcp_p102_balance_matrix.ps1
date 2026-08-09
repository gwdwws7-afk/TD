param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputDirectory = "E:/TD/output/playtest/p102_balance_matrix",
    [int]$Seed = 10202,
    [int]$UnityReadyTimeoutSeconds = 60,
    [switch]$RefreshScripts
)

$ErrorActionPreference = "Stop"
$runStartedUtc = [DateTime]::UtcNow
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$matrixJsonPath = Join-Path $outputRoot "p102_balance_matrix.json"
$runCsvPath = Join-Path $outputRoot "p102_runs.csv"
$curveCsvPath = Join-Path $outputRoot "p102_level_curve.csv"
$examCsvPath = Join-Path $outputRoot "p102_exam_strategies.csv"
$reportMarkdownPath = Join-Path $outputRoot "p102_balance_report.md"
$auditJsonPath = Join-Path $outputRoot "p102_audit.json"

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
            clientInfo = @{ name = "td-p102-balance-matrix"; version = "1.0" }
        }
    } | ConvertTo-Json -Depth 20
    $response = Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -Body $body -UseBasicParsing -TimeoutSec 20
    $sessionId = $response.Headers["Mcp-Session-Id"]
    if ([string]::IsNullOrWhiteSpace($sessionId)) {
        throw "MCP initialize response did not include Mcp-Session-Id."
    }

    $headers["Mcp-Session-Id"] = $sessionId
    $notification = @{ jsonrpc = "2.0"; method = "notifications/initialized"; params = @{} } | ConvertTo-Json -Depth 5
    try {
        Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -Body $notification -UseBasicParsing -TimeoutSec 10 | Out-Null
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
        [int]$Id,
        [int]$TimeoutSec = 60
    )
    $headers = @{
        Accept = "application/json, text/event-stream"
        "Content-Type" = "application/json"
        "Mcp-Session-Id" = $SessionId
    }
    $body = @{ jsonrpc = "2.0"; id = $Id; method = $Method; params = $Params } | ConvertTo-Json -Depth 80
    $response = Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -Body $body -UseBasicParsing -TimeoutSec $TimeoutSec
    return ConvertFrom-McpEvent $response.Content
}

function Invoke-UnityTool {
    param(
        [string]$SessionId,
        [string]$ToolName,
        [hashtable]$Arguments,
        [int]$Id,
        [int]$TimeoutSec = 60
    )
    return Invoke-Mcp -Url $McpUrl -SessionId $SessionId -Method "tools/call" -Id $Id -TimeoutSec $TimeoutSec -Params @{
        name = $ToolName
        arguments = $Arguments
    }
}

function Invoke-UnityCode {
    param([string]$SessionId, [string]$Code, [int]$Id, [int]$TimeoutSec = 90)
    return Invoke-UnityTool -SessionId $SessionId -ToolName "execute_code" -Id $Id -TimeoutSec $TimeoutSec -Arguments @{
        action = "execute"
        code = $Code
        safety_checks = $true
        compiler = "auto"
    }
}

function Get-StructuredContent {
    param($Response)
    if ($null -eq $Response -or $null -eq $Response.result) {
        return $null
    }

    return $Response.result.structuredContent
}

function Assert-UnitySuccess {
    param([string]$Step, $Response)
    $content = Get-StructuredContent $Response
    $success = $null -ne $content -and
        (($null -ne $content.PSObject.Properties["success"] -and [bool]$content.success) -or
         ($null -ne $content.PSObject.Properties["result"] -and [bool]$content.result.success))
    if (-not $success) {
        $detail = if ($null -eq $content) { "empty response" } else { $content | ConvertTo-Json -Depth 15 -Compress }
        throw "$Step failed: $detail"
    }
}

function Wait-UnityReady {
    param([string]$SessionId)
    $deadline = [DateTime]::UtcNow.AddSeconds($UnityReadyTimeoutSeconds)
    do {
        try {
            $probe = Invoke-UnityTool -SessionId $SessionId -ToolName "read_console" -Id 10 -TimeoutSec 10 -Arguments @{
                action = "get"
                types = @("error")
                count = 1
            }
            $content = Get-StructuredContent $probe
            if ($null -ne $content -and [bool]$content.success) {
                return
            }
        } catch {
        }

        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Unity MCP did not become ready within $UnityReadyTimeoutSeconds seconds."
}

function Escape-CSharpString {
    param([string]$Value)
    return $Value.Replace('\', '/').Replace('"', '\"')
}

function Get-DifficultySummary {
    param($Level, [string]$DifficultyId)
    return @($Level.difficulties | Where-Object { $_.difficultyId -eq $DifficultyId })[0]
}

function Get-Median {
    param([object[]]$Values)
    $ordered = @($Values | ForEach-Object { [double]$_ } | Sort-Object)
    if ($ordered.Count -eq 0) {
        return 0
    }
    $middle = [Math]::Floor($ordered.Count / 2)
    if ($ordered.Count % 2 -eq 0) {
        return ($ordered[$middle - 1] + $ordered[$middle]) / 2
    }
    return $ordered[$middle]
}

$sessionId = New-McpSession -Url $McpUrl
Wait-UnityReady -SessionId $sessionId

if ($RefreshScripts) {
    $refresh = Invoke-UnityTool -SessionId $sessionId -ToolName "refresh_unity" -Id 11 -TimeoutSec 60 -Arguments @{
        mode = "force"
        scope = "all"
        compile = "request"
        wait_for_ready = $false
    }
    Assert-UnitySuccess -Step "refresh Unity scripts" -Response $refresh
    Start-Sleep -Seconds 2
    Wait-UnityReady -SessionId $sessionId
}

$clear = Invoke-UnityCode -SessionId $sessionId -Id 12 -Code @'
var entries = System.Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
var clear = entries == null ? null : entries.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
if (clear != null) clear.Invoke(null, null);
return clear != null ? "console cleared" : "console clear unavailable";
'@
Assert-UnitySuccess -Step "clear Unity console" -Response $clear

$escapedMatrixPath = Escape-CSharpString $matrixJsonPath
$matrixResult = Invoke-UnityCode -SessionId $sessionId -Id 20 -TimeoutSec 120 -Code @"
return TD.TDBalanceSimulator.WriteReportJson("$escapedMatrixPath", $Seed);
"@
Assert-UnitySuccess -Step "execute P10.2 matrix" -Response $matrixResult
if (-not (Test-Path -LiteralPath $matrixJsonPath)) {
    throw "Unity did not write the matrix JSON to $matrixJsonPath."
}

$matrix = Get-Content -LiteralPath $matrixJsonPath -Raw | ConvertFrom-Json
$repeatResult = Invoke-UnityCode -SessionId $sessionId -Id 21 -TimeoutSec 120 -Code @"
return TD.TDBalanceSimulator.RunMatrix($Seed).fingerprint;
"@
Assert-UnitySuccess -Step "repeat deterministic fingerprint" -Response $repeatResult
$repeatFingerprint = [string](Get-StructuredContent $repeatResult).data.result
$deterministic = $repeatFingerprint -eq [string]$matrix.fingerprint

$runRows = foreach ($run in $matrix.runs) {
    $topTower = @($run.towerContributions | Sort-Object contributionScore -Descending | Select-Object -First 1)[0]
    [pscustomobject]@{
        runId = $run.runId
        level = $run.levelIndex
        difficulty = $run.difficultyId
        strategy = $run.strategyId
        doctrine = $run.doctrine
        victory = $run.victory
        score = [Math]::Round([double]$run.totalScore, 1)
        durationSeconds = [Math]::Round([double]$run.durationSeconds, 1)
        firstLeakWave = $run.firstLeakWave
        escapes = $run.escapedEnemies
        integrity = $run.integrityRemaining
        hottestRoute = $run.hottestRoute
        scenarioConversionPct = [Math]::Round([double]$run.scenarioConversionPct, 1)
        coverage = [Math]::Round([double]$run.coverageScore, 1)
        counter = [Math]::Round([double]$run.counterScore, 1)
        output = [Math]::Round([double]$run.outputScore, 1)
        economy = [Math]::Round([double]$run.economyScore, 1)
        command = [Math]::Round([double]$run.commandScore, 1)
        towersBuilt = $run.towersBuilt
        upgrades = $run.upgradesPurchased
        topTower = if ($null -eq $topTower) { "none" } else { $topTower.towerId }
        topTowerContribution = if ($null -eq $topTower) { 0 } else { [Math]::Round([double]$topTower.contributionScore, 1) }
        signature = $run.strategySignature
    }
}
$runRows | Export-Csv -LiteralPath $runCsvPath -NoTypeInformation -Encoding UTF8

$curveRows = foreach ($level in $matrix.levelSummaries) {
    $standard = Get-DifficultySummary -Level $level -DifficultyId "standard"
    $veteran = Get-DifficultySummary -Level $level -DifficultyId "veteran"
    $ember = Get-DifficultySummary -Level $level -DifficultyId "ember_trial"
    [pscustomobject]@{
        level = $level.levelIndex
        exam = $level.milestoneExam
        authoredPressure = [Math]::Round([double]$level.authoredPressure, 1)
        standardWinPct = [Math]::Round([double]$standard.winRatePct, 1)
        standardScore = [Math]::Round([double]$standard.medianScore, 1)
        standardDuration = [Math]::Round([double]$standard.medianDurationSeconds, 1)
        standardFirstLeak = [Math]::Round([double]$standard.medianFirstLeakWave, 1)
        veteranWinPct = [Math]::Round([double]$veteran.winRatePct, 1)
        veteranScore = [Math]::Round([double]$veteran.medianScore, 1)
        veteranDuration = [Math]::Round([double]$veteran.medianDurationSeconds, 1)
        veteranFirstLeak = [Math]::Round([double]$veteran.medianFirstLeakWave, 1)
        emberWinPct = [Math]::Round([double]$ember.winRatePct, 1)
        emberScore = [Math]::Round([double]$ember.medianScore, 1)
        emberDuration = [Math]::Round([double]$ember.medianDurationSeconds, 1)
        emberFirstLeak = [Math]::Round([double]$ember.medianFirstLeakWave, 1)
    }
}
$curveRows | Export-Csv -LiteralPath $curveCsvPath -NoTypeInformation -Encoding UTF8

$examRows = foreach ($exam in $matrix.examSummaries) {
    [pscustomobject]@{
        level = $exam.levelIndex
        strategies = $exam.strategyCount
        standardVictories = $exam.standardVictories
        distinctSuccessfulSignatures = $exam.distinctSuccessfulSignatures
        scoreSpread = [Math]::Round([double]$exam.standardScoreSpread, 1)
        successfulStrategies = @($exam.successfulStrategyIds) -join ";"
        successfulSignatures = @($exam.successfulSignatures) -join ";"
        pass = $exam.pass
    }
}
$examRows | Export-Csv -LiteralPath $examCsvPath -NoTypeInformation -Encoding UTF8

$difficultyRows = foreach ($difficultyId in @("standard", "veteran", "ember_trial")) {
    $matches = @($runRows | Where-Object difficulty -eq $difficultyId)
    [pscustomobject]@{
        difficulty = $difficultyId
        wins = @($matches | Where-Object victory).Count
        medianDuration = [Math]::Round((Get-Median $matches.durationSeconds), 1)
        medianFirstLeak = [Math]::Round((Get-Median $matches.firstLeakWave), 1)
        averageScenarioConversion = [Math]::Round(($matches.scenarioConversionPct | Measure-Object -Average).Average, 1)
        coverage = [Math]::Round(($matches.coverage | Measure-Object -Average).Average, 1)
        counter = [Math]::Round(($matches.counter | Measure-Object -Average).Average, 1)
        output = [Math]::Round(($matches.output | Measure-Object -Average).Average, 1)
        economy = [Math]::Round(($matches.economy | Measure-Object -Average).Average, 1)
        command = [Math]::Round(($matches.command | Measure-Object -Average).Average, 1)
    }
}
$warningCount = @($matrix.alarms | Where-Object severity -eq "WARNING").Count
$errorCount = @($matrix.alarms | Where-Object severity -eq "ERROR").Count

$examMetricRows = foreach ($examLevel in @(5, 9, 13, 17, 20)) {
    $matches = @($runRows | Where-Object { [int]$_.level -eq $examLevel -and $_.difficulty -eq "standard" })
    [pscustomobject]@{
        level = $examLevel
        routes = @($matches.hottestRoute | Sort-Object -Unique) -join ","
        topTowers = @($matches.topTower | Sort-Object -Unique) -join ","
        scenarioConversion = [Math]::Round(($matches.scenarioConversionPct | Measure-Object -Average).Average, 1)
        coverage = [Math]::Round(($matches.coverage | Measure-Object -Average).Average, 1)
        counter = [Math]::Round(($matches.counter | Measure-Object -Average).Average, 1)
        output = [Math]::Round(($matches.output | Measure-Object -Average).Average, 1)
        economy = [Math]::Round(($matches.economy | Measure-Object -Average).Average, 1)
        command = [Math]::Round(($matches.command | Measure-Object -Average).Average, 1)
    }
}

$consoleResult = Invoke-UnityTool -SessionId $sessionId -ToolName "read_console" -Id 30 -TimeoutSec 30 -Arguments @{
    action = "get"
    types = @("error", "warning")
    count = 200
}
Assert-UnitySuccess -Step "read Unity console" -Response $consoleResult
$consoleEntries = @((Get-StructuredContent $consoleResult).data)
$ignoredConsolePattern = "MCP-FOR-UNITY.*Unexpected receive error: WebSocket is not initialised"
$effectiveConsoleEntries = @($consoleEntries | Where-Object { [string]$_ -notmatch $ignoredConsolePattern })

$markdown = New-Object System.Text.StringBuilder
[void]$markdown.AppendLine("# P10.2 Automated Balance Report")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("Generated: $($matrix.generatedUtc)  ")
[void]$markdown.AppendLine("Mode: ``$($matrix.simulationMode)``  ")
[void]$markdown.AppendLine("Seed: ``$Seed``  ")
[void]$markdown.AppendLine("Fingerprint: ``$($matrix.fingerprint)``  ")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## Release Gate")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("| Check | Result |")
[void]$markdown.AppendLine("|---|---:|")
[void]$markdown.AppendLine("| Completed runs | $($matrix.completedRuns) / 180 |")
[void]$markdown.AppendLine("| Stalled waves | $($matrix.stalledRuns) |")
[void]$markdown.AppendLine("| Unity console issues | $($effectiveConsoleEntries.Count) |")
[void]$markdown.AppendLine("| Deterministic repeat | $deterministic |")
[void]$markdown.AppendLine("| Milestone strategy exams | $(@($matrix.examSummaries | Where-Object pass).Count) / 5 |")
[void]$markdown.AppendLine("| Curve alarms | $(@($matrix.alarms).Count) |")
[void]$markdown.AppendLine("| Curve warnings / errors | $warningCount / $errorCount |")
[void]$markdown.AppendLine("| Hard gate | $($matrix.hardPass) |")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## Difficulty Summary")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("| Difficulty | Wins | Median duration | Median first leak | Scenario conversion | Coverage | Counter | Output | Economy | Command |")
[void]$markdown.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|")
foreach ($row in $difficultyRows) {
    [void]$markdown.AppendLine("| $($row.difficulty) | $($row.wins)/60 | $($row.medianDuration)s | W$($row.medianFirstLeak) | $($row.averageScenarioConversion)% | $($row.coverage) | $($row.counter) | $($row.output) | $($row.economy) | $($row.command) |")
}
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## Difficulty Curve")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("| L | Exam | Pressure | Standard W / Score / Leak | Veteran W / Score / Leak | Ember W / Score / Leak |")
[void]$markdown.AppendLine("|---:|:---:|---:|---:|---:|---:|")
foreach ($row in $curveRows) {
    $examMark = if ($row.exam) { "YES" } else { "" }
    [void]$markdown.AppendLine("| $($row.level) | $examMark | $($row.authoredPressure) | $($row.standardWinPct)% / $($row.standardScore) / W$($row.standardFirstLeak) | $($row.veteranWinPct)% / $($row.veteranScore) / W$($row.veteranFirstLeak) | $($row.emberWinPct)% / $($row.emberScore) / W$($row.emberFirstLeak) |")
}
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## Milestone Exams")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("| L | Standard wins | Distinct signatures | Score spread | Successful strategies | Pass |")
[void]$markdown.AppendLine("|---:|---:|---:|---:|---|:---:|")
foreach ($row in $examRows) {
    [void]$markdown.AppendLine("| $($row.level) | $($row.standardVictories)/3 | $($row.distinctSuccessfulSignatures) | $($row.scoreSpread) | $($row.successfulStrategies) | $($row.pass) |")
}
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## Exam Decision Metrics")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("| L | Hottest routes | Top contributing towers | Scenario | Coverage | Counter | Output | Economy | Command |")
[void]$markdown.AppendLine("|---:|---|---|---:|---:|---:|---:|---:|---:|")
foreach ($row in $examMetricRows) {
    [void]$markdown.AppendLine("| $($row.level) | $($row.routes) | $($row.topTowers) | $($row.scenarioConversion)% | $($row.coverage) | $($row.counter) | $($row.output) | $($row.economy) | $($row.command) |")
}
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## Curve Alarms")
[void]$markdown.AppendLine()
if (@($matrix.alarms).Count -eq 0) {
    [void]$markdown.AppendLine("No spike, flat-mission, strategy-collapse, difficulty-inversion, or stall alarms were raised.")
} else {
    foreach ($alarm in $matrix.alarms) {
        [void]$markdown.AppendLine("- **$($alarm.severity) $($alarm.code), L$($alarm.levelIndex)**: $($alarm.message) $($alarm.evidence)")
    }
}
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## Strategy Definitions")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("- ``focused_fire``: Ember Surge, damage branches, armor/heavy focus, conservative scenario spending.")
[void]$markdown.AppendLine("- ``control_lattice``: Fracture Mark, utility branches, fast/swarm control, aggressive scenario spending.")
[void]$markdown.AppendLine("- ``adaptive_network``: mixed branches, broad counter coverage, economy-first scenario timing.")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("## Method")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("This is a deterministic fast-rules balance simulation, not a claim of 180 rendered real-time sessions. Unity loads the shipping campaign, wave, enemy, difficulty, chapter-remix, scenario, tower-stat, specialization and unlock data. Each run allocates budget wave by wave, evaluates route pressure against tower output/control/counters, and records the same decision-facing metrics used by the post-battle review. Use real-time MCP playtests as calibration anchors after any alarm or major combat-rule change.")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("Raw artifacts: ``p102_balance_matrix.json``, ``p102_runs.csv``, ``p102_level_curve.csv``, and ``p102_exam_strategies.csv``.")
$markdown.ToString() | Set-Content -LiteralPath $reportMarkdownPath -Encoding UTF8

$audit = [ordered]@{
    schemaVersion = "p102-audit-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $runStartedUtc).TotalSeconds, 2)
    seed = $Seed
    fingerprint = [string]$matrix.fingerprint
    repeatFingerprint = $repeatFingerprint
    deterministic = $deterministic
    totalRuns = [int]$matrix.totalRuns
    completedRuns = [int]$matrix.completedRuns
    stalledRuns = [int]$matrix.stalledRuns
    milestoneExamsPassed = @($matrix.examSummaries | Where-Object pass).Count
    alarmCount = @($matrix.alarms).Count
    warningCount = $warningCount
    errorCount = $errorCount
    curveStatus = [string]$matrix.curveStatus
    hardPass = [bool]$matrix.hardPass
    effectiveConsoleIssueCount = $effectiveConsoleEntries.Count
    effectiveConsoleIssues = $effectiveConsoleEntries
    artifacts = [ordered]@{
        matrixJson = $matrixJsonPath
        runsCsv = $runCsvPath
        curveCsv = $curveCsvPath
        examsCsv = $examCsvPath
        reportMarkdown = $reportMarkdownPath
    }
}
$audit | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $auditJsonPath -Encoding UTF8
$audit | ConvertTo-Json -Depth 20

if (-not $deterministic) {
    throw "P10.2 deterministic repeat mismatch: $($matrix.fingerprint) vs $repeatFingerprint."
}
if ([int]$matrix.completedRuns -ne 180 -or [int]$matrix.stalledRuns -ne 0) {
    throw "P10.2 matrix incomplete: completed=$($matrix.completedRuns), stalled=$($matrix.stalledRuns)."
}
if (-not [bool]$matrix.hardPass) {
    throw "P10.2 matrix hard gate failed. Inspect $reportMarkdownPath."
}
if ($effectiveConsoleEntries.Count -gt 0) {
    throw "P10.2 Unity console has $($effectiveConsoleEntries.Count) effective issue(s). Inspect $auditJsonPath."
}
