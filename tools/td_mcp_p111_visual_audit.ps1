param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [int]$LevelIndex = 20,
    [string]$OutputDirectory = "E:/TD/output/playtest/p11"
)

$ErrorActionPreference = "Stop"
$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$runs = @(
    @{ name = "p11_1280_prep"; width = 1280; height = 720 },
    @{ name = "p11_960_prep"; width = 960; height = 540 }
)

$results = @()
foreach ($run in $runs) {
    $screenshot = (Join-Path $OutputDirectory ($run.name + ".png")).Replace("\", "/")
    $summary = Join-Path $OutputDirectory ($run.name + ".json")
    $result = & $runner `
        -McpUrl $McpUrl `
        -LevelIndex $LevelIndex `
        -DurationSeconds 2 `
        -ViewportWidth $run.width `
        -ViewportHeight $run.height `
        -SkipAutoBuild `
        -SkipStartWave `
        -RunP111Audit `
        -PreserveCampaignProgress `
        -ScreenshotPath $screenshot `
        -SummaryPath $summary | ConvertFrom-Json

    $results += [ordered]@{
        name = $run.name
        viewport = "$($run.width)x$($run.height)"
        screenshot = $screenshot
        summary = [IO.Path]::GetFullPath($summary)
        uiBounds = [bool]$result.checks.uiBounds
        uiOverlap = [bool]$result.checks.uiOverlap
        uiTextFit = [bool]$result.checks.uiTextFit
        p111Audit = [bool]$result.checks.p111Audit
        consoleClean = [bool]$result.checks.consoleClean
    }
}

$pass = @($results | Where-Object {
    -not ($_.uiBounds -and $_.uiOverlap -and $_.uiTextFit -and $_.p111Audit -and $_.consoleClean)
}).Count -eq 0

[ordered]@{
    p111VisualAudit = $pass
    runs = $results
} | ConvertTo-Json -Depth 6
