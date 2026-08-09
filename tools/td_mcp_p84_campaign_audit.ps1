param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputDirectory = "E:/TD/output/playtest/p84_campaign_audit",
    [switch]$RefreshScripts
)

$ErrorActionPreference = "Stop"
$playtestScript = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$chapterArguments = @{
    McpUrl = $McpUrl
    LevelIndex = 1
    DurationSeconds = 1
    ScreenshotPath = Join-Path $OutputDirectory "p84_chapter_mastery.png"
    SummaryPath = Join-Path $OutputDirectory "p84_chapter_mastery.json"
    ExpectState = "p8.4.chapters.mastered=1;p8.4.rewards.claimed=1;p8.4.audit.chapterMastery=True;p8.4.audit.autoClaim=True;p8.4.audit.rewardRuntime=True;p8.4.audit.portableRoundTrip=True;p8.4.audit.clipboard=True;p8.4.audit.doubleConfirm=True;p8.4.audit.allTextFit=True;p8.4.audit.pass=True"
    SkipAutoBuild = $true
    SkipStartWave = $true
    KeepMissionBoardOpen = $true
    PrepareP84ChapterBoard = $true
    RunP84Audit = $true
    PreserveCampaignProgress = $true
}
if ($RefreshScripts) {
    $chapterArguments.RefreshScripts = $true
}

& $playtestScript @chapterArguments
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "P8.4 chapter mastery audit failed with exit code $LASTEXITCODE."
}

$profileArguments = @{
    McpUrl = $McpUrl
    LevelIndex = 1
    DurationSeconds = 1
    ScreenshotPath = Join-Path $OutputDirectory "p84_campaign_profile.png"
    SummaryPath = Join-Path $OutputDirectory "p84_campaign_profile.json"
    ExpectState = "uiActive.Campaign_Profile=True;p8.4.profile.open=True;p8.4.profile.previewValid=True;p8.4.rewards.claimed=1;uiTextOverflow=0"
    SkipAutoBuild = $true
    SkipStartWave = $true
    PrepareP84ChapterBoard = $true
    KeepCampaignProfileOpen = $true
    PreserveCampaignProgress = $true
}

& $playtestScript @profileArguments
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "P8.4 campaign profile audit failed with exit code $LASTEXITCODE."
}

$completionArguments = @{
    McpUrl = $McpUrl
    LevelIndex = 20
    DurationSeconds = 1
    ScreenshotPath = Join-Path $OutputDirectory "p84_campaign_complete.png"
    SummaryPath = Join-Path $OutputDirectory "p84_campaign_complete.json"
    ExpectState = "uiActive.Run_Result=True;p8.4.campaign.complete=True;p8.4.campaign.rank=S;p8.4.result.archive=True;p8.4.chapters.mastered=4;p8.4.rewards.claimed=4;nextMissionButton=Campaign Archive interactable=True;uiTextOverflow=0"
    SkipAutoBuild = $true
    SkipStartWave = $true
    PrepareP84CampaignCompletion = $true
    PreserveCampaignProgress = $true
}

& $playtestScript @completionArguments
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "P8.4 campaign completion audit failed with exit code $LASTEXITCODE."
}

$index = [ordered]@{
    chapterMastery = Join-Path $OutputDirectory "p84_chapter_mastery.json"
    campaignProfile = Join-Path $OutputDirectory "p84_campaign_profile.json"
    campaignComplete = Join-Path $OutputDirectory "p84_campaign_complete.json"
    profilePreserved = $true
}
$indexPath = Join-Path $OutputDirectory "p84_audit_index.json"
$index | ConvertTo-Json -Depth 8 | Set-Content -Path $indexPath -Encoding UTF8
$index | ConvertTo-Json -Depth 8
