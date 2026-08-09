param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputDirectory = "E:/TD/output/playtest/p85_difficulty_audit",
    [switch]$RefreshScripts
)

$ErrorActionPreference = "Stop"
$playtestScript = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

function Invoke-DifficultyProbe {
    param(
        [string]$Difficulty,
        [string]$ExpectedRuntime,
        [switch]$RunAudit,
        [switch]$Refresh
    )

    $slug = $Difficulty.ToLowerInvariant()
    $displayDifficulty = if ($Difficulty -eq "EmberTrial") { "EMBER TRIAL" } else { $Difficulty.ToUpperInvariant() }
    $arguments = @{
        McpUrl = $McpUrl
        LevelIndex = 1
        DurationSeconds = 1
        ScreenshotPath = Join-Path $OutputDirectory "p85_$slug`_formation.png"
        SummaryPath = Join-Path $OutputDirectory "p85_$slug`_formation.json"
        ExpectState = "uiActive.Prebattle_Formation=True;p8.5.active=$Difficulty;p8.5.runtime=$ExpectedRuntime;p8.5.audit.runtimeMatches=True;uiText.Formation_Difficulty=$displayDifficulty;uiTextOverflow=0"
        FormationDifficulty = $Difficulty
        SkipAutoBuild = $true
        SkipStartWave = $true
        KeepFormationOpen = $true
        PrepareP85Difficulty = $true
        PreserveCampaignProgress = $true
    }
    if ($RunAudit) {
        $arguments.RunP85Audit = $true
        $arguments.ExpectState += ";p8.5.audit.portableRoundTrip=True;p8.5.audit.ui=True;p8.5.audit.fullChallenge=True;p8.5.audit.pass=True"
    }
    if ($Refresh) {
        $arguments.RefreshScripts = $true
    }

    & $playtestScript @arguments
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "P8.5 $Difficulty probe failed with exit code $LASTEXITCODE."
    }
}

Invoke-DifficultyProbe `
    -Difficulty "Standard" `
    -ExpectedRuntime "budget:140,integrity:20,hpX:1,speedX:1,armor:0,rewardX:1,resonanceX:1" `
    -RunAudit `
    -Refresh:$RefreshScripts
Invoke-DifficultyProbe `
    -Difficulty "Veteran" `
    -ExpectedRuntime "budget:125,integrity:20,hpX:1.15,speedX:1.113,armor:1,rewardX:1.1,resonanceX:1"
Invoke-DifficultyProbe `
    -Difficulty "EmberTrial" `
    -ExpectedRuntime "budget:115,integrity:18,hpX:1.3,speedX:1.166,armor:2,rewardX:1.25,resonanceX:1.1"

$perfectedArguments = @{
    McpUrl = $McpUrl
    LevelIndex = 20
    DurationSeconds = 1
    ScreenshotPath = Join-Path $OutputDirectory "p85_campaign_perfected.png"
    SummaryPath = Join-Path $OutputDirectory "p85_campaign_perfected.json"
    ExpectState = "uiActive.Run_Result=True;uiText.Run_Result_Title=CAMPAIGN PERFECTED   EMBER TRIAL;p8.5.active=EmberTrial;p8.5.progress.veteran=20/20;p8.5.progress.ember=20/20;p8.5.audit.runtimeMatches=True;nextMissionButton=Campaign Archive interactable=True;uiTextOverflow=0"
    FormationDifficulty = "EmberTrial"
    SkipAutoBuild = $true
    SkipStartWave = $true
    PrepareP85CampaignPerfected = $true
    PreserveCampaignProgress = $true
}

& $playtestScript @perfectedArguments
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "P8.5 campaign perfected probe failed with exit code $LASTEXITCODE."
}

$index = [ordered]@{
    standard = Join-Path $OutputDirectory "p85_standard_formation.json"
    veteran = Join-Path $OutputDirectory "p85_veteran_formation.json"
    emberTrial = Join-Path $OutputDirectory "p85_embertrial_formation.json"
    campaignPerfected = Join-Path $OutputDirectory "p85_campaign_perfected.json"
    profilePreserved = $true
}
$indexPath = Join-Path $OutputDirectory "p85_audit_index.json"
$index | ConvertTo-Json -Depth 8 | Set-Content -Path $indexPath -Encoding UTF8
$index | ConvertTo-Json -Depth 8
