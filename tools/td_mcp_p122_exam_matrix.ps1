param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputDirectory = "E:/TD/output/playtest/p122_exam_matrix",
    [switch]$RefreshScripts
)

$ErrorActionPreference = "Stop"
$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$examLevels = @(5, 9, 13, 17, 20)
$runs = New-Object System.Collections.Generic.List[object]
$refreshPending = $RefreshScripts.IsPresent

function Invoke-P122Pass {
    param(
        [int]$LevelIndex,
        [string]$Name,
        [int]$Width,
        [int]$Height,
        [switch]$ShowResult,
        [switch]$Refresh
    )

    $arguments = @{
        McpUrl = $McpUrl
        LevelIndex = $LevelIndex
        DurationSeconds = 0
        TimeScale = 1.0
        ViewportWidth = $Width
        ViewportHeight = $Height
        ScreenshotPath = "$OutputDirectory/$Name.png"
        SummaryPath = "$OutputDirectory/$Name.json"
        SkipAutoBuild = $true
        SkipStartWave = $true
        PrepareP122Exam = $true
        PreserveCampaignProgress = $true
    }
    if ($ShowResult) {
        $arguments.ForceRunResult = $true
        $arguments.RunP122Audit = $true
    }
    if ($Refresh) {
        $arguments.RefreshScripts = $true
    }

    & $runner @arguments | Out-Null
    $summary = Get-Content -LiteralPath $arguments.SummaryPath -Raw | ConvertFrom-Json
    return [pscustomobject]@{
        name = $Name
        levelIndex = $LevelIndex
        width = $Width
        height = $Height
        result = [bool]$ShowResult
        pass = [bool]$summary.checks.screenshot -and
               [bool]$summary.checks.consoleClean -and
               [bool]$summary.checks.uiBounds -and
               [bool]$summary.checks.uiOverlap -and
               [bool]$summary.checks.uiTextFit -and
               (-not $ShowResult -or [bool]$summary.checks.p122Audit)
        screenshot = $arguments.ScreenshotPath
        summary = $arguments.SummaryPath
    }
}

foreach ($level in $examLevels) {
    $prefix = "p122_l{0:00}" -f $level
    $runs.Add((Invoke-P122Pass -LevelIndex $level -Name "${prefix}_combat_1280" -Width 1280 -Height 720 -Refresh:$refreshPending))
    $refreshPending = $false
    $runs.Add((Invoke-P122Pass -LevelIndex $level -Name "${prefix}_result_1280" -Width 1280 -Height 720 -ShowResult))
    $runs.Add((Invoke-P122Pass -LevelIndex $level -Name "${prefix}_result_960" -Width 960 -Height 540 -ShowResult))
}

$index = [ordered]@{
    phase = "P12.2"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    examLevels = $examLevels
    runCount = $runs.Count
    passedRuns = @($runs | Where-Object pass).Count
    pass = @($runs | Where-Object { -not $_.pass }).Count -eq 0
    runs = $runs
}
$indexPath = Join-Path $OutputDirectory "p122_exam_matrix_index.json"
$index | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $indexPath -Encoding UTF8
$index | ConvertTo-Json -Depth 12
if (-not $index.pass) {
    throw "P12.2 exam matrix failed. See $indexPath"
}
