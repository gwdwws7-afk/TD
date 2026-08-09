param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [int]$LevelIndex = 16,
    [string]$OutputDirectory = "E:/TD/output/playtest/p83_formation_audit",
    [switch]$RefreshScripts
)

$ErrorActionPreference = "Stop"
$playtestScript = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$formationArguments = @{
    McpUrl = $McpUrl
    LevelIndex = $LevelIndex
    DurationSeconds = 1
    ScreenshotPath = Join-Path $OutputDirectory "p83_formation.png"
    SummaryPath = Join-Path $OutputDirectory "p83_formation.json"
    ExpectState = "missionCloseButton=Formation Required interactable=False;p8.3.doctrine.available=True;p8.3.audit.autoFits=20/20;p8.3.audit.allFormationTextFit=True;p8.3.audit.pass=True"
    SkipAutoBuild = $true
    SkipStartWave = $true
    KeepFormationOpen = $true
    RunCampaignProgressAudit = $true
    PreserveCampaignProgress = $true
}

if ($RefreshScripts) {
    $formationArguments.RefreshScripts = $true
}

& $playtestScript @formationArguments
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "P8.3 formation UI audit failed with exit code $LASTEXITCODE."
}

$emberArguments = @{
    McpUrl = $McpUrl
    LevelIndex = $LevelIndex
    DurationSeconds = 2
    BuildPlan = "1,1:RailLancer;4,2:SiegeDrill;8,1:CinderMortar;12,1:ResonanceBeacon"
    BonusBudget = 700
    FormationDoctrine = "EmberSurge"
    ResonanceCommand = "EmberSurge"
    EnemyPlan = "carapace_brute:8:default:0.24:12"
    ScreenshotPath = Join-Path $OutputDirectory "p83_ember_battle.png"
    SummaryPath = Join-Path $OutputDirectory "p83_ember_battle.json"
    ExpectState = "p8.3.doctrine=EmberSurge;p8.3.doctrine.livePower=1.10;p8.3.doctrine.empoweredCommands=1;p8.3.formation.slots=4/4"
    PreserveCampaignProgress = $true
}

& $playtestScript @emberArguments
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "P8.3 Ember doctrine audit failed with exit code $LASTEXITCODE."
}

$fractureArguments = @{
    McpUrl = $McpUrl
    LevelIndex = $LevelIndex
    DurationSeconds = 2
    BuildPlan = "1,1:FrostCoil;4,2:EmberFlak;8,1:CinderMortar;12,1:GravSnare"
    BonusBudget = 700
    FormationDoctrine = "FractureMark"
    ResonanceCommand = "FractureMark"
    EnemyPlan = "skitter_runner:12:default:0.24:12"
    ScreenshotPath = Join-Path $OutputDirectory "p83_fracture_battle.png"
    SummaryPath = Join-Path $OutputDirectory "p83_fracture_battle.json"
    ExpectState = "p8.3.doctrine=FractureMark;p8.3.doctrine.livePower=1.10;p8.3.doctrine.empoweredCommands=1;p8.3.formation.slots=4/4"
    PreserveCampaignProgress = $true
}

& $playtestScript @fractureArguments
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "P8.3 Fracture doctrine audit failed with exit code $LASTEXITCODE."
}

$index = [ordered]@{
    levelIndex = $LevelIndex
    formation = Join-Path $OutputDirectory "p83_formation.json"
    ember = Join-Path $OutputDirectory "p83_ember_battle.json"
    fracture = Join-Path $OutputDirectory "p83_fracture_battle.json"
    profilePreserved = $true
}
$indexPath = Join-Path $OutputDirectory "p83_audit_index.json"
$index | ConvertTo-Json -Depth 8 | Set-Content -Path $indexPath -Encoding UTF8
$index | ConvertTo-Json -Depth 8
