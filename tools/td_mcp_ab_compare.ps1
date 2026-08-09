param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [int]$LevelIndex = 9,
    [int]$DurationSeconds = 30,
    [float]$TimeScale = 4.0,
    [int[]]$RandomSeeds = @(1337, 2027, 9001),
    [int]$BonusBudget = 300,
    [string]$VariantAName = "Baseline",
    [string]$BuildPlanA = "5,4:RailLancer",
    [string]$UpgradePlanA = "",
    [string]$VariantBName = "CounterBuild",
    [string]$BuildPlanB = "5,4:CinderMortar;11,4:FrostCoil",
    [string]$UpgradePlanB = "5,4:Damage,Damage;11,4:Utility,Utility",
    [int]$MinExpectedScoreDelta = 0,
    [string]$OutputDirectory = "E:/TD/output/playtest/p6_ab",
    [string]$SummaryPath = "E:/TD/output/playtest/p6_ab_summary.json",
    [switch]$RefreshScripts,
    [switch]$AllowConsoleIssues
)

$ErrorActionPreference = "Stop"
$runnerPath = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
if (-not (Test-Path -LiteralPath $runnerPath)) {
    throw "Playtest runner not found: $runnerPath"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$summaryDirectory = Split-Path -Parent $SummaryPath
if ($summaryDirectory) {
    New-Item -ItemType Directory -Path $summaryDirectory -Force | Out-Null
}

function ConvertTo-SafeName {
    param([string]$Value)
    $safe = ($Value -replace "[^A-Za-z0-9_-]", "_").Trim("_")
    return $(if ([string]::IsNullOrWhiteSpace($safe)) { "variant" } else { $safe })
}

function Get-StateInt {
    param(
        [string]$State,
        [string]$Key,
        [int]$Default = 0
    )

    $match = [regex]::Match($State, "(?m)^" + [regex]::Escape($Key) + "=(-?\d+)\r?$")
    return $(if ($match.Success) { [int]$match.Groups[1].Value } else { $Default })
}

function Get-StateValue {
    param(
        [string]$State,
        [string]$Key,
        [string]$Default = ""
    )

    $match = [regex]::Match($State, "(?m)^" + [regex]::Escape($Key) + "=(.*)\r?$")
    return $(if ($match.Success) { $match.Groups[1].Value.Trim() } else { $Default })
}

function Get-Average {
    param([object[]]$Values)
    if ($null -eq $Values -or $Values.Count -eq 0) {
        return 0
    }

    return [Math]::Round((($Values | Measure-Object -Average).Average), 2)
}

function Get-RecordInt {
    param(
        [string]$Record,
        [string]$Key,
        [int]$Default = 0
    )

    $match = [regex]::Match($Record, "(?:^|,)" + [regex]::Escape($Key) + ":(-?\d+)(?:,|$)")
    return $(if ($match.Success) { [int]$match.Groups[1].Value } else { $Default })
}

function Get-StdDev {
    param([double[]]$Values)
    if ($null -eq $Values -or $Values.Count -le 1) {
        return 0
    }

    $average = ($Values | Measure-Object -Average).Average
    $variance = (($Values | ForEach-Object { [Math]::Pow($_ - $average, 2) }) | Measure-Object -Average).Average
    return [Math]::Round([Math]::Sqrt($variance), 2)
}

function Get-VariantAggregate {
    param(
        [string]$Name,
        [object[]]$Rows
    )

    $scores = @($Rows | ForEach-Object { [double]$_.score })
    $scoreMin = [int](($scores | Measure-Object -Minimum).Minimum)
    $scoreMax = [int](($scores | Measure-Object -Maximum).Maximum)
    return [ordered]@{
        name = $Name
        runs = $Rows.Count
        scoreAverage = Get-Average $scores
        scoreMin = $scoreMin
        scoreMax = $scoreMax
        scoreSpread = $scoreMax - $scoreMin
        scoreStdDev = Get-StdDev $scores
        coverageAverage = Get-Average @($Rows | ForEach-Object { [double]$_.coverage })
        counterAverage = Get-Average @($Rows | ForEach-Object { [double]$_.counter })
        outputAverage = Get-Average @($Rows | ForEach-Object { [double]$_.output })
        economyAverage = Get-Average @($Rows | ForEach-Object { [double]$_.economy })
        commandAverage = Get-Average @($Rows | ForEach-Object { [double]$_.command })
        killsAverage = Get-Average @($Rows | ForEach-Object { [double]$_.kills })
        escapesAverage = Get-Average @($Rows | ForEach-Object { [double]$_.escapes })
        damageAverage = Get-Average @($Rows | ForEach-Object { [double]$_.damage })
        hotspotHeatAverage = Get-Average @($Rows | ForEach-Object { [double]$_.hotspotHeat })
        hotspotCoverageAverage = Get-Average @($Rows | ForEach-Object { [double]$_.hotspotCoverage })
        allChecksPassed = @($Rows | Where-Object { -not $_.allChecksPassed }).Count -eq 0
        analyticsConsistent = @($Rows | Where-Object { -not $_.analyticsConsistent }).Count -eq 0
        topHotspots = @($Rows | ForEach-Object { $_.hotspot })
    }
}

$variants = @(
    [pscustomobject]@{ name = $VariantAName; buildPlan = $BuildPlanA; upgradePlan = $UpgradePlanA },
    [pscustomobject]@{ name = $VariantBName; buildPlan = $BuildPlanB; upgradePlan = $UpgradePlanB }
)
$rows = @()
$refreshPending = [bool]$RefreshScripts

foreach ($variant in $variants) {
    $safeName = ConvertTo-SafeName $variant.name
    foreach ($seed in $RandomSeeds) {
        $runName = "{0}_seed_{1}" -f $safeName, $seed
        $runSummaryPath = Join-Path $OutputDirectory "$runName.json"
        $screenshotPath = (Join-Path $OutputDirectory "$runName.png").Replace("\", "/")
        $runnerArgs = @{
            McpUrl = $McpUrl
            LevelIndex = $LevelIndex
            DurationSeconds = $DurationSeconds
            TimeScale = $TimeScale
            RandomSeed = $seed
            BuildPlan = $variant.buildPlan
            UpgradePlan = $variant.upgradePlan
            BonusBudget = $BonusBudget
            ScreenshotPath = $screenshotPath
            SummaryPath = $runSummaryPath
        }
        if ($refreshPending) {
            $runnerArgs.RefreshScripts = $true
            $refreshPending = $false
        }
        if ($AllowConsoleIssues) {
            $runnerArgs.AllowConsoleIssues = $true
        }

        try {
            & $runnerPath @runnerArgs | Out-Null
        } catch {
            throw "A/B run failed for $($variant.name), seed $seed. $($_.Exception.Message)"
        }

        $summary = Get-Content -Raw -LiteralPath $runSummaryPath | ConvertFrom-Json
        $state = [string]$summary.state.data.result
        $failedChecks = @($summary.checks.PSObject.Properties | Where-Object { -not [bool]$_.Value } | ForEach-Object { $_.Name })
        $hotspot = Get-StateValue $state "p6.hotspot.0" "none"
        $rows += [pscustomobject]@{
            variant = $variant.name
            seed = $seed
            score = [int]$summary.tacticalScore
            coverage = Get-StateInt $state "p6.score.coverage"
            counter = Get-StateInt $state "p6.score.counter"
            output = Get-StateInt $state "p6.score.output"
            economy = Get-StateInt $state "p6.score.economy"
            command = Get-StateInt $state "p6.score.command"
            kills = Get-StateInt $state "_totalKills"
            escapes = Get-StateInt $state "_totalEscapes"
            damage = Get-StateInt $state "_totalDamageDealt"
            integrityDamage = Get-StateInt $state "_totalIntegrityDamageTaken"
            hotspot = $hotspot
            hotspotHeat = Get-RecordInt $hotspot "heat"
            hotspotCoverage = Get-RecordInt $hotspot "coverage"
            recommendation1 = Get-StateValue $state "p6.recommendation.0" ""
            recommendation2 = Get-StateValue $state "p6.recommendation.1" ""
            recommendation3 = Get-StateValue $state "p6.recommendation.2" ""
            analyticsConsistent = $state.Contains("p6.audit.consistent=True")
            allChecksPassed = $failedChecks.Count -eq 0
            failedChecks = $failedChecks
            actualDurationSeconds = [double]$summary.actualDurationSeconds
            summaryPath = $runSummaryPath
            screenshotPath = $screenshotPath
        }
    }
}

$rowsA = @($rows | Where-Object { $_.variant -eq $VariantAName })
$rowsB = @($rows | Where-Object { $_.variant -eq $VariantBName })
$aggregateA = Get-VariantAggregate -Name $VariantAName -Rows $rowsA
$aggregateB = Get-VariantAggregate -Name $VariantBName -Rows $rowsB
$scoreDelta = [Math]::Round($aggregateB.scoreAverage - $aggregateA.scoreAverage, 2)
$dimensionDeltas = [ordered]@{
    coverage = [Math]::Round($aggregateB.coverageAverage - $aggregateA.coverageAverage, 2)
    counter = [Math]::Round($aggregateB.counterAverage - $aggregateA.counterAverage, 2)
    output = [Math]::Round($aggregateB.outputAverage - $aggregateA.outputAverage, 2)
    economy = [Math]::Round($aggregateB.economyAverage - $aggregateA.economyAverage, 2)
    command = [Math]::Round($aggregateB.commandAverage - $aggregateA.commandAverage, 2)
}
$winner = if ($scoreDelta -ge 3) { $VariantBName } elseif ($scoreDelta -le -3) { $VariantAName } else { "tie" }
$comparisonPassed = $scoreDelta -ge $MinExpectedScoreDelta -and
                    [bool]$aggregateA.allChecksPassed -and
                    [bool]$aggregateB.allChecksPassed -and
                    [bool]$aggregateA.analyticsConsistent -and
                    [bool]$aggregateB.analyticsConsistent

$result = [ordered]@{
    schemaVersion = "td-p6-ab-v1"
    levelIndex = $LevelIndex
    durationSeconds = $DurationSeconds
    timeScale = $TimeScale
    bonusBudget = $BonusBudget
    seeds = $RandomSeeds
    variants = @($aggregateA, $aggregateB)
    comparison = [ordered]@{
        winner = $winner
        scoreDeltaBMinusA = $scoreDelta
        minExpectedScoreDelta = $MinExpectedScoreDelta
        dimensionDeltasBMinusA = $dimensionDeltas
        killsDelta = [Math]::Round($aggregateB.killsAverage - $aggregateA.killsAverage, 2)
        escapesDelta = [Math]::Round($aggregateB.escapesAverage - $aggregateA.escapesAverage, 2)
        damageDelta = [Math]::Round($aggregateB.damageAverage - $aggregateA.damageAverage, 2)
        hotspotHeatDelta = [Math]::Round($aggregateB.hotspotHeatAverage - $aggregateA.hotspotHeatAverage, 2)
        hotspotCoverageDelta = [Math]::Round($aggregateB.hotspotCoverageAverage - $aggregateA.hotspotCoverageAverage, 2)
        passed = $comparisonPassed
    }
    runs = $rows
}

$result | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $SummaryPath -Encoding UTF8
$csvPath = [IO.Path]::ChangeExtension($SummaryPath, ".csv")
$rows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
$result | ConvertTo-Json -Depth 30

if (-not $comparisonPassed) {
    throw "A/B comparison failed: score delta $scoreDelta, required $MinExpectedScoreDelta. See $SummaryPath"
}
