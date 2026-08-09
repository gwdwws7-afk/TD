param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputRoot = "E:/TD/output/playtest/p131_l07_l15_matrix",
    [float]$TimeScale = 16,
    [int]$RandomSeed = 1337,
    [int]$MaxRealSeconds = 120,
    [switch]$RefreshScripts,
    [switch]$ResumeExisting,
    [switch]$RerunEntranceVariant,
    [switch]$RerunLevel15,
    [switch]$RerunL15Adaptive,
    [switch]$RerunL15Focused
)

$ErrorActionPreference = "Stop"
$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
$runRoot = Join-Path $OutputRoot "runs"
$reportPath = Join-Path $OutputRoot "p131_l07_l15_matrix.json"
$markdownPath = Join-Path $OutputRoot "p131_l07_l15_matrix.md"
New-Item -ItemType Directory -Force -Path $OutputRoot, $runRoot | Out-Null

$levels = @(7, 15)
$strategies = @("focused_fire", "control_lattice", "adaptive_network")
$sites = @(0, 1, 2)
$rows = New-Object System.Collections.Generic.List[object]
$refreshPending = $RefreshScripts.IsPresent

foreach ($level in $levels) {
    foreach ($strategy in $strategies) {
        foreach ($site in $sites) {
            $key = "l{0:d2}_{1}_site{2}" -f $level, $strategy, $site
            $summaryPath = Join-Path $runRoot "$key`_summary.json"
            $runPath = Join-Path $runRoot "$key`_run.json"
            $screenshotPath = Join-Path $runRoot "$key.png"
            $reuse = $false
            if ($ResumeExisting -and (Test-Path -LiteralPath $summaryPath) -and
                (Test-Path -LiteralPath $runPath)) {
                try {
                    $existing = Get-Content -LiteralPath $runPath -Raw -Encoding UTF8 | ConvertFrom-Json
                    $reuse = [bool]$existing.completed -and
                             -not [bool]$existing.stalled -and
                             [string]$existing.difficultyId -eq "standard"
                } catch {
                    $reuse = $false
                }
            }
            if ($RerunEntranceVariant -and $site -eq 1) {
                $reuse = $false
            }
            if ($RerunLevel15 -and $level -eq 15) {
                $reuse = $false
            }
            if ($RerunL15Adaptive -and $level -eq 15 -and $strategy -eq "adaptive_network") {
                $reuse = $false
            }
            if ($RerunL15Focused -and $level -eq 15 -and $strategy -eq "focused_fire") {
                $reuse = $false
            }

            if (-not $reuse) {
                Remove-Item -LiteralPath $summaryPath, $runPath, $screenshotPath `
                    -Force -ErrorAction SilentlyContinue
                $arguments = @{
                    McpUrl = $McpUrl
                    LevelIndex = $level
                    DurationSeconds = $MaxRealSeconds + 5
                    TimeScale = $TimeScale
                    RandomSeed = $RandomSeed
                    ViewportWidth = 960
                    ViewportHeight = 540
                    FormationDifficulty = "Standard"
                    PrepareP85Difficulty = $true
                    P124AutoplayStrategy = $strategy
                    P124SiteVariant = $site
                    P124MaxRealSeconds = $MaxRealSeconds
                    P124RunReportPath = $runPath.Replace("\", "/")
                    RunP124Audit = $true
                    PreserveCampaignProgress = $true
                    ScreenshotPath = $screenshotPath.Replace("\", "/")
                    SummaryPath = $summaryPath
                }
                if ($refreshPending) {
                    $arguments.RefreshScripts = $true
                    $refreshPending = $false
                }

                $completed = $false
                for ($attempt = 1; $attempt -le 3; $attempt++) {
                    try {
                        & $runner @arguments | Out-Null
                    } catch {
                        # A completed losing run intentionally fails the per-run P13.1 victory gate.
                    }

                    if ((Test-Path -LiteralPath $summaryPath) -and
                        (Test-Path -LiteralPath $runPath)) {
                        try {
                            $probe = Get-Content -LiteralPath $runPath -Raw -Encoding UTF8 | ConvertFrom-Json
                            $completed = [bool]$probe.completed -and -not [bool]$probe.stalled
                        } catch {
                            $completed = $false
                        }
                    }
                    if ($completed) { break }
                    if ($attempt -lt 3) {
                        Start-Sleep -Seconds 3
                        $arguments.Remove("RefreshScripts")
                    }
                }
                if (-not $completed) {
                    throw "P13.1 run did not complete after three attempts: $key"
                }
            }

            $run = Get-Content -LiteralPath $runPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $summary = Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $stateText = [string]$summary.state.data.result
            $rows.Add([pscustomobject]@{
                key = $key
                level = [int]$run.levelIndex
                strategy = [string]$run.strategyId
                site = [int]$run.siteVariant
                victory = [bool]$run.victory
                waves = [int]$run.wavesCleared
                firstLeak = [int]$run.firstLeakWave
                openingEscapes = [int]$run.openingEscapes
                escapes = [int]$run.escapes
                integrity = [int]$run.integrityRemaining
                towers = [int]$run.towersBuilt
                upgrades = [int]$run.upgradesPurchased
                topSiteShare = [double]$run.topSiteDamageSharePct
                firstWaveAudit = $stateText.Contains("p13.1.audit.firstWave=True")
                cliffAudit = $stateText.Contains("p13.1.audit.cliffPacing=True")
                sitePolicyAudit = $stateText.Contains("p13.1.audit.sitePolicy=True")
                p124Audit = [bool]$summary.checks.p124Audit
                p130Audit = [bool]$summary.checks.p130Audit
                p131Audit = [bool]$summary.checks.p131Audit
                uiBounds = [bool]$summary.checks.uiBounds
                uiOverlap = [bool]$summary.checks.uiOverlap
                uiTextFit = [bool]$summary.checks.uiTextFit
                consoleClean = [bool]$summary.checks.consoleClean
                summary = [IO.Path]::GetFullPath($summaryPath)
                runReport = [IO.Path]::GetFullPath($runPath)
                screenshot = [IO.Path]::GetFullPath($screenshotPath)
            })
        }
    }
}

$levelSummaries = foreach ($level in $levels) {
    $levelRows = @($rows | Where-Object level -eq $level)
    $strategyResults = foreach ($strategy in $strategies) {
        $matches = @($levelRows | Where-Object strategy -eq $strategy)
        [pscustomobject]@{
            strategy = $strategy
            victories = @($matches | Where-Object victory).Count
            runs = $matches.Count
            pass = @($matches | Where-Object victory).Count -ge 2
        }
    }
    $siteResults = foreach ($site in $sites) {
        $matches = @($levelRows | Where-Object site -eq $site)
        [pscustomobject]@{
            site = $site
            victories = @($matches | Where-Object victory).Count
            runs = $matches.Count
            winRate = [Math]::Round(100 * @($matches | Where-Object victory).Count / $matches.Count, 1)
            pass = @($matches | Where-Object victory).Count -ge 2
        }
    }
    $siteRates = @($siteResults | ForEach-Object { [double]$_.winRate })
    $siteSpread = [Math]::Round(
        ($siteRates | Measure-Object -Maximum).Maximum -
        ($siteRates | Measure-Object -Minimum).Minimum,
        1)
    $victories = @($levelRows | Where-Object victory).Count
    [pscustomobject]@{
        level = $level
        victories = $victories
        runs = $levelRows.Count
        strategyResults = $strategyResults
        siteResults = $siteResults
        siteWinRateSpread = $siteSpread
        pass = $victories -ge 7 -and
               @($strategyResults | Where-Object { -not $_.pass }).Count -eq 0 -and
               @($siteResults | Where-Object { -not $_.pass }).Count -eq 0 -and
               $siteSpread -le 33.4
    }
}

$surfacePass = @($rows | Where-Object {
    -not $_.uiBounds -or -not $_.uiOverlap -or -not $_.uiTextFit -or -not $_.consoleClean
}).Count -eq 0
$staticAuditPass = @($rows | Where-Object {
    -not $_.firstWaveAudit -or -not $_.cliffAudit -or -not $_.sitePolicyAudit -or
    -not $_.p124Audit -or -not $_.p130Audit
}).Count -eq 0
$openingPass = @($rows | Where-Object openingEscapes -gt 4).Count -eq 0
$contributionPass = @($rows | Where-Object topSiteShare -gt 32).Count -eq 0
$matrixPass = @($levelSummaries | Where-Object { -not $_.pass }).Count -eq 0
$pass = $rows.Count -eq 18 -and $surfacePass -and $staticAuditPass -and
        $openingPass -and $contributionPass -and $matrixPass

$report = [ordered]@{
    schemaVersion = "p131-l07-l15-matrix-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    randomSeed = $RandomSeed
    timeScale = $TimeScale
    acceptance = [ordered]@{
        minimumLevelVictories = 7
        minimumStrategyVictories = 2
        minimumSiteVictories = 2
        maximumSiteWinRateSpread = 33.4
        maximumOpeningEscapes = 4
        maximumTopSiteDamageShare = 32
    }
    levelSummaries = $levelSummaries
    runs = $rows
    checks = [ordered]@{
        surface = $surfacePass
        staticAudits = $staticAuditPass
        openingPressure = $openingPass
        siteContribution = $contributionPass
        matrix = $matrixPass
        pass = $pass
    }
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

$markdown = New-Object Text.StringBuilder
[void]$markdown.AppendLine("# P13.1 L07 / L15 Runtime Matrix")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("| Level | Wins | Site spread | Result |")
[void]$markdown.AppendLine("|---|---:|---:|---|")
foreach ($levelSummary in $levelSummaries) {
    [void]$markdown.AppendLine(
        "| L$('{0:d2}' -f $levelSummary.level) | $($levelSummary.victories)/$($levelSummary.runs) | " +
        "$($levelSummary.siteWinRateSpread)% | $(if ($levelSummary.pass) { 'PASS' } else { 'FAIL' }) |")
}
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("| Level | Strategy | Site | Result | Waves | Opening escapes | Escapes | Integrity | Top site |")
[void]$markdown.AppendLine("|---|---|---:|---|---:|---:|---:|---:|---:|")
foreach ($row in $rows) {
    [void]$markdown.AppendLine(
        "| L$('{0:d2}' -f $row.level) | $($row.strategy) | $($row.site) | " +
        "$(if ($row.victory) { 'WIN' } else { 'LOSS' }) | $($row.waves)/20 | " +
        "$($row.openingEscapes) | $($row.escapes) | $($row.integrity) | $($row.topSiteShare)% |")
}
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("Overall: $(if ($pass) { 'PASS' } else { 'FAIL' })")
$markdown.ToString() | Set-Content -LiteralPath $markdownPath -Encoding UTF8

Write-Output ($report | ConvertTo-Json -Depth 8)
if (-not $pass) {
    throw "P13.1 matrix acceptance failed. See $reportPath"
}
