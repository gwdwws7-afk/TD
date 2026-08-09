param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [int]$LevelIndex = 9,
    [string]$OutputDirectory = "E:/TD/output/playtest/p11"
)

$ErrorActionPreference = "Stop"
$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$runs = @(
    @{ name = "p112_enemy_1280"; width = 1280; height = 720; combat = $false; refresh = $true; duration = 1; timeScale = 0.35 },
    @{ name = "p112_enemy_960"; width = 960; height = 540; combat = $false; refresh = $false; duration = 1; timeScale = 0.35 },
    @{ name = "p112_combat_1280"; width = 1280; height = 720; combat = $true; refresh = $false; duration = 1; timeScale = 0.18 }
)

$results = @()
foreach ($run in $runs) {
    $screenshot = (Join-Path $OutputDirectory ($run.name + ".png")).Replace("\", "/")
    $summary = Join-Path $OutputDirectory ($run.name + ".json")
    $arguments = @{
        McpUrl = $McpUrl
        LevelIndex = $LevelIndex
        DurationSeconds = $run.duration
        TimeScale = $run.timeScale
        ViewportWidth = $run.width
        ViewportHeight = $run.height
        SkipAutoBuild = $true
        SkipStartWave = $true
        RunP112Audit = $true
        PreserveCampaignProgress = $true
        ScreenshotPath = $screenshot
        SummaryPath = $summary
    }
    if ($run.combat) {
        $arguments.PrepareP112Combat = $true
    } else {
        $arguments.PrepareP112Presentation = $true
    }
    if ($run.refresh) {
        $arguments.RefreshScripts = $true
    }

    $result = & $runner @arguments | ConvertFrom-Json
    $results += [ordered]@{
        name = $run.name
        viewport = "$($run.width)x$($run.height)"
        combat = [bool]$run.combat
        screenshot = $screenshot
        summary = [IO.Path]::GetFullPath($summary)
        uiBounds = [bool]$result.checks.uiBounds
        uiOverlap = [bool]$result.checks.uiOverlap
        uiTextFit = [bool]$result.checks.uiTextFit
        p112Audit = [bool]$result.checks.p112Audit
        consoleClean = [bool]$result.checks.consoleClean
    }
}

$pass = @($results | Where-Object {
    -not ($_.uiBounds -and $_.uiOverlap -and $_.uiTextFit -and $_.p112Audit -and $_.consoleClean)
}).Count -eq 0

[ordered]@{
    p112VisualAudit = $pass
    runs = $results
} | ConvertTo-Json -Depth 6
