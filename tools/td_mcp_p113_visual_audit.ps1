param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [int]$LevelIndex = 9,
    [string]$OutputDirectory = "E:/TD/output/playtest/p11"
)

$ErrorActionPreference = "Stop"
$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
$alphaAuditTool = Join-Path $PSScriptRoot "audit_sprite_alpha_holes.py"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$alphaAudit = & python $alphaAuditTool | ConvertFrom-Json
if (-not $alphaAudit.pass) {
    throw "Sprite alpha-hole audit failed: $($alphaAudit.unexpected | ConvertTo-Json -Compress)"
}

$runs = @(
    @{ name = "p113_towers_1280"; width = 1280; height = 720; refresh = $true; duration = 0; timeScale = 0.20 },
    @{ name = "p113_towers_960"; width = 960; height = 540; refresh = $false; duration = 0; timeScale = 0.20 },
    @{ name = "p113_live_1280"; width = 1280; height = 720; refresh = $false; duration = 2; timeScale = 1.00 }
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
        PrepareP113Presentation = $true
        RunP113Audit = $true
        PreserveCampaignProgress = $true
        ScreenshotPath = $screenshot
        SummaryPath = $summary
    }
    if ($run.refresh) {
        $arguments.RefreshScripts = $true
    }

    $result = & $runner @arguments | ConvertFrom-Json
    $results += [ordered]@{
        name = $run.name
        viewport = "$($run.width)x$($run.height)"
        screenshot = $screenshot
        summary = [IO.Path]::GetFullPath($summary)
        foundations = [bool]$result.checks.p113Audit
        uiBounds = [bool]$result.checks.uiBounds
        uiOverlap = [bool]$result.checks.uiOverlap
        uiTextFit = [bool]$result.checks.uiTextFit
        consoleClean = [bool]$result.checks.consoleClean
    }
}

$pass = [bool]$alphaAudit.pass -and @($results | Where-Object {
    -not ($_.foundations -and $_.uiBounds -and $_.uiOverlap -and $_.uiTextFit -and $_.consoleClean)
}).Count -eq 0

$index = [ordered]@{
    schemaVersion = "p113-visual-audit-v1"
    p113VisualAudit = $pass
    alphaHoleAudit = [ordered]@{
        pass = [bool]$alphaAudit.pass
        scannedFrames = [int]$alphaAudit.scannedFrames
        scannedGroups = [int]$alphaAudit.scannedGroups
        knownRepairs = $alphaAudit.knownRepairs
        unexpected = $alphaAudit.unexpected
    }
    runs = $results
}
$indexPath = Join-Path $OutputDirectory "p113_visual_index.json"
$index | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $indexPath -Encoding UTF8
$index | ConvertTo-Json -Depth 8

if (-not $pass) {
    throw "P11.3 visual audit failed. Inspect $indexPath."
}
