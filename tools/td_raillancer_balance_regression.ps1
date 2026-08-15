# RailLancer single-tower balance regression suite (25 real-runtime autoplay runs).
#
# Verifies that a RailLancer-only comp can no longer clear levels after the
# balance pass. Each run drives the game through td_mcp_playtest.ps1 with the
# P12.4 autoplay brain and records its own JSON report, so:
#   - a killed suite loses nothing (completed runs are skipped on relaunch)
#   - per-run data survives regardless of the script's strict audit exit code
#     (exit=1 is expected whenever autoplay does not finish all 20 waves)
#
# Usage:
#   powershell -File tools\td_raillancer_balance_regression.ps1            # run/resume
#   powershell -File tools\td_raillancer_balance_regression.ps1 -Force     # rerun all
#   powershell -File tools\td_raillancer_balance_regression.ps1 -MaxRuns 1 # probe

param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputDir = "E:/TD/output/playtest/balance_regression",
    [int]$TimeScale = 16,
    [int]$P124MaxRealSeconds = 150,
    [int]$DurationSeconds = 170,
    [int]$UnityReadyTimeoutSeconds = 90,
    [int]$MaxRuns = 25,
    [switch]$Force
)

$ErrorActionPreference = "Continue"
$playtestScript = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
if (-not (Test-Path -LiteralPath $playtestScript)) {
    throw "td_mcp_playtest.ps1 not found next to this script: $playtestScript"
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$progressLog = Join-Path $OutputDir "progress.log"
$statusPath = Join-Path $OutputDir "status.json"
$reportPath = Join-Path $OutputDir "balance_regression_report.md"
$csvPath = Join-Path $OutputDir "balance_regression_results.csv"
$stdoutDir = Join-Path $OutputDir "run_stdout"
New-Item -ItemType Directory -Path $stdoutDir -Force | Out-Null

function Write-ProgressLine {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    $line | Add-Content -Path $progressLog -Encoding UTF8
    Write-Host $line
}

function Write-StatusJson {
    param([hashtable]$Status)
    $Status.generatedUtc = [DateTime]::UtcNow.ToString("o")
    $Status | ConvertTo-Json -Depth 8 | Set-Content -Path $statusPath -Encoding UTF8
}

# 5 representative levels x 5 variants = 25 runs.
# focused_fire doctrine builds RailLancer-heavy; control_lattice and
# adaptive_network mix in control towers. Site variants rotate the build plan.
$levels = @(
    @{ Index = 1;  Tag = "L01_tutorial" },
    @{ Index = 5;  Tag = "L05_first_boss" },
    @{ Index = 9;  Tag = "L09_midgame" },
    @{ Index = 13; Tag = "L13_pressure" },
    @{ Index = 20; Tag = "L20_finale" }
)
$variants = @(
    @{ Id = "adaptive_network"; Site = 0 },
    @{ Id = "focused_fire";     Site = 0 },
    @{ Id = "control_lattice";  Site = 0 },
    @{ Id = "adaptive_network"; Site = 1 },
    @{ Id = "focused_fire";     Site = 1 }
)

$runs = New-Object System.Collections.Generic.List[object]
$runNumber = 0
foreach ($level in $levels) {
    foreach ($variant in $variants) {
        $runNumber++
        if ($runNumber -gt 25) { break }
        $runs.Add([pscustomobject]@{
            Number = $runNumber
            LevelIndex = $level.Index
            LevelTag = $level.Tag
            Strategy = $variant.Id
            SiteVariant = $variant.Site
            Name = ("{0}_{1}_s{2}" -f $level.Tag, $variant.Id, $variant.Site)
        })
    }
    if ($runNumber -gt 25) { break }
}
$runs = @($runs | Select-Object -First $MaxRuns)

Write-ProgressLine "RailLancer balance regression starting: $($runs.Count) runs, timeScale=$TimeScale, budget=${P124MaxRealSeconds}s"
Write-StatusJson @{ state = "running"; totalRuns = $runs.Count; completedRuns = 0 }

$suiteStarted = Get-Date
$results = New-Object System.Collections.Generic.List[object]
$completedCount = 0
$failureCount = 0

foreach ($run in $runs) {
    $p124Path = Join-Path $OutputDir ("run_{0:D2}_{1}.p124.json" -f $run.Number, $run.Name)
    $summaryPath = Join-Path $OutputDir ("run_{0:D2}_{1}.summary.json" -f $run.Number, $run.Name)
    $screenshotPath = Join-Path $OutputDir ("run_{0:D2}_{1}.png" -f $run.Number, $run.Name)
    $runStdout = Join-Path $stdoutDir ("run_{0:D2}_{1}.stdout.log" -f $run.Number, $run.Name)

    if (-not $Force -and (Test-Path -LiteralPath $p124Path)) {
        Write-ProgressLine ("[{0:D2}/{1}] {2} ... SKIP (report exists)" -f $run.Number, $runs.Count, $run.Name)
        $existing = Get-Content -LiteralPath $p124Path -Raw -Encoding UTF8 | ConvertFrom-Json
        $results.Add([pscustomobject]@{
            Run = $run.Number; Name = $run.Name; LevelIndex = $run.LevelIndex
            Strategy = $run.Strategy; SiteVariant = $run.SiteVariant
            ExitCode = 0; Skipped = $true
            Completed = [bool]$existing.completed; Stalled = [bool]$existing.stalled
            Victory = [bool]$existing.victory
            WavesCleared = [int]$existing.wavesCleared; WaveCount = [int]$existing.waveCount
            TowersBuilt = [int]$existing.towersBuilt
            AvailableKinds = [int]$existing.availableTowerKinds
            TowerKindsUsed = [int]$existing.towerKindsUsed
            TopTowerKind = [string]$existing.topTowerKind
            TopSharePct = [double]$existing.topTowerKindContributionSharePct
            FirstLeakWave = if ($null -ne $existing.firstLeakWave) { [int]$existing.firstLeakWave } else { -1 }
        })
        $completedCount++
        continue
    }

    $runStarted = Get-Date
    Write-ProgressLine ("[{0:D2}/{1}] {2} ... running (level={3} strategy={4} site={5})" -f `
        $run.Number, $runs.Count, $run.Name, $run.LevelIndex, $run.Strategy, $run.SiteVariant)

    & powershell -NoProfile -ExecutionPolicy Bypass -File $playtestScript `
        -McpUrl $McpUrl `
        -LevelIndex $run.LevelIndex `
        -DurationSeconds $DurationSeconds `
        -TimeScale $TimeScale `
        -RandomSeed (1337 + $run.Number) `
        -UnityReadyTimeoutSeconds $UnityReadyTimeoutSeconds `
        -P124AutoplayStrategy $run.Strategy `
        -P124SiteVariant $run.SiteVariant `
        -P124MaxRealSeconds $P124MaxRealSeconds `
        -P124RunReportPath ($p124Path -replace "/", "\") `
        -SummaryPath ($summaryPath -replace "/", "\") `
        -ScreenshotPath ($screenshotPath -replace "/", "\") `
        -AllowConsoleIssues `
        > $runStdout 2>&1
    $exitCode = $LASTEXITCODE

    if (-not (Test-Path -LiteralPath $p124Path)) {
        $failureCount++
        Write-ProgressLine ("[{0:D2}/{1}] {2} ... FAILED exit={3} (no P124 report written)" -f `
            $run.Number, $runs.Count, $run.Name, $exitCode)
        $results.Add([pscustomobject]@{
            Run = $run.Number; Name = $run.Name; LevelIndex = $run.LevelIndex
            Strategy = $run.Strategy; SiteVariant = $run.SiteVariant
            ExitCode = $exitCode; Skipped = $false
            Completed = $false; Stalled = $false; Victory = $false
            WavesCleared = 0; WaveCount = 0; TowersBuilt = 0
            AvailableKinds = 0
            TowerKindsUsed = 0; TopTowerKind = "<no-report>"; TopSharePct = 0.0; FirstLeakWave = -1
        })
        Write-StatusJson @{ state = "running"; totalRuns = $runs.Count; completedRuns = $completedCount; failedRuns = $failureCount }
        continue
    }

    $report = Get-Content -LiteralPath $p124Path -Raw -Encoding UTF8 | ConvertFrom-Json
    $elapsed = [int]((Get-Date) - $runStarted).TotalSeconds
    # exit=1 is expected when autoplay loses or hits the time budget before
    # wave 20; the audit gates are stricter than this suite's pass criteria.
    Write-ProgressLine ("[{0:D2}/{1}] {2} ... exit={3} victory={4} waves={5}/{6} towers={7} kinds={8} top={9}({10:P0}) [{11}s]" -f `
        $run.Number, $runs.Count, $run.Name, $exitCode, $report.victory, $report.wavesCleared,
        $report.waveCount, $report.towersBuilt, $report.towerKindsUsed, $report.topTowerKind,
        ([double]$report.topTowerKindContributionSharePct / 100.0), $elapsed)

    $results.Add([pscustomobject]@{
        Run = $run.Number; Name = $run.Name; LevelIndex = $run.LevelIndex
        Strategy = $run.Strategy; SiteVariant = $run.SiteVariant
        ExitCode = $exitCode; Skipped = $false
        Completed = [bool]$report.completed; Stalled = [bool]$report.stalled
        Victory = [bool]$report.victory
        WavesCleared = [int]$report.wavesCleared; WaveCount = [int]$report.waveCount
        TowersBuilt = [int]$report.towersBuilt
        AvailableKinds = [int]$report.availableTowerKinds
        TowerKindsUsed = [int]$report.towerKindsUsed
        TopTowerKind = [string]$report.topTowerKind
        TopSharePct = [double]$report.topTowerKindContributionSharePct
        FirstLeakWave = if ($null -ne $report.firstLeakWave) { [int]$report.firstLeakWave } else { -1 }
    })
    $completedCount++
    Write-StatusJson @{ state = "running"; totalRuns = $runs.Count; completedRuns = $completedCount; failedRuns = $failureCount }
}

# ---- Aggregate ----
$results | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8

# A single-kind clear only counts as a regression when the autoplay brain had
# other towers available and chose RailLancer alone anyway (L01's tutorial
# progression intentionally unlocks only RailLancer, so a forced single-kind
# clear there is by design, not a balance failure).
$singleKindRailLancerClears = @($results | Where-Object {
    $_.TowerKindsUsed -eq 1 -and $_.TopTowerKind -eq "RailLancer" -and $_.Victory -and
    $_.AvailableKinds -gt 1
})
$forcedSingleKindClears = @($results | Where-Object {
    $_.TowerKindsUsed -eq 1 -and $_.Victory -and $_.AvailableKinds -le 1
})
$victories = @($results | Where-Object { $_.Victory })
$defeats = @($results | Where-Object { -not $_.Victory -and -not $_.Skipped })
$stalledRuns = @($results | Where-Object { $_.Stalled })
$railLancerDominant = @($results | Where-Object { $_.TopTowerKind -eq "RailLancer" -and $_.TopSharePct -ge 80.0 })
$noReportRuns = @($results | Where-Object { $_.TopTowerKind -eq "<no-report>" })

$suitePass = ($singleKindRailLancerClears.Count -eq 0) -and ($noReportRuns.Count -eq 0)

$reportLines = New-Object System.Collections.Generic.List[string]
$reportLines.Add("# RailLancer 单塔平衡回归报告")
$reportLines.Add("")
$reportLines.Add("- 生成时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$reportLines.Add("- 总局数: $($results.Count)(关卡 1/5/9/13/20 × 策略 adaptive_network/focused_fire/control_lattice × 站位变体)")
$reportLines.Add("- 速度 ${TimeScale}x,每局真实时间预算 ${P124MaxRealSeconds}s")
$reportLines.Add("")
$reportLines.Add("## 判定")
$reportLines.Add("")
$reportLines.Add("回归目标:在有其他塔可选时,RailLancer 单一塔种不得通关。")
$reportLines.Add("")
$reportLines.Add("- 可选塔 >1 时仍单塔种(RailLancer only)通关: **$($singleKindRailLancerClears.Count)** $(
    if ($singleKindRailLancerClears.Count -eq 0) { '✅ 无' } else { '❌ ' + (($singleKindRailLancerClears | ForEach-Object { $_.Name }) -join ', ') })")
$reportLines.Add("- 教程关仅解锁 1 塔被迫单塔通关(设计内,不计回归): $($forcedSingleKindClears.Count)")
$reportLines.Add("- 无报告局数(管线失败): **$($noReportRuns.Count)**")
$reportLines.Add("- 胜利局: $($victories.Count) / 失败局: $($defeats.Count) / 超时截断局: $($stalledRuns.Count)")
$reportLines.Add("- RailLancer 贡献占比 ≥80% 的局: $($railLancerDominant.Count)")
$reportLines.Add("")
$reportLines.Add("## 结论: $(if ($suitePass) { '✅ PASS — RailLancer 单塔无法通关,平衡修复保持' } else { '❌ FAIL — 见上方明细' })")
$reportLines.Add("")
$reportLines.Add("## 每局明细")
$reportLines.Add("")
$reportLines.Add("| # | 局名 | 胜利 | 波次 | 塔 | 可选/使用塔种 | 主力塔 | 占比 | 首漏波 | exit |")
$reportLines.Add("|---|------|------|------|----|--------------|--------|------|--------|------|")
foreach ($row in $results) {
    $reportLines.Add(("| {0} | {1} | {2} | {3}/{4} | {5} | {6}/{7} | {8} | {9:P0} | {10} | {11} |" -f `
        $row.Run, $row.Name, $(if ($row.Victory) { "✅" } else { "❌" }),
        $row.WavesCleared, $row.WaveCount, $row.TowersBuilt, $row.AvailableKinds, $row.TowerKindsUsed,
        $row.TopTowerKind, ($row.TopSharePct / 100.0), $row.FirstLeakWave, $row.ExitCode))
}
$reportLines | Set-Content -Path $reportPath -Encoding UTF8

$totalElapsed = [int]((Get-Date) - $suiteStarted).TotalSeconds
Write-StatusJson @{
    state = $(if ($suitePass) { "passed" } else { "failed" })
    totalRuns = $results.Count
    completedRuns = $completedCount
    failedRuns = $failureCount
    victories = $victories.Count
    singleKindRailLancerClears = $singleKindRailLancerClears.Count
    elapsedSeconds = $totalElapsed
    reportPath = $reportPath
}
Write-ProgressLine "Suite finished in ${totalElapsed}s -> $reportPath (pass=$suitePass)"
exit $(if ($suitePass) { 0 } else { 1 })
