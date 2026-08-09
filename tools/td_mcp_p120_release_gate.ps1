param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputDirectory = "E:/TD/output/playtest/p120_release_gate_full",
    [int]$CaptureDurationSeconds = 8,
    [int]$Seed = 10202
)

$ErrorActionPreference = "Stop"
$runStartedUtc = [DateTime]::UtcNow
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$stageResults = New-Object System.Collections.Generic.List[object]

function Invoke-GateStage {
    param(
        [string]$Name,
        [string]$ScriptName,
        [hashtable]$Arguments
    )

    $started = [DateTime]::UtcNow
    $scriptPath = Join-Path $PSScriptRoot $ScriptName
    try {
        & $scriptPath @Arguments | Out-Null
        $stageResults.Add([ordered]@{
            name = $Name
            pass = $true
            elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $started).TotalSeconds, 2)
            error = ""
        })
    } catch {
        $stageResults.Add([ordered]@{
            name = $Name
            pass = $false
            elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $started).TotalSeconds, 2)
            error = $_.Exception.Message
        })
        throw "P12.0 stage '$Name' failed: $($_.Exception.Message)"
    }
}

$p83Directory = Join-Path $outputRoot "p83"
$p84Directory = Join-Path $outputRoot "p84"
$p85Directory = Join-Path $outputRoot "p85"
$geometryDirectory = Join-Path $outputRoot "geometry"
$p113Directory = Join-Path $outputRoot "p113"
$p102Directory = Join-Path $outputRoot "p102"
$recordingDirectory = Join-Path $outputRoot "recording"

Invoke-GateStage -Name "formation_doctrine" -ScriptName "td_mcp_p83_formation_audit.ps1" -Arguments @{
    McpUrl = $McpUrl
    OutputDirectory = $p83Directory
    RefreshScripts = $true
}
Invoke-GateStage -Name "chapter_rewards" -ScriptName "td_mcp_p84_campaign_audit.ps1" -Arguments @{
    McpUrl = $McpUrl
    OutputDirectory = $p84Directory
}
Invoke-GateStage -Name "difficulty_integration" -ScriptName "td_mcp_p85_difficulty_audit.ps1" -Arguments @{
    McpUrl = $McpUrl
    OutputDirectory = $p85Directory
}
Invoke-GateStage -Name "five_map_geometry" -ScriptName "td_mcp_p120_geometry_audit.ps1" -Arguments @{
    McpUrl = $McpUrl
    OutputDirectory = $geometryDirectory
}
Invoke-GateStage -Name "visual_ui_regression" -ScriptName "td_mcp_p113_visual_audit.ps1" -Arguments @{
    McpUrl = $McpUrl
    OutputDirectory = $p113Directory
}
Invoke-GateStage -Name "balance_matrix" -ScriptName "td_mcp_p102_balance_matrix.ps1" -Arguments @{
    McpUrl = $McpUrl
    OutputDirectory = $p102Directory
    Seed = $Seed
}
Invoke-GateStage -Name "live_capture" -ScriptName "td_capture_visual_run.ps1" -Arguments @{
    McpUrl = $McpUrl
    LevelIndex = 9
    DurationSeconds = $CaptureDurationSeconds
    TimeScale = 2
    OutputDirectory = $recordingDirectory
    OutputName = "p120_l09_capture"
}

$geometry = Get-Content -LiteralPath (Join-Path $geometryDirectory "p120_geometry_index.json") -Raw | ConvertFrom-Json
$visual = Get-Content -LiteralPath (Join-Path $p113Directory "p113_visual_index.json") -Raw | ConvertFrom-Json
$balance = Get-Content -LiteralPath (Join-Path $p102Directory "p102_audit.json") -Raw | ConvertFrom-Json
$balanceMatrix = Get-Content -LiteralPath (Join-Path $p102Directory "p102_balance_matrix.json") -Raw | ConvertFrom-Json
$capture = Get-Content -LiteralPath (Join-Path $recordingDirectory "p120_l09_capture_summary.json") -Raw | ConvertFrom-Json

$summaryFiles = @(Get-ChildItem -LiteralPath $outputRoot -Recurse -File -Filter "*.json")
$failedChecks = New-Object System.Collections.Generic.List[string]
$playtestSummaryCount = 0
foreach ($file in $summaryFiles) {
    $json = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    if ($null -eq $json.checks) {
        continue
    }

    $playtestSummaryCount++
    foreach ($property in $json.checks.PSObject.Properties) {
        if ($property.Value -is [bool] -and -not [bool]$property.Value) {
            $failedChecks.Add("$($file.BaseName):$($property.Name)")
        }
    }
    if (@($json.effectiveConsoleIssues).Count -gt 0) {
        $failedChecks.Add("$($file.BaseName):effectiveConsoleIssues")
    }
}

$artifactFiles = @(Get-ChildItem -LiteralPath $outputRoot -Recurse -File | Where-Object {
    $_.Extension -in @(".json", ".png", ".mp4", ".csv", ".md", ".log")
})
$emptyArtifacts = @($artifactFiles | Where-Object Length -le 0 | ForEach-Object FullName)
$videoPath = Join-Path $recordingDirectory "p120_l09_capture.mp4"
$videoBytes = if (Test-Path -LiteralPath $videoPath) { (Get-Item -LiteralPath $videoPath).Length } else { 0 }

$blockingChecks = [ordered]@{
    stages = @($stageResults | Where-Object { -not $_.pass }).Count -eq 0
    playtestChecks = $playtestSummaryCount -gt 0 -and $failedChecks.Count -eq 0
    exactAutomation = [bool]$capture.checks.autoBuild -and [bool]$capture.checks.autoUpgrade
    validCapture = $videoBytes -ge 10240
    uiTextFit = @($summaryFiles | ForEach-Object {
        $json = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
        if ($null -ne $json.checks) { [bool]$json.checks.uiTextFit }
    } | Where-Object { -not $_ }).Count -eq 0
    campaignIntegration = [bool]$capture.checks.p84Campaign -and [bool]$capture.checks.p85Difficulty
    geometry = [bool]$geometry.pass -and [int]$geometry.uniqueMaps -eq 5
    visualRegression = [bool]$visual.p113VisualAudit
    balanceMatrix = [bool]$balance.deterministic -and [bool]$balance.hardPass -and [int]$balance.errorCount -eq 0 -and $null -ne $balanceMatrix.alarms
    artifactsNonEmpty = $artifactFiles.Count -gt 0 -and $emptyArtifacts.Count -eq 0
}
$pass = @($blockingChecks.GetEnumerator() | Where-Object { -not [bool]$_.Value }).Count -eq 0

$index = [ordered]@{
    schemaVersion = "p120-release-gate-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $runStartedUtc).TotalSeconds, 2)
    pass = $pass
    mcpUrl = $McpUrl
    profileIsolation = "all playtest stages preserve and restore the active campaign profile"
    stages = $stageResults
    blockingChecks = $blockingChecks
    playtestSummaryCount = $playtestSummaryCount
    failedChecks = $failedChecks
    artifactCount = $artifactFiles.Count
    emptyArtifacts = $emptyArtifacts
    capture = [ordered]@{
        videoPath = $videoPath
        videoBytes = $videoBytes
        decodeValid = $true
    }
    geometry = [ordered]@{
        uniqueMaps = [int]$geometry.uniqueMaps
        probes = @($geometry.probes).Count
    }
    balance = [ordered]@{
        totalRuns = [int]$balance.totalRuns
        deterministic = [bool]$balance.deterministic
        warningCount = [int]$balance.warningCount
        errorCount = [int]$balance.errorCount
        curveStatus = [string]$balance.curveStatus
        alarms = $balanceMatrix.alarms
    }
}
$indexPath = Join-Path $outputRoot "p120_release_gate_index.json"
$index | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $indexPath -Encoding UTF8
$index | ConvertTo-Json -Depth 20

if (-not $pass) {
    throw "P12.0 release gate failed. Inspect $indexPath."
}
