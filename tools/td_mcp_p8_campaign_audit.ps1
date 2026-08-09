param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [int]$LevelIndex = 1,
    [string]$OutputDirectory = "E:/TD/output/playtest/p8_campaign_audit",
    [switch]$RefreshScripts,
    [switch]$KeepPlaying
)

$ErrorActionPreference = "Stop"
$playtestScript = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
$screenshotPath = Join-Path $OutputDirectory "p8_campaign_board.png"
$summaryPath = Join-Path $OutputDirectory "p8_campaign_audit_summary.json"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$arguments = @{
    McpUrl = $McpUrl
    LevelIndex = $LevelIndex
    DurationSeconds = 1
    ScreenshotPath = $screenshotPath
    SummaryPath = $summaryPath
    ExpectState = "p8.ui.open=True;p8.ui.deploymentConfirmed=False;p8.ui.selected=$LevelIndex;p8.ui.levelButtons=20;p8.audit.pass=True"
    SkipAutoBuild = $true
    SkipStartWave = $true
    KeepMissionBoardOpen = $true
    RunCampaignProgressAudit = $true
}

if ($RefreshScripts) {
    $arguments.RefreshScripts = $true
}

if ($KeepPlaying) {
    $arguments.KeepPlaying = $true
}

& $playtestScript @arguments
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "P8 campaign audit failed with exit code $LASTEXITCODE."
}
