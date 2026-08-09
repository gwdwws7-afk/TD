param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputRoot = "E:/TD/output/playtest/p135_campaign_experience",
    [ValidateSet("Pilot", "Release")]
    [string]$Scope = "Release",
    [float]$TimeScale = 16,
    [int]$MaxRealSeconds = 110,
    [switch]$RefreshScripts,
    [switch]$ResumeExisting,
    [string[]]$RerunKeys = @()
)

$ErrorActionPreference = "Stop"
$startedUtc = [DateTime]::UtcNow
$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
$runRoot = Join-Path $OutputRoot "real_runs"
New-Item -ItemType Directory -Force -Path $OutputRoot, $runRoot | Out-Null

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

function Get-Pct {
    param([int]$Numerator, [int]$Denominator)
    if ($Denominator -le 0) { return 0 }
    return [Math]::Round(100 * $Numerator / $Denominator, 1)
}

function Add-RunSpec {
    param(
        [System.Collections.Generic.List[object]]$List,
        [int]$Level,
        [string]$Difficulty,
        [string]$Strategy,
        [string]$MechanicPolicy,
        [string]$Purpose
    )
    $difficultyId = if ($Difficulty -eq "EmberTrial") { "ember_trial" } else { $Difficulty.ToLowerInvariant() }
    $key = "L{0:d2}_{1}_{2}_{3}" -f $Level, $difficultyId, $Strategy, $MechanicPolicy
    if (@($List | Where-Object key -eq $key).Count -gt 0) { return }
    $List.Add([pscustomobject]@{
        key = $key
        level = $Level
        difficulty = $Difficulty
        difficultyId = $difficultyId
        strategy = $Strategy
        mechanicPolicy = $MechanicPolicy
        purpose = $Purpose
    })
}

function Get-PrimaryFailure {
    param($Run)
    if ([bool]$Run.baseline.victory) { return "victory" }
    if ([int]$Run.baseline.wavesCleared -le 2) { return "opening_collapse" }
    if ($Run.mechanicType -eq "boss_phase" -and [int]$Run.baseline.currentWave -ge [int]$Run.baseline.waveCount - 1) {
        return "boss_phase_breakdown"
    }
    $failures = @($Run.baseline.failureReasons)
    if ($failures.Count -gt 0) {
        return ([string]$failures[0] -replace ':\d+$', '')
    }
    if ([int]$Run.baseline.endingBudget -gt 700) { return "economy_underspend" }
    if ([int]$Run.baseline.escapes -gt 0) { return "route_leak" }
    return "output_collapse"
}

$focusLevels = @(5, 7, 9, 13, 15, 17, 20)
$examLevels = @(5, 9, 13, 17, 20)
$strategies = @("focused_fire", "control_lattice", "adaptive_network")
$difficulties = @("Standard", "Veteran", "EmberTrial")
$specs = New-Object System.Collections.Generic.List[object]

if ($Scope -eq "Release") {
    foreach ($level in 1..20) {
        foreach ($difficulty in $difficulties) {
            foreach ($strategy in $strategies) {
                Add-RunSpec $specs $level $difficulty $strategy "adaptive" "campaign_matrix"
            }
        }
    }
} else {
    foreach ($level in $focusLevels) {
        foreach ($strategy in $strategies) {
            Add-RunSpec $specs $level "Standard" $strategy "adaptive" "focus_strategy"
        }
        foreach ($difficulty in @("Veteran", "EmberTrial")) {
            Add-RunSpec $specs $level $difficulty "adaptive_network" "adaptive" "focus_difficulty"
        }
    }
}

foreach ($level in $examLevels) {
    Add-RunSpec $specs $level "Standard" "adaptive_network" "engage" "mechanic_ab"
    Add-RunSpec $specs $level "Standard" "adaptive_network" "hold" "mechanic_ab"
}

$rows = New-Object System.Collections.Generic.List[object]
$refreshPending = $RefreshScripts.IsPresent
$runIndex = 0
foreach ($spec in $specs) {
    $runIndex++
    $stem = $spec.key.ToLowerInvariant()
    $summaryPath = Join-Path $runRoot "$stem`_summary.json"
    $p124Path = Join-Path $runRoot "$stem`_p124.json"
    $p135Path = Join-Path $runRoot "$stem`_p135.json"
    $screenshotPath = Join-Path $runRoot "$stem.png"
    $forceRerun = $RerunKeys -contains $spec.key
    $reuse = $false
    if ($ResumeExisting -and -not $forceRerun -and
        (Test-Path -LiteralPath $p135Path) -and (Test-Path -LiteralPath $summaryPath)) {
        try {
            $existing = Get-Content -LiteralPath $p135Path -Raw -Encoding UTF8 | ConvertFrom-Json
            $reuse = [bool]$existing.baseline.completed -and -not [bool]$existing.baseline.stalled -and
                     [int]$existing.baseline.levelIndex -eq [int]$spec.level -and
                     [string]$existing.baseline.difficultyId -eq [string]$spec.difficultyId -and
                     [string]$existing.baseline.strategyId -eq [string]$spec.strategy -and
                     [string]$existing.mechanicPolicy -eq [string]$spec.mechanicPolicy
        } catch {
            $reuse = $false
        }
    }

    Write-Host ("[{0}/{1}] {2} {3}" -f $runIndex, $specs.Count, $spec.key, $(if ($reuse) { "reuse" } else { "run" }))
    $runnerPassed = $reuse
    $attempts = 0
    if (-not $reuse) {
        Remove-Item -LiteralPath $summaryPath, $p124Path, $p135Path, $screenshotPath -Force -ErrorAction SilentlyContinue
        $arguments = @{
            McpUrl = $McpUrl
            LevelIndex = $spec.level
            DurationSeconds = $MaxRealSeconds + 8
            TimeScale = $TimeScale
            ViewportWidth = 960
            ViewportHeight = 540
            FormationDifficulty = $spec.difficulty
            PrepareP85Difficulty = $true
            P124AutoplayStrategy = $spec.strategy
            P124SiteVariant = 0
            P124MaxRealSeconds = $MaxRealSeconds
            P124RunReportPath = $p124Path.Replace("\", "/")
            P135MechanicPolicy = $spec.mechanicPolicy
            P135RunReportPath = $p135Path.Replace("\", "/")
            RunP124Audit = $true
            RunP125EconomyAudit = $true
            PreserveCampaignProgress = $true
            ScreenshotPath = $screenshotPath.Replace("\", "/")
            SummaryPath = $summaryPath
        }
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
                $valid = $false
                if ((Test-Path -LiteralPath $p135Path) -and (Test-Path -LiteralPath $summaryPath)) {
                    try {
                        $probe = Get-Content -LiteralPath $p135Path -Raw -Encoding UTF8 | ConvertFrom-Json
                        $valid = [bool]$probe.baseline.completed -and -not [bool]$probe.baseline.stalled -and
                                 [int]$probe.baseline.levelIndex -eq [int]$spec.level -and
                                 [string]$probe.baseline.difficultyId -eq [string]$spec.difficultyId -and
                                 [string]$probe.baseline.strategyId -eq [string]$spec.strategy -and
                                 [string]$probe.mechanicPolicy -eq [string]$spec.mechanicPolicy
                    } catch {
                        $valid = $false
                    }
                }
                if ($valid) { break }
                if ($attempt -ge 3) { throw }
                Start-Sleep -Seconds 2
                $arguments.Remove("RefreshScripts")
            }
        }
    }

    $run = Get-Content -LiteralPath $p135Path -Raw -Encoding UTF8 | ConvertFrom-Json
    $summary = Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([int]$run.baseline.levelIndex -ne [int]$spec.level -or
        [string]$run.baseline.difficultyId -ne [string]$spec.difficultyId -or
        [string]$run.baseline.strategyId -ne [string]$spec.strategy -or
        [string]$run.mechanicPolicy -ne [string]$spec.mechanicPolicy) {
        throw "P13.5 run identity mismatch for $($spec.key): " +
              "L$($run.baseline.levelIndex)/$($run.baseline.difficultyId)/" +
              "$($run.baseline.strategyId)/$($run.mechanicPolicy)"
    }

    $rows.Add([pscustomobject]@{
        key = $spec.key
        purpose = $spec.purpose
        level = [int]$run.baseline.levelIndex
        map = [string]$run.baseline.mapId
        difficulty = [string]$run.baseline.difficultyId
        strategy = [string]$run.baseline.strategyId
        mechanic = [string]$run.mechanicType
        mechanicPolicy = [string]$run.mechanicPolicy
        completed = [bool]$run.baseline.completed
        stalled = [bool]$run.baseline.stalled
        victory = [bool]$run.baseline.victory
        wavesCleared = [int]$run.baseline.wavesCleared
        score = [int]$run.baseline.totalScore
        integrity = [int]$run.baseline.integrityRemaining
        escapes = [int]$run.baseline.escapes
        firstLeak = [int]$run.baseline.firstLeakWave
        firstWavePressure = [int]$run.firstWavePressureScore
        firstWaveEscapes = [int]$run.firstWaveEscapes
        endingBudget = [int]$run.baseline.endingBudget
        finalFiveConversion = [double]$run.baseline.finalFiveSpendConversionPct
        economyDecision = [bool]$run.baseline.economyDecisionValue
        topSite = [string]$run.baseline.topSite
        topSiteShare = [double]$run.baseline.topSiteDamageSharePct
        topKind = [string]$(if ([string]::IsNullOrWhiteSpace($run.baseline.topTowerKindByContribution)) {
            $run.baseline.topTowerKind
        } else {
            $run.baseline.topTowerKindByContribution
        })
        topKindShare = [double]$run.baseline.topTowerKindContributionSharePct
        topDamageKind = [string]$run.baseline.topTowerKind
        topDamageKindShare = [double]$run.baseline.topTowerKindDamageSharePct
        towerKinds = [int]$run.baseline.towerKindsUsed
        damageBranches = [int]$run.damageBranchUpgrades
        utilityBranches = [int]$run.utilityBranchUpgrades
        routeEntropy = [double]$run.routeDamageEntropyPct
        mechanicUses = @($run.mechanicActivationWaves).Count
        buildsAfterMechanic = [int]$run.buildsAfterFirstMechanic
        upgradesAfterMechanic = [int]$run.upgradesAfterFirstMechanic
        bossPhases = [int]$run.bossPhasesTriggered
        bossSuppressed = [int]$run.bossPhasesSuppressed
        placementSignature = [string]$run.placementSignature
        compositionSignature = [string]$run.compositionSignature
        branchSignature = [string]$run.branchSignature
        replaySignature = [string]$run.replaySignature
        primaryFailure = Get-PrimaryFailure $run
        warningReasons = @($run.baseline.failureReasons) -join ";"
        analyticsConsistent = [bool]$run.baseline.analyticsConsistent
        runnerPassed = $runnerPassed
        attempts = $attempts
        uiBounds = [bool]$summary.checks.uiBounds
        uiOverlap = [bool]$summary.checks.uiOverlap
        uiTextFit = [bool]$summary.checks.uiTextFit
        consoleClean = [bool]$summary.checks.consoleClean
        p135Audit = [bool]$summary.checks.p135Audit
        p135Report = [IO.Path]::GetFullPath($p135Path)
        summary = [IO.Path]::GetFullPath($summaryPath)
        screenshot = [IO.Path]::GetFullPath($screenshotPath)
        runObject = $run
    })
}

$normalRows = @($rows | Where-Object purpose -ne "mechanic_ab")
$eligible = @($rows | Where-Object { $_.completed -and -not $_.stalled })
$victoryRows = @($normalRows | Where-Object victory)

$levelSummary = foreach ($group in @($normalRows | Group-Object level,difficulty | Sort-Object {
    [int]($_.Name -split ',')[0]
}, Name)) {
    $matches = @($group.Group)
    [pscustomobject]@{
        level = [int]$matches[0].level
        map = $matches[0].map
        difficulty = $matches[0].difficulty
        runs = $matches.Count
        wins = @($matches | Where-Object victory).Count
        winRatePct = Get-Pct @($matches | Where-Object victory).Count $matches.Count
        medianScore = Get-Median @($matches | ForEach-Object { [double]$_.score })
        medianFirstWavePressure = Get-Median @($matches | ForEach-Object { [double]$_.firstWavePressure })
        medianEscapes = Get-Median @($matches | ForEach-Object { [double]$_.escapes })
        averageEscapes = [Math]::Round(($matches | Measure-Object escapes -Average).Average, 2)
        medianEndingBudget = Get-Median @($matches | ForEach-Object { [double]$_.endingBudget })
        maxTopSiteSharePct = [Math]::Round((($matches.topSiteShare | Measure-Object -Maximum).Maximum), 1)
        maxTopKindSharePct = [Math]::Round((($matches.topKindShare | Measure-Object -Maximum).Maximum), 1)
    }
}

$difficultySummary = foreach ($difficulty in @("standard", "veteran", "ember_trial")) {
    $matches = @($normalRows | Where-Object difficulty -eq $difficulty)
    [pscustomobject]@{
        difficulty = $difficulty
        runs = $matches.Count
        wins = @($matches | Where-Object victory).Count
        winRatePct = Get-Pct @($matches | Where-Object victory).Count $matches.Count
        medianScore = Get-Median @($matches | ForEach-Object { [double]$_.score })
        medianFirstWavePressure = Get-Median @($matches | ForEach-Object { [double]$_.firstWavePressure })
        medianEscapes = Get-Median @($matches | ForEach-Object { [double]$_.escapes })
        averageEscapes = [Math]::Round(($matches | Measure-Object escapes -Average).Average, 2)
        medianEndingBudget = Get-Median @($matches | ForEach-Object { [double]$_.endingBudget })
    }
}

$towerFacts = New-Object System.Collections.Generic.List[object]
foreach ($row in $normalRows) {
    foreach ($tower in @($row.runObject.towers)) {
        $towerFacts.Add([pscustomobject]@{
            key = $row.key
            level = $row.level
            difficulty = $row.difficulty
            strategy = $row.strategy
            towerKind = [string]$tower.towerKind
            cell = "$($tower.cellX),$($tower.cellY)"
            damage = [int]$tower.damage
            kills = [int]$tower.kills
            controls = [int]$tower.controls
            damageBranches = [int]$tower.damageBranchUpgrades
            utilityBranches = [int]$tower.utilityBranchUpgrades
            zeroContribution = [int]$tower.damage -eq 0 -and [int]$tower.controls -eq 0
        })
    }
}

$towerUsage = foreach ($kind in @($towerFacts.towerKind | Sort-Object -Unique)) {
    $facts = @($towerFacts | Where-Object towerKind -eq $kind)
    $presentRuns = @($facts.key | Sort-Object -Unique).Count
    [pscustomobject]@{
        towerKind = $kind
        runsPresent = $presentRuns
        runUsageRatePct = Get-Pct $presentRuns $normalRows.Count
        towersBuilt = $facts.Count
        damage = ($facts | Measure-Object damage -Sum).Sum
        kills = ($facts | Measure-Object kills -Sum).Sum
        controls = ($facts | Measure-Object controls -Sum).Sum
        zeroContributionTowers = @($facts | Where-Object zeroContribution).Count
        zeroContributionRatePct = Get-Pct @($facts | Where-Object zeroContribution).Count $facts.Count
    }
}

$branchSelection = foreach ($group in @($towerFacts | Group-Object towerKind,strategy)) {
    $facts = @($group.Group)
    $damage = ($facts | Measure-Object damageBranches -Sum).Sum
    $utility = ($facts | Measure-Object utilityBranches -Sum).Sum
    $total = $damage + $utility
    [pscustomobject]@{
        towerKind = $facts[0].towerKind
        strategy = $facts[0].strategy
        damageBranches = $damage
        utilityBranches = $utility
        damageRatePct = Get-Pct $damage $total
        utilityRatePct = Get-Pct $utility $total
    }
}

$failureDistribution = foreach ($group in @($normalRows | Group-Object primaryFailure)) {
    [pscustomobject]@{
        reason = $group.Name
        runs = $group.Count
        sharePct = Get-Pct $group.Count $normalRows.Count
    }
}

$warningFacts = New-Object System.Collections.Generic.List[object]
foreach ($row in $normalRows) {
    foreach ($warning in @($row.runObject.baseline.failureReasons)) {
        $parts = [string]$warning -split ':'
        $warningFacts.Add([pscustomobject]@{
            key = $row.key
            reason = $parts[0]
            count = if ($parts.Count -gt 1) { [int]$parts[1] } else { 1 }
        })
    }
}
$warningDistribution = foreach ($group in @($warningFacts | Group-Object reason)) {
    [pscustomobject]@{
        reason = $group.Name
        affectedRuns = @($group.Group.key | Sort-Object -Unique).Count
        events = ($group.Group | Measure-Object count -Sum).Sum
    }
}

$replayDifferences = foreach ($group in @($normalRows | Group-Object level,difficulty)) {
    $matches = @($group.Group)
    [pscustomobject]@{
        level = $matches[0].level
        difficulty = $matches[0].difficulty
        runs = $matches.Count
        distinctPlacements = @($matches.placementSignature | Sort-Object -Unique).Count
        distinctCompositions = @($matches.compositionSignature | Sort-Object -Unique).Count
        distinctBranches = @($matches.branchSignature | Sort-Object -Unique).Count
        distinctReplayStrategies = @($matches.replaySignature | Sort-Object -Unique).Count
        strategyDifference = @($matches.replaySignature | Sort-Object -Unique).Count -ge [Math]::Min(2, $matches.Count)
    }
}

$mechanicComparisons = foreach ($level in $examLevels) {
    $engage = @($rows | Where-Object { $_.purpose -eq "mechanic_ab" -and $_.level -eq $level -and $_.mechanicPolicy -eq "engage" })[0]
    $hold = @($rows | Where-Object { $_.purpose -eq "mechanic_ab" -and $_.level -eq $level -and $_.mechanicPolicy -eq "hold" })[0]
    $placementChanged = $engage.placementSignature -ne $hold.placementSignature
    $compositionChanged = $engage.compositionSignature -ne $hold.compositionSignature
    $branchChanged = $engage.branchSignature -ne $hold.branchSignature
    [pscustomobject]@{
        level = $level
        map = $engage.map
        mechanic = $engage.mechanic
        engageUses = $engage.mechanicUses
        holdUses = $hold.mechanicUses
        placementChanged = $placementChanged
        compositionChanged = $compositionChanged
        branchChanged = $branchChanged
        decisionChanged = $placementChanged -or $compositionChanged -or $branchChanged
        scoreDelta = $engage.score - $hold.score
        integrityDelta = $engage.integrity - $hold.integrity
        budgetDelta = $engage.endingBudget - $hold.endingBudget
        routeEntropyDelta = [Math]::Round($engage.routeEntropy - $hold.routeEntropy, 1)
    }
}

$completionPass = $eligible.Count -eq $rows.Count
$surfacePass = @($rows | Where-Object {
    -not $_.uiBounds -or -not $_.uiOverlap -or -not $_.uiTextFit -or -not $_.consoleClean
}).Count -eq 0
$telemetryPass = @($rows | Where-Object {
    -not $_.analyticsConsistent -or -not $_.p135Audit
}).Count -eq 0
$firstWavePass = @($normalRows | Where-Object {
    $_.firstWavePressure -gt 85 -or
    ($_.difficulty -eq "standard" -and $_.firstWaveEscapes -gt 4) -or
    ($_.difficulty -eq "veteran" -and $_.firstWaveEscapes -gt 6) -or
    ($_.difficulty -eq "ember_trial" -and $_.firstWaveEscapes -gt 8)
}).Count -eq 0
$siteDominancePass = @($normalRows | Where-Object {
    $_.level -ge 5 -and $_.topSiteShare -gt 58
}).Count -eq 0
$kindDominancePass = @($normalRows | Where-Object {
    $_.level -ge 5 -and $_.towerKinds -gt 1 -and $_.topKindShare -gt 78
}).Count -eq 0
$economyDecisionPct = Get-Pct @($victoryRows | Where-Object economyDecision).Count $victoryRows.Count
$economyPass = $economyDecisionPct -ge 90 -and
               @($victoryRows | Where-Object endingBudget -gt 999).Count -eq 0
$replayDifferencePct = Get-Pct @($replayDifferences | Where-Object strategyDifference).Count $replayDifferences.Count
$replayPass = $replayDifferencePct -ge 95
$mechanicPass = @($mechanicComparisons | Where-Object {
    -not $_.decisionChanged -or $_.engageUses -le 0 -or $_.holdUses -ne 0
}).Count -eq 0
$focusPass = @($normalRows | Where-Object {
    $_.level -in $focusLevels -and $_.difficulty -eq "standard" -and -not $_.victory
}).Count -le [Math]::Max(1, [Math]::Floor(@($normalRows | Where-Object {
    $_.level -in $focusLevels -and $_.difficulty -eq "standard"
}).Count * 0.15))
$bossPass = @($rows | Where-Object {
    $_.level -eq 20 -and $_.victory -and $_.mechanicPolicy -ne "hold" -and $_.bossPhases -lt 2
}).Count -eq 0
$difficultySeparated = $difficultySummary.Count -eq 3 -and (
    $difficultySummary[0].winRatePct - $difficultySummary[2].winRatePct -ge 10 -or
    $difficultySummary[2].medianEscapes -ge $difficultySummary[0].medianEscapes + 1 -or
    $difficultySummary[2].averageEscapes -ge $difficultySummary[0].averageEscapes + 0.5 -or
    $difficultySummary[2].medianFirstWavePressure -gt $difficultySummary[0].medianFirstWavePressure)
$difficultyOrderPass = $difficultySummary.Count -eq 3 -and
    $difficultySummary[0].winRatePct -ge $difficultySummary[1].winRatePct -and
    $difficultySummary[1].winRatePct -ge $difficultySummary[2].winRatePct -and
    $difficultySummary[0].medianEscapes -le $difficultySummary[1].medianEscapes -and
    $difficultySummary[1].medianEscapes -le $difficultySummary[2].medianEscapes -and
    $difficultySummary[0].averageEscapes -le $difficultySummary[1].averageEscapes -and
    $difficultySummary[1].averageEscapes -le $difficultySummary[2].averageEscapes -and
    $difficultySeparated
$hardPass = $completionPass -and $surfacePass -and $telemetryPass -and $firstWavePass -and
    $siteDominancePass -and $kindDominancePass -and $economyPass -and $replayPass -and
    $mechanicPass -and $focusPass -and $bossPass -and $difficultyOrderPass

$exportRows = $rows | Select-Object * -ExcludeProperty runObject
$exportRows | Export-Csv -LiteralPath (Join-Path $OutputRoot "p135_real_runs.csv") -NoTypeInformation -Encoding UTF8
$levelSummary | Export-Csv -LiteralPath (Join-Path $OutputRoot "p135_level_curve.csv") -NoTypeInformation -Encoding UTF8
$difficultySummary | Export-Csv -LiteralPath (Join-Path $OutputRoot "p135_difficulty_summary.csv") -NoTypeInformation -Encoding UTF8
$towerUsage | Export-Csv -LiteralPath (Join-Path $OutputRoot "p135_tower_usage.csv") -NoTypeInformation -Encoding UTF8
$branchSelection | Export-Csv -LiteralPath (Join-Path $OutputRoot "p135_branch_selection.csv") -NoTypeInformation -Encoding UTF8
$failureDistribution | Export-Csv -LiteralPath (Join-Path $OutputRoot "p135_failure_distribution.csv") -NoTypeInformation -Encoding UTF8
$warningDistribution | Export-Csv -LiteralPath (Join-Path $OutputRoot "p135_warning_distribution.csv") -NoTypeInformation -Encoding UTF8
$replayDifferences | Export-Csv -LiteralPath (Join-Path $OutputRoot "p135_replay_differences.csv") -NoTypeInformation -Encoding UTF8
$mechanicComparisons | Export-Csv -LiteralPath (Join-Path $OutputRoot "p135_mechanic_ab.csv") -NoTypeInformation -Encoding UTF8

$index = [ordered]@{
    schemaVersion = "p135-campaign-experience-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $startedUtc).TotalSeconds, 1)
    scope = $Scope
    expectedMainMatrixRuns = if ($Scope -eq "Release") { 180 } else { 35 }
    mainMatrixRuns = $normalRows.Count
    mechanicAbRuns = @($rows | Where-Object purpose -eq "mechanic_ab").Count
    completedRuns = $eligible.Count
    victories = @($normalRows | Where-Object victory).Count
    economyDecisionPct = $economyDecisionPct
    replayDifferencePct = $replayDifferencePct
    gates = [ordered]@{
        completion = $completionPass
        surfaces = $surfacePass
        telemetry = $telemetryPass
        firstWave = $firstWavePass
        siteDominance = $siteDominancePass
        kindDominance = $kindDominancePass
        economy = $economyPass
        replayDifference = $replayPass
        mapMechanics = $mechanicPass
        focusLevels = $focusPass
        bossPhases = $bossPass
        difficultyOrder = $difficultyOrderPass
    }
    difficultySummary = $difficultySummary
    focusLevels = $focusLevels
    mechanicComparisons = $mechanicComparisons
    failureDistribution = $failureDistribution
    warningDistribution = $warningDistribution
    towerUsage = $towerUsage
    branchSelection = $branchSelection
    hardPass = $hardPass
}
$indexPath = Join-Path $OutputRoot "p135_campaign_experience_index.json"
$index | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $indexPath -Encoding UTF8

$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine("# P13.5 Campaign Experience Calibration")
[void]$md.AppendLine("")
[void]$md.AppendLine("Generated: ``$($index.generatedUtc)``  ")
[void]$md.AppendLine("Mode: accelerated rendered Unity runtime autoplay at ``${TimeScale}x``.  ")
[void]$md.AppendLine("Main matrix: **$($normalRows.Count)** runs; mechanic counterfactuals: **$(@($rows | Where-Object purpose -eq 'mechanic_ab').Count)** runs.  ")
[void]$md.AppendLine("")
[void]$md.AppendLine("## Release Gates")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Gate | Result |")
[void]$md.AppendLine("|---|---:|")
foreach ($gate in $index.gates.GetEnumerator()) {
    [void]$md.AppendLine("| $($gate.Key) | $($gate.Value) |")
}
[void]$md.AppendLine("| **P13.5 hard pass** | **$hardPass** |")
[void]$md.AppendLine("")
[void]$md.AppendLine("## Difficulty")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Difficulty | Wins | Win rate | Median score | First-wave pressure | Median / avg escapes | Ending budget |")
[void]$md.AppendLine("|---|---:|---:|---:|---:|---:|---:|")
foreach ($row in $difficultySummary) {
    [void]$md.AppendLine("| $($row.difficulty) | $($row.wins)/$($row.runs) | $($row.winRatePct)% | $($row.medianScore) | $($row.medianFirstWavePressure) | $($row.medianEscapes) / $($row.averageEscapes) | $($row.medianEndingBudget) |")
}
[void]$md.AppendLine("")
[void]$md.AppendLine("## Map Mechanic Counterfactuals")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Level | Map mechanic | Uses engage/hold | Placement | Composition | Branches | Score delta |")
[void]$md.AppendLine("|---:|---|---:|---:|---:|---:|---:|")
foreach ($row in $mechanicComparisons) {
    [void]$md.AppendLine("| L$('{0:d2}' -f $row.level) | $($row.mechanic) | $($row.engageUses)/$($row.holdUses) | $($row.placementChanged) | $($row.compositionChanged) | $($row.branchChanged) | $($row.scoreDelta) |")
}
[void]$md.AppendLine("")
[void]$md.AppendLine("## Failure Reasons")
[void]$md.AppendLine("")
foreach ($row in $failureDistribution | Sort-Object runs -Descending) {
    [void]$md.AppendLine("- ``$($row.reason)``: $($row.runs) runs ($($row.sharePct)%).")
}
[void]$md.AppendLine("")
[void]$md.AppendLine("## Tower Usage")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Tower | Run usage | Built | Zero contribution | Damage | Kills | Controls |")
[void]$md.AppendLine("|---|---:|---:|---:|---:|---:|---:|")
foreach ($row in $towerUsage | Sort-Object towerKind) {
    [void]$md.AppendLine("| $($row.towerKind) | $($row.runUsageRatePct)% | $($row.towersBuilt) | $($row.zeroContributionRatePct)% | $($row.damage) | $($row.kills) | $($row.controls) |")
}
[void]$md.AppendLine("")
[void]$md.AppendLine("## Focus Levels")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Level | Difficulty | Wins | Score | First pressure | Escapes | Budget | Max site/kind |")
[void]$md.AppendLine("|---:|---|---:|---:|---:|---:|---:|---:|")
foreach ($row in $levelSummary | Where-Object level -in $focusLevels) {
    [void]$md.AppendLine("| L$('{0:d2}' -f $row.level) | $($row.difficulty) | $($row.wins)/$($row.runs) | $($row.medianScore) | $($row.medianFirstWavePressure) | $($row.medianEscapes) | $($row.medianEndingBudget) | $($row.maxTopSiteSharePct)% / $($row.maxTopKindSharePct)% |")
}
[void]$md.AppendLine("")
[void]$md.AppendLine("Raw evidence: ``p135_real_runs.csv`` and per-run ``real_runs/*_p135.json``.")

$reportPath = Join-Path $OutputRoot "p135_campaign_experience_report.md"
$md.ToString() | Set-Content -LiteralPath $reportPath -Encoding UTF8

$index | ConvertTo-Json -Depth 6
if (-not $hardPass) {
    throw "P13.5 calibration gates failed. See $reportPath"
}
