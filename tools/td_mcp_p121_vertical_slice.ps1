param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputDirectory = "E:/TD/output/playtest/p121_vertical_slice",
    [switch]$RefreshScripts
)

$ErrorActionPreference = "Stop"
$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function Invoke-P121Pass {
    param(
        [string]$Name,
        [int]$Width,
        [int]$Height,
        [switch]$ShowResult,
        [switch]$Refresh
    )

    $arguments = @{
        McpUrl = $McpUrl
        LevelIndex = 9
        DurationSeconds = 0
        TimeScale = 1.0
        ViewportWidth = $Width
        ViewportHeight = $Height
        ScreenshotPath = "$OutputDirectory/$Name.png"
        SummaryPath = "$OutputDirectory/$Name.json"
        SkipAutoBuild = $true
        SkipStartWave = $true
        PrepareP121Presentation = $true
    }
    if ($ShowResult) {
        $arguments.ForceRunResult = $true
        $arguments.RunP121Audit = $true
    } else {
        $arguments.RunP113Audit = $true
    }

    if ($Refresh) {
        $arguments.RefreshScripts = $true
    }

    & $runner @arguments | Out-Null
    return Get-Content -LiteralPath $arguments.SummaryPath -Raw | ConvertFrom-Json
}

$combat = Invoke-P121Pass -Name "p121_l09_combat_1280" -Width 1280 -Height 720 -Refresh:$RefreshScripts
$result1280 = Invoke-P121Pass -Name "p121_l09_result_1280" -Width 1280 -Height 720 -ShowResult
$result960 = Invoke-P121Pass -Name "p121_l09_result_960" -Width 960 -Height 540 -ShowResult

$index = [ordered]@{
    phase = "P12.1"
    levelIndex = 9
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    pass = [bool]$combat.checks.p113Audit -and
           [bool]$result1280.checks.p121Audit -and
           [bool]$result960.checks.p121Audit
    artifacts = @(
        "$OutputDirectory/p121_l09_combat_1280.png",
        "$OutputDirectory/p121_l09_result_1280.png",
        "$OutputDirectory/p121_l09_result_960.png"
    )
    checks = [ordered]@{
        combatP113 = [bool]$combat.checks.p113Audit
        result1280 = [bool]$result1280.checks.p121Audit
        result960 = [bool]$result960.checks.p121Audit
    }
}

$indexPath = Join-Path $OutputDirectory "p121_vertical_slice_index.json"
$index | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $indexPath -Encoding UTF8
$index | ConvertTo-Json -Depth 12
if (-not $index.pass) {
    throw "P12.1 vertical slice audit failed. See $indexPath"
}
