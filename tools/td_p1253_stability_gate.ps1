param(
    [string]$PlayerPath = "E:/TD/output/builds/p1253_il2cpp/EmberlineDefense.exe",
    [string]$OutputRoot = "E:/TD/output/playtest/p1253_stability",
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [ValidateRange(1, 20)]
    [int]$LevelIndex = 20,
    [ValidateRange(1, 20)]
    [int]$MinimumWave = 14,
    [ValidateRange(15, 1200)]
    [int]$SampleSeconds = 45,
    [ValidateRange(60, 1800)]
    [int]$TimeoutSeconds = 360,
    [ValidateRange(1, 20)]
    [float]$WarmupTimeScale = 16,
    [ValidateRange(15, 120)]
    [float]$TargetAverageFps = 55,
    [ValidateRange(256, 8192)]
    [int]$MaximumMemoryMegabytes = 1536,
    [ValidateRange(0, 5000)]
    [int]$TechnicalIntegrity = 5000,
    [float[]]$TimeScales = @(1, 2, 3),
    [switch]$SkipMcpAudit
)

$ErrorActionPreference = "Stop"
$startedUtc = [DateTime]::UtcNow
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$workspacePrefix = $projectRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$outputFull = [IO.Path]::GetFullPath($OutputRoot)
$playerFull = [IO.Path]::GetFullPath($PlayerPath)
if (-not $outputFull.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must stay inside the workspace: $outputFull"
}
if (-not (Test-Path -LiteralPath $playerFull)) {
    throw "IL2CPP player was not found: $playerFull"
}
$gameAssemblyPath = Join-Path (Split-Path -Parent $playerFull) "GameAssembly.dll"
if (-not (Test-Path -LiteralPath $gameAssemblyPath)) {
    throw "IL2CPP GameAssembly.dll was not found beside the player: $gameAssemblyPath"
}
if ($TimeScales.Count -eq 0 -or
    @($TimeScales | Where-Object { $_ -lt 1 -or $_ -gt 3 }).Count -gt 0) {
    throw "TimeScales must contain only shipping playback speeds from 1 through 3."
}

New-Item -ItemType Directory -Path $outputFull -Force | Out-Null
$auditPath = Join-Path $outputFull "p1253_stability_audit.json"
$markdownPath = Join-Path $outputFull "p1253_stability_report.md"
$mcpAuditPath = Join-Path $outputFull "p1253_save_recovery_mcp.json"
$prefsBackupPath = Join-Path $outputFull "playerprefs-before-gate.reg"
$recoveryBackupPath = Join-Path $outputFull "campaign-recovery-before-gate"
$playerPrefsKey = "HKCU\Software\Emberline\Emberline Defense"
$persistentRoot = [IO.Path]::GetFullPath(
    (Join-Path $env:USERPROFILE "AppData/LocalLow/Emberline/Emberline Defense"))
$recoveryRoot = [IO.Path]::GetFullPath((Join-Path $persistentRoot "CampaignRecovery"))
$safePersistentPrefix = $persistentRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if (-not $recoveryRoot.StartsWith($safePersistentPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe campaign recovery path: $recoveryRoot"
}

$hadPlayerPrefs = (& reg.exe query $playerPrefsKey 2>$null) -ne $null
if ($hadPlayerPrefs) {
    & reg.exe export $playerPrefsKey $prefsBackupPath /y | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not back up PlayerPrefs before the P12.5.3 gate."
    }
}
$hadRecoveryDirectory = Test-Path -LiteralPath $recoveryRoot
if ($hadRecoveryDirectory) {
    Copy-Item -LiteralPath $recoveryRoot -Destination $recoveryBackupPath -Recurse -Force
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
            clientInfo = @{ name = "td-p1253-stability-gate"; version = "1.0" }
        }
    } | ConvertTo-Json -Depth 20
    $response = Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -Body $body -UseBasicParsing -TimeoutSec 20
    $sessionId = $response.Headers["Mcp-Session-Id"]
    if ([string]::IsNullOrWhiteSpace($sessionId)) {
        throw "MCP initialize did not return a session id."
    }
    $headers["Mcp-Session-Id"] = $sessionId
    $notification = @{
        jsonrpc = "2.0"
        method = "notifications/initialized"
        params = @{}
    } | ConvertTo-Json -Depth 5
    try {
        Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -Body $notification -UseBasicParsing -TimeoutSec 10 | Out-Null
    } catch {
    }
    return $sessionId
}

function Invoke-McpTool {
    param(
        [string]$Url,
        [string]$SessionId,
        [string]$Name,
        [hashtable]$Arguments,
        [int]$Id,
        [int]$Timeout = 60
    )
    $headers = @{
        Accept = "application/json, text/event-stream"
        "Content-Type" = "application/json"
        "Mcp-Session-Id" = $SessionId
    }
    $body = @{
        jsonrpc = "2.0"
        id = $Id
        method = "tools/call"
        params = @{ name = $Name; arguments = $Arguments }
    } | ConvertTo-Json -Depth 80
    $response = Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -Body $body -UseBasicParsing -TimeoutSec $Timeout
    return ConvertFrom-McpEvent $response.Content
}

function Get-McpResultText {
    param($Response)
    if ($null -eq $Response.result -or $null -eq $Response.result.structuredContent) {
        throw "MCP response did not contain structured content."
    }
    $content = $Response.result.structuredContent
    if ($null -ne $content.PSObject.Properties["success"] -and -not [bool]$content.success) {
        throw "MCP call failed: $($content | ConvertTo-Json -Depth 12 -Compress)"
    }
    return [string]$content.data.result
}

$mcpAudit = $null
$rows = @()
$restoreErrors = @()
$gateError = ""
try {
    if (-not $SkipMcpAudit) {
        $sessionId = New-McpSession -Url $McpUrl
        $recoveryResponse = Invoke-McpTool -Url $McpUrl -SessionId $sessionId -Name "execute_code" -Id 10 -Arguments @{
            action = "execute"
            code = @'
var audit = TD.TDCampaignProgression.DebugAuditRecoveryForTest(20);
return UnityEngine.JsonUtility.ToJson(audit, true);
'@
            safety_checks = $true
            compiler = "auto"
        }
        $mcpAudit = (Get-McpResultText -Response $recoveryResponse) | ConvertFrom-Json
        $mcpAudit | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $mcpAuditPath -Encoding utf8
        if (-not [bool]$mcpAudit.passed) {
            throw "MCP save recovery audit failed. Inspect $mcpAuditPath"
        }
    }

    foreach ($timeScale in $TimeScales) {
        $scaleToken = $timeScale.ToString([Globalization.CultureInfo]::InvariantCulture)
        $rowDirectory = Join-Path $outputFull "${scaleToken}x"
        New-Item -ItemType Directory -Path $rowDirectory -Force | Out-Null
        $reportPath = Join-Path $rowDirectory "stability-report.json"
        $logPath = Join-Path $rowDirectory "player.log"
        foreach ($path in @($reportPath, $logPath)) {
            if (Test-Path -LiteralPath $path) {
                Remove-Item -LiteralPath $path -Force
            }
        }

        $arguments = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 " +
                     "-logFile `"$logPath`" --td-p1253-stability-test " +
                     "--td-smoke-report `"$reportPath`" --td-smoke-level $LevelIndex " +
                     "--td-smoke-time-scale $scaleToken --td-smoke-timeout $TimeoutSeconds " +
                     "--td-smoke-technical-integrity $TechnicalIntegrity " +
                     "--td-stability-min-wave $MinimumWave --td-stability-warmup-scale $WarmupTimeScale " +
                     "--td-stability-sample-seconds $SampleSeconds " +
                     "--td-stability-target-fps $TargetAverageFps " +
                     "--td-stability-max-memory-mb $MaximumMemoryMegabytes"
        Write-Output "[P12.5.3] Running L$LevelIndex dense-wave sample at ${scaleToken}x..."
        $player = Start-Process -FilePath $playerFull -ArgumentList $arguments -PassThru -WindowStyle Hidden
        if (-not $player.WaitForExit(($TimeoutSeconds + 45) * 1000)) {
            Stop-Process -Id $player.Id -Force
            throw "P12.5.3 ${scaleToken}x player timed out."
        }
        if (-not (Test-Path -LiteralPath $reportPath)) {
            throw "P12.5.3 ${scaleToken}x player did not write a report."
        }

        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        $rows += [ordered]@{
            timeScale = [float]$timeScale
            exitCode = $player.ExitCode
            passed = $player.ExitCode -eq 0 -and [bool]$report.passed
            profileRestored = [bool]$report.profileRestored
            recoveryPassed = [bool]$report.saveRecovery.passed
            sampleStartWave = [int]$report.stabilitySampleStartWave
            sampleSeconds = [Math]::Round([double]$report.performance.actualRealSeconds, 2)
            averageFps = [Math]::Round([double]$report.performance.averageFps, 2)
            p95FrameMs = [Math]::Round([double]$report.performance.p95FrameMilliseconds, 2)
            p99FrameMs = [Math]::Round([double]$report.performance.p99FrameMilliseconds, 2)
            peakReservedMemoryMb = [Math]::Round([double]$report.performance.peakReservedMemoryBytes / 1MB, 2)
            peakEnemies = [int]$report.performance.peakActiveEnemies
            peakTowers = [int]$report.performance.peakActiveTowers
            peakProjectiles = [int]$report.performance.peakActiveProjectiles
            runtimeErrors = @($report.runtimeErrors).Count
            report = $reportPath
            log = $logPath
        }
    }
}
catch {
    $gateError = $_.Exception.Message
}
finally {
    try {
        & reg.exe delete $playerPrefsKey /f 2>$null | Out-Null
        if ($hadPlayerPrefs) {
            & reg.exe import $prefsBackupPath | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "PlayerPrefs registry restore returned exit code $LASTEXITCODE."
            }
        }
    } catch {
        $restoreErrors += $_.Exception.Message
    }

    try {
        if (Test-Path -LiteralPath $recoveryRoot) {
            Remove-Item -LiteralPath $recoveryRoot -Recurse -Force
        }
        if ($hadRecoveryDirectory) {
            Copy-Item -LiteralPath $recoveryBackupPath -Destination $recoveryRoot -Recurse -Force
        }
    } catch {
        $restoreErrors += $_.Exception.Message
    }
}

$allRowsPassed = $rows.Count -eq $TimeScales.Count -and
                 @($rows | Where-Object { -not $_.passed }).Count -eq 0
$audit = [ordered]@{
    schemaVersion = "p1253-stability-gate-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $startedUtc).TotalSeconds, 2)
    player = $playerFull
    playerSha256 = (Get-FileHash -LiteralPath $playerFull -Algorithm SHA256).Hash
    gameAssembly = $gameAssemblyPath
    gameAssemblySha256 = (Get-FileHash -LiteralPath $gameAssemblyPath -Algorithm SHA256).Hash
    scriptingBackend = "IL2CPP"
    levelIndex = $LevelIndex
    minimumWave = $MinimumWave
    sampleSeconds = $SampleSeconds
    timeScales = $TimeScales
    mcpAuditSkipped = [bool]$SkipMcpAudit
    mcpRecoveryPassed = [bool]$SkipMcpAudit -or ($null -ne $mcpAudit -and [bool]$mcpAudit.passed)
    playerPrefsRestored = $restoreErrors.Count -eq 0
    restoreErrors = $restoreErrors
    error = $gateError
    rows = $rows
    hardPass = $allRowsPassed -and
               ([bool]$SkipMcpAudit -or ($null -ne $mcpAudit -and [bool]$mcpAudit.passed)) -and
               $restoreErrors.Count -eq 0 -and
               [string]::IsNullOrWhiteSpace($gateError)
    artifacts = [ordered]@{
        audit = $auditPath
        report = $markdownPath
        mcpRecovery = $mcpAuditPath
    }
}
$audit | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $auditPath -Encoding utf8

$markdown = [Text.StringBuilder]::new()
[void]$markdown.AppendLine("# P12.5.3 Stability Gate")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("- Player: ``$playerFull``")
[void]$markdown.AppendLine("- Level: L$LevelIndex, dense-wave sampling from wave $MinimumWave")
[void]$markdown.AppendLine("- Window: $SampleSeconds real seconds at each shipping speed")
[void]$markdown.AppendLine("- Save recovery: $($audit.mcpRecoveryPassed)")
[void]$markdown.AppendLine("- Profile restored: $($audit.playerPrefsRestored)")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("| Speed | Pass | Avg FPS | P95 ms | P99 ms | Peak MB | Enemies | Towers | Projectiles |")
[void]$markdown.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |")
foreach ($row in $rows) {
    [void]$markdown.AppendLine(
        "| $($row.timeScale)x | $($row.passed) | $($row.averageFps) | $($row.p95FrameMs) | " +
        "$($row.p99FrameMs) | $($row.peakReservedMemoryMb) | $($row.peakEnemies) | " +
        "$($row.peakTowers) | $($row.peakProjectiles) |")
}
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("Hard pass: **$($audit.hardPass)**")
$markdown.ToString() | Set-Content -LiteralPath $markdownPath -Encoding utf8

$audit | ConvertTo-Json -Depth 20
if (-not $audit.hardPass) {
    throw "P12.5.3 stability gate failed. Inspect $auditPath"
}
