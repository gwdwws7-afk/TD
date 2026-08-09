param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [int]$LevelIndex = 9,
    [string]$OutputDirectory = "E:/TD/output/playtest/p82_contract_audit",
    [switch]$RefreshScripts,
    [switch]$KeepPlaying
)

$ErrorActionPreference = "Stop"
$playtestScript = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$boardArguments = @{
    McpUrl = $McpUrl
    LevelIndex = $LevelIndex
    DurationSeconds = 1
    ScreenshotPath = Join-Path $OutputDirectory "p82_mission_board.png"
    SummaryPath = Join-Path $OutputDirectory "p82_mission_board.json"
    ExpectState = "p8.ui.open=True;p8.2.content.contracts=20/20;p8.2.content.mutators=20/20;p8.2.audit.contractPersists=True;p8.2.audit.pass=True"
    SkipAutoBuild = $true
    SkipStartWave = $true
    KeepMissionBoardOpen = $true
    RunCampaignProgressAudit = $true
}

if ($RefreshScripts) {
    $boardArguments.RefreshScripts = $true
}

& $playtestScript @boardArguments
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "P8.2 mission board audit failed with exit code $LASTEXITCODE."
}

$resultArguments = @{
    McpUrl = $McpUrl
    LevelIndex = $LevelIndex
    DurationSeconds = 0
    ScreenshotPath = Join-Path $OutputDirectory "p82_contract_result.png"
    SummaryPath = Join-Path $OutputDirectory "p82_contract_result.json"
    ExpectState = "p8.2.contract.completed=True;p8.result.contract=True;uiTextOverflow=0"
    SkipAutoBuild = $true
    SkipStartWave = $true
    ForceVictoryResult = $true
}

if ($KeepPlaying) {
    $resultArguments.KeepPlaying = $true
}

& $playtestScript @resultArguments
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "P8.2 contract result audit failed with exit code $LASTEXITCODE."
}
