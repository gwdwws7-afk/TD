param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputRoot = "E:/TD/output/playtest/p124_full_campaign_calibration",
    [ValidateSet("Pilot", "Release")]
    [string]$Scope = "Release",
    [float]$TimeScale = 16,
    [int]$MaxRealSeconds = 90,
    [switch]$RefreshScripts,
    [switch]$SkipFastMatrix,
    [switch]$ResumeExisting,
    [string[]]$RerunKeys = @()
)

$ErrorActionPreference = "Stop"
$startedUtc = [DateTime]::UtcNow
$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
$fastRunner = Join-Path $PSScriptRoot "td_mcp_p102_balance_matrix.ps1"
$realRoot = Join-Path $OutputRoot "real_runs"
$fastRoot = Join-Path $OutputRoot "fast_matrix"
New-Item -ItemType Directory -Force -Path $OutputRoot, $realRoot, $fastRoot | Out-Null

function Get-Median {
    param([double[]]$Values)
    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 0) { return 0 }
    $middle = [Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) { return [Math]::Round($sorted[$middle], 2) }
    return [Math]::Round(($sorted[$middle - 1] + $sorted[$middle]) / 2, 2)
}

function Get-Average {
    param([double[]]$Values)
    if ($Values.Count -eq 0) { return 0 }
    return [Math]::Round((($Values | Measure-Object -Average).Average), 2)
}

function Add-RunSpec {
    param(
        [System.Collections.Generic.List[object]]$List,
        [int]$Level,
        [string]$Difficulty,
        [string]$Strategy,
        [int]$SiteVariant,
        [string]$Purpose
    )
    $key = "L{0:d2}_{1}_{2}_site{3}" -f $Level, $Difficulty.ToLowerInvariant(), $Strategy, $SiteVariant
    if (@($List | Where-Object key -eq $key).Count -eq 0) {
        $List.Add([pscustomobject]@{
            key = $key
            level = $Level
            difficulty = $Difficulty
            difficultyId = if ($Difficulty -eq "EmberTrial") { "ember_trial" } else { $Difficulty.ToLowerInvariant() }
            strategy = $Strategy
            siteVariant = $SiteVariant
            purpose = $Purpose
        })
    }
}

$anchors = @(1, 5, 9, 13, 17, 20)
$strategies = @("focused_fire", "control_lattice", "adaptive_network")
$specs = New-Object System.Collections.Generic.List[object]
if ($Scope -eq "Pilot") {
    foreach ($level in $anchors) {
        Add-RunSpec $specs $level "Standard" "adaptive_network" 0 "anchor"
    }
} else {
    foreach ($level in $anchors) {
        foreach ($strategy in $strategies) {
            Add-RunSpec $specs $level "Standard" $strategy 0 "strategy_anchor"
        }
        Add-RunSpec $specs $level "Veteran" "adaptive_network" 0 "difficulty_anchor"
        Add-RunSpec $specs $level "EmberTrial" "adaptive_network" 0 "difficulty_anchor"
    }
    foreach ($strategy in $strategies) {
        Add-RunSpec $specs 14 "EmberTrial" $strategy 0 "cliff_probe"
    }
    foreach ($level in @(9, 20)) {
        Add-RunSpec $specs $level "Standard" "adaptive_network" 1 "site_ab"
        Add-RunSpec $specs $level "Standard" "adaptive_network" 2 "site_ab"
    }
}

$fastMatrixPath = Join-Path $fastRoot "p102_balance_matrix.json"
if (-not $SkipFastMatrix -or -not (Test-Path -LiteralPath $fastMatrixPath)) {
    $fastArgs = @{ McpUrl = $McpUrl; OutputDirectory = $fastRoot }
    if ($RefreshScripts) { $fastArgs.RefreshScripts = $true }
    $fastCompleted = $false
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            & $fastRunner @fastArgs | Out-Null
            $fastCompleted = $true
            break
        } catch {
            if ($attempt -ge 3) { throw }
            Start-Sleep -Seconds 2
            $fastArgs.Remove("RefreshScripts")
        }
    }
    if (-not $fastCompleted) { throw "P10.2 prerequisite matrix did not complete." }
    $refreshPending = $false
} else {
    $refreshPending = $RefreshScripts.IsPresent
}
$fastMatrix = Get-Content -LiteralPath $fastMatrixPath -Raw | ConvertFrom-Json

$rows = New-Object System.Collections.Generic.List[object]
foreach ($spec in $specs) {
    $stem = $spec.key.ToLowerInvariant()
    $summaryPath = Join-Path $realRoot "$stem`_summary.json"
    $runPath = Join-Path $realRoot "$stem`_run.json"
    $screenshotPath = Join-Path $realRoot "$stem.png"
    $reuseExisting = $false
    $forceRerun = $RerunKeys -contains $spec.key
    if ($ResumeExisting -and -not $forceRerun -and
        (Test-Path -LiteralPath $summaryPath) -and (Test-Path -LiteralPath $runPath)) {
        try {
            $existingRun = Get-Content -LiteralPath $runPath -Raw | ConvertFrom-Json
            $reuseExisting = [bool]$existingRun.completed -and -not [bool]$existingRun.stalled
        } catch {
            $reuseExisting = $false
        }
    }
    if (-not $reuseExisting) {
        Remove-Item -LiteralPath $summaryPath, $runPath -Force -ErrorAction SilentlyContinue
    }
    $arguments = @{
        McpUrl = $McpUrl
        LevelIndex = $spec.level
        DurationSeconds = $MaxRealSeconds + 5
        TimeScale = $TimeScale
        ViewportWidth = 960
        ViewportHeight = 540
        FormationDifficulty = $spec.difficulty
        P124AutoplayStrategy = $spec.strategy
        P124SiteVariant = $spec.siteVariant
        P124MaxRealSeconds = $MaxRealSeconds
        P124RunReportPath = $runPath.Replace("\", "/")
        RunP124Audit = $true
        RunP125EconomyAudit = $true
        PreserveCampaignProgress = $true
        ScreenshotPath = $screenshotPath.Replace("\", "/")
        SummaryPath = $summaryPath
    }
    if ($spec.difficulty -ne "Standard") { $arguments.PrepareP85Difficulty = $true }

    $runnerPassed = $reuseExisting
    $attempts = 0
    if (-not $reuseExisting) {
        if ($refreshPending) {
            $arguments.RefreshScripts = $true
            $refreshPending = $false
        }
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            $attempts = $attempt
            try {
                & $runner @arguments | Out-Null
                $runnerPassed = $true
                break
            } catch {
                $validCompletedRun = $false
                if ((Test-Path -LiteralPath $runPath) -and (Test-Path -LiteralPath $summaryPath)) {
                    try {
                        $probe = Get-Content -LiteralPath $runPath -Raw | ConvertFrom-Json
                        $validCompletedRun = [bool]$probe.completed -and -not [bool]$probe.stalled
                    } catch {
                        $validCompletedRun = $false
                    }
                }
                if ($validCompletedRun) { break }
                if ($attempt -ge 3) { throw }
                Start-Sleep -Seconds 2
            }
        }
    }

    $real = Get-Content -LiteralPath $runPath -Raw | ConvertFrom-Json
    $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    $topSiteTowerKind = "none"
    $topSiteParts = @([string]$real.topSite -split ",")
    if ($topSiteParts.Count -eq 2) {
        $topSiteTower = @($real.towers | Where-Object {
            [int]$_.cellX -eq [int]$topSiteParts[0] -and [int]$_.cellY -eq [int]$topSiteParts[1]
        } | Select-Object -First 1)
        if ($topSiteTower.Count -gt 0) { $topSiteTowerKind = [string]$topSiteTower[0].towerKind }
    }
    $fast = @($fastMatrix.runs | Where-Object {
        [int]$_.levelIndex -eq [int]$spec.level -and
        $_.difficultyId -eq $spec.difficultyId -and
        $_.strategyId -eq $spec.strategy
    })[0]
    $scoreError = [Math]::Round([double]$real.totalScore - [double]$fast.totalScore, 2)
    $durationErrorPct = if ([double]$real.simulationDurationSeconds -le 0) { 0 } else {
        [Math]::Round(100 * ([double]$fast.durationSeconds - [double]$real.simulationDurationSeconds) / [double]$real.simulationDurationSeconds, 2)
    }
    $rows.Add([pscustomobject]@{
        key = $spec.key
        purpose = $spec.purpose
        level = [int]$real.levelIndex
        map = $real.mapId
        difficulty = $real.difficultyId
        strategy = $real.strategyId
        siteVariant = [int]$real.siteVariant
        completed = [bool]$real.completed
        stalled = [bool]$real.stalled
        victory = [bool]$real.victory
        wave = [int]$real.currentWave
        wavesCleared = [int]$real.wavesCleared
        score = [int]$real.totalScore
        fastScore = [double]$fast.totalScore
        scoreError = $scoreError
        realDuration = [double]$real.realDurationSeconds
        simulationDuration = [double]$real.simulationDurationSeconds
        fastDuration = [double]$fast.durationSeconds
        durationErrorPct = $durationErrorPct
        firstLeak = [int]$real.firstLeakWave
        escapes = [int]$real.escapes
        integrity = [int]$real.integrityRemaining
        endingBudget = [int]$real.endingBudget
        towers = [int]$real.towersBuilt
        availableTowerKinds = [int]$real.availableTowerKinds
        towerPowerMultiplier = [double]$real.towerPowerMultiplier
        towerKinds = [int]$real.towerKindsUsed
        upgrades = [int]$real.upgradesPurchased
        scenarioConversion = [double]$real.scenarioConversionPct
        topKind = $real.topTowerKind
        topKindShare = [double]$real.topTowerKindDamageSharePct
        topSite = $real.topSite
        topSiteTowerKind = $topSiteTowerKind
        topSiteShare = [double]$real.topSiteDamageSharePct
        analyticsConsistent = [bool]$real.analyticsConsistent
        failureReasons = @($real.failureReasons) -join ";"
        recommendations = @($real.recommendations).Count
        runnerPassed = $runnerPassed
        attempts = $attempts
        summary = [IO.Path]::GetFullPath($summaryPath)
        runReport = [IO.Path]::GetFullPath($runPath)
        screenshot = [IO.Path]::GetFullPath($screenshotPath)
        uiBounds = [bool]$summary.checks.uiBounds
        uiOverlap = [bool]$summary.checks.uiOverlap
        uiTextFit = [bool]$summary.checks.uiTextFit
        consoleClean = [bool]$summary.checks.consoleClean
    })
}

$eligible = @($rows | Where-Object { $_.completed -and -not $_.stalled })
$standardCore = @($rows | Where-Object { $_.difficulty -eq "standard" -and $_.siteVariant -eq 0 })
$difficultySummary = foreach ($difficulty in @("standard", "veteran", "ember_trial")) {
    $matches = @($rows | Where-Object { $_.difficulty -eq $difficulty -and $_.siteVariant -eq 0 })
    [pscustomobject]@{
        difficulty = $difficulty
        runs = $matches.Count
        victories = @($matches | Where-Object victory).Count
        winRate = if ($matches.Count -eq 0) { 0 } else { [Math]::Round(100 * @($matches | Where-Object victory).Count / $matches.Count, 1) }
        medianScore = Get-Median @($matches | ForEach-Object { [double]$_.score })
        medianFirstLeak = Get-Median @($matches | ForEach-Object { [double]$_.firstLeak })
        medianDuration = Get-Median @($matches | ForEach-Object { [double]$_.simulationDuration })
    }
}

$dominanceEligible = @($eligible | Where-Object { $_.towers -ge 3 })
$mapSiteSummary = foreach ($map in @($dominanceEligible.map | Sort-Object -Unique)) {
    $matches = @($dominanceEligible | Where-Object map -eq $map)
    $topCellGroup = @($matches | Group-Object topSite | Sort-Object Count -Descending | Select-Object -First 1)[0]
    [pscustomobject]@{
        map = $map
        runs = $matches.Count
        distinctTopSites = @($matches.topSite | Sort-Object -Unique).Count
        mostFrequentTopSite = if ($null -eq $topCellGroup) { "none" } else { $topCellGroup.Name }
        topSiteFrequencyPct = if ($matches.Count -eq 0 -or $null -eq $topCellGroup) { 0 } else { [Math]::Round(100 * $topCellGroup.Count / $matches.Count, 1) }
        medianTopSiteSharePct = Get-Median @($matches | ForEach-Object { [double]$_.topSiteShare })
        maximumTopSiteSharePct = [Math]::Round((($matches.topSiteShare | Measure-Object -Maximum).Maximum), 1)
        maximumTopKindSharePct = [Math]::Round((($matches.topKindShare | Measure-Object -Maximum).Maximum), 1)
    }
}

$calibrationRows = @($eligible | Where-Object siteVariant -eq 0)
$durationCalibrationRows = @($calibrationRows | Where-Object victory | Where-Object {
    $row = $_
    @($fastMatrix.runs | Where-Object {
        [int]$_.levelIndex -eq [int]$row.level -and $_.difficultyId -eq $row.difficulty -and
        $_.strategyId -eq $row.strategy -and [bool]$_.victory
    }).Count -gt 0
})
$medianAbsoluteScoreError = Get-Median @($calibrationRows | ForEach-Object { [Math]::Abs([double]$_.scoreError) })
$medianAbsoluteDurationErrorPct = Get-Median @($durationCalibrationRows | ForEach-Object { [Math]::Abs([double]$_.durationErrorPct) })
$victoryAgreementPct = if ($calibrationRows.Count -eq 0) { 0 } else {
    $agreement = 0
    foreach ($row in $calibrationRows) {
        $fastRun = @($fastMatrix.runs | Where-Object {
            [int]$_.levelIndex -eq [int]$row.level -and $_.difficultyId -eq $row.difficulty -and $_.strategyId -eq $row.strategy
        })[0]
        if ([bool]$fastRun.victory -eq [bool]$row.victory) { $agreement++ }
    }
    [Math]::Round(100 * $agreement / $calibrationRows.Count, 1)
}

$siteVariantPass = $Scope -eq "Pilot"
if ($Scope -eq "Release") {
    $siteAbGroups = @($rows | Where-Object { $_.level -in @(9, 20) -and $_.difficulty -eq "standard" -and $_.strategy -eq "adaptive_network" } | Group-Object level)
    $siteVariantPass = $siteAbGroups.Count -eq 2
    foreach ($group in $siteAbGroups) {
        $distinctSites = @($group.Group.topSite | Sort-Object -Unique).Count
        $distinctLoadouts = @($group.Group | ForEach-Object { "$($_.topSite):$($_.topSiteTowerKind)" } | Sort-Object -Unique).Count
        if ($group.Count -lt 3 -or ($distinctSites -lt 2 -and $distinctLoadouts -lt 2)) { $siteVariantPass = $false }
    }
}
$completionPass = @($rows | Where-Object { -not $_.completed -or $_.stalled }).Count -eq 0
$telemetryPass = @($rows | Where-Object { -not $_.analyticsConsistent -or $_.recommendations -ne 3 }).Count -eq 0
$surfacePass = @($rows | Where-Object { -not $_.uiBounds -or -not $_.uiOverlap -or -not $_.uiTextFit -or -not $_.consoleClean }).Count -eq 0
$siteDominancePass = @($mapSiteSummary | Where-Object {
    $_.maximumTopSiteSharePct -gt 58 -or ($_.runs -ge 6 -and $_.topSiteFrequencyPct -gt 75)
}).Count -eq 0
$kindDominancePass = @($dominanceEligible | Where-Object { $_.availableTowerKinds -gt 1 -and $_.topKindShare -gt 78 }).Count -eq 0
$explainableFailurePass = @($rows | Where-Object { -not $_.victory -and ([string]::IsNullOrWhiteSpace($_.failureReasons) -or $_.recommendations -ne 3) }).Count -eq 0
$economySaturationRows = @($eligible | Where-Object {
    $_.towers -ge 12 -and $_.upgrades -ge 36 -and $_.endingBudget -gt 1000
})
$calibrationPass = $medianAbsoluteScoreError -le 8 -and $medianAbsoluteDurationErrorPct -le 20 -and $victoryAgreementPct -ge 80
$fastCurvePass = $fastMatrix.curveStatus -eq "PASS" -and @($fastMatrix.alarms).Count -eq 0
$hardPass = $completionPass -and $telemetryPass -and $surfacePass -and $siteDominancePass -and
            $kindDominancePass -and $siteVariantPass -and $explainableFailurePass -and $calibrationPass -and $fastCurvePass

$rows | Export-Csv -LiteralPath (Join-Path $OutputRoot "p124_real_runs.csv") -NoTypeInformation -Encoding UTF8
$difficultySummary | Export-Csv -LiteralPath (Join-Path $OutputRoot "p124_difficulty_summary.csv") -NoTypeInformation -Encoding UTF8
$mapSiteSummary | Export-Csv -LiteralPath (Join-Path $OutputRoot "p124_site_dominance.csv") -NoTypeInformation -Encoding UTF8

$index = [ordered]@{
    schemaVersion = "p124-full-campaign-calibration-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $startedUtc).TotalSeconds, 1)
    scope = $Scope
    runCount = $rows.Count
    completedRuns = $eligible.Count
    victories = @($rows | Where-Object victory).Count
    calibration = [ordered]@{
        medianAbsoluteScoreError = $medianAbsoluteScoreError
        medianAbsoluteDurationErrorPct = $medianAbsoluteDurationErrorPct
        victoryAgreementPct = $victoryAgreementPct
        pass = $calibrationPass
    }
    gates = [ordered]@{
        completion = $completionPass
        telemetry = $telemetryPass
        surfaces = $surfacePass
        siteDominance = $siteDominancePass
        kindDominance = $kindDominancePass
        siteVariants = $siteVariantPass
        explainableFailures = $explainableFailurePass
        fastCurve = $fastCurvePass
    }
    difficultySummary = $difficultySummary
    mapSiteSummary = $mapSiteSummary
    fastFingerprint = $fastMatrix.fingerprint
    fastCurveStatus = $fastMatrix.curveStatus
    fastAlarms = $fastMatrix.alarms
    softWarnings = @(
        if ($economySaturationRows.Count -gt 0) {
            "ECONOMY_SATURATION: $($economySaturationRows.Count) runs ended with 12 fully upgraded towers and more than 1000 unspent budget."
        }
    )
    hardPass = $hardPass
    runs = $rows
}
$indexPath = Join-Path $OutputRoot "p124_calibration_index.json"
$index | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $indexPath -Encoding UTF8

$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine("# P12.4 Full Campaign Calibration")
[void]$md.AppendLine("")
[void]$md.AppendLine("Generated: ``$($index.generatedUtc)``  ")
[void]$md.AppendLine("Mode: rendered runtime autoplay + ``$($fastMatrix.simulationMode)``  ")
[void]$md.AppendLine("Runs: $($index.completedRuns)/$($index.runCount)  ")
[void]$md.AppendLine("")
[void]$md.AppendLine("## Release Gate")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Check | Result |")
[void]$md.AppendLine("|---|---:|")
[void]$md.AppendLine("| Complete, non-stalled real runs | $completionPass |")
[void]$md.AppendLine("| Runtime analytics and three recommendations | $telemetryPass |")
[void]$md.AppendLine("| UI, text and Console | $surfacePass |")
[void]$md.AppendLine("| Build-site dominance | $siteDominancePass |")
[void]$md.AppendLine("| Tower-kind dominance | $kindDominancePass |")
[void]$md.AppendLine("| Site A/B changes decisions | $siteVariantPass |")
[void]$md.AppendLine("| Failures explainable | $explainableFailurePass |")
[void]$md.AppendLine("| Fast curve clean | $fastCurvePass |")
[void]$md.AppendLine("| **P12.4 hard pass** | **$hardPass** |")
[void]$md.AppendLine("")
[void]$md.AppendLine("## Simulator Calibration")
[void]$md.AppendLine("")
[void]$md.AppendLine("- Median absolute score error: **$medianAbsoluteScoreError** points (target <= 8).")
[void]$md.AppendLine("- Median absolute duration error: **$medianAbsoluteDurationErrorPct%** (target <= 20%).")
[void]$md.AppendLine("- Victory agreement: **$victoryAgreementPct%** (target >= 80%).")
[void]$md.AppendLine("- Duration calibration uses **$($durationCalibrationRows.Count)** complete real/fast victory pairs.")
[void]$md.AppendLine("")
[void]$md.AppendLine("## Difficulty")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Difficulty | Wins | Win rate | Median score | First leak | Duration |")
[void]$md.AppendLine("|---|---:|---:|---:|---:|---:|")
foreach ($row in $difficultySummary) {
    [void]$md.AppendLine("| $($row.difficulty) | $($row.victories)/$($row.runs) | $($row.winRate)% | $($row.medianScore) | W$($row.medianFirstLeak) | $($row.medianDuration)s |")
}
[void]$md.AppendLine("")
[void]$md.AppendLine("## Build Sites")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Map | Runs | Distinct top sites | Most frequent | Frequency | Median / max share | Max tower-kind share |")
[void]$md.AppendLine("|---|---:|---:|---|---:|---:|---:|")
foreach ($row in $mapSiteSummary) {
    [void]$md.AppendLine("| $($row.map) | $($row.runs) | $($row.distinctTopSites) | $($row.mostFrequentTopSite) | $($row.topSiteFrequencyPct)% | $($row.medianTopSiteSharePct)% / $($row.maximumTopSiteSharePct)% | $($row.maximumTopKindSharePct)% |")
}
[void]$md.AppendLine("")
[void]$md.AppendLine("## Soft Warnings")
[void]$md.AppendLine("")
if ($economySaturationRows.Count -gt 0) {
    [void]$md.AppendLine("- **ECONOMY_SATURATION**: $($economySaturationRows.Count) runs filled all 12 sites, bought all 36 upgrades, and still ended above 1000 budget. Preserve this as a P12.5 economy-sink task; it does not invalidate route, difficulty, or dominance calibration.")
} else {
    [void]$md.AppendLine("- None.")
}
[void]$md.AppendLine("")
[void]$md.AppendLine("Every rendered run uses the shipping wave coroutine, enemy movement, targeting, projectiles, damage, scenario devices, resonance, rewards and post-run analytics. Time scale changes wall-clock duration only; the report compares simulation-time duration.")
$reportPath = Join-Path $OutputRoot "p124_calibration_report.md"
[IO.File]::WriteAllText([IO.Path]::GetFullPath($reportPath), $md.ToString())

$index | ConvertTo-Json -Depth 8
if (-not $hardPass) {
    throw "P12.4 calibration remains in review. Inspect $indexPath and $reportPath."
}
