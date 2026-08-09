param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputRoot = "E:/TD/output/playtest/p1250_economy_gate",
    [ValidateSet("Pilot", "Release")]
    [string]$Scope = "Release",
    [float]$TimeScale = 16,
    [int]$MaxRealSeconds = 90,
    [switch]$RefreshScripts,
    [switch]$ResumeExisting
)

$ErrorActionPreference = "Stop"
$startedUtc = [DateTime]::UtcNow
$runner = Join-Path $PSScriptRoot "td_mcp_p124_full_campaign_calibration.ps1"

$arguments = @{
    McpUrl = $McpUrl
    OutputRoot = $OutputRoot
    Scope = $Scope
    TimeScale = $TimeScale
    MaxRealSeconds = $MaxRealSeconds
}
if ($RefreshScripts) { $arguments.RefreshScripts = $true }
if ($ResumeExisting) { $arguments.ResumeExisting = $true }

& $runner @arguments | Out-Null

$runFiles = @(Get-ChildItem -LiteralPath (Join-Path $OutputRoot "real_runs") -Filter "*_run.json" -File)
$runs = @($runFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
$victories = @($runs | Where-Object victory)
$economyFailures = @($victories | Where-Object { -not [bool]$_.economyDecisionValue })
$earlySaturation = @($runs | Where-Object {
    [int]$_.firstSaturatedWave -gt 0 -and [int]$_.firstSaturatedWave -lt [int]$_.finalFiveStartWave
})
$legacyOverflow = @($runs | Where-Object {
    [int]$_.towersBuilt -ge 12 -and [int]$_.upgradesPurchased -ge 36 -and [int]$_.endingBudget -ge 1000
})
$telemetryFailures = @($runs | Where-Object {
    ([bool]$_.victory -and @($_.finalFiveEconomy).Count -ne 5) -or
    [int]$_.combatIncome -le 0 -or [int]$_.clearIncome -le 0
})

$fastPath = Join-Path $OutputRoot "fast_matrix/p102_balance_matrix.json"
$fast = Get-Content -LiteralPath $fastPath -Raw | ConvertFrom-Json
$fastEconomyErrors = @($fast.alarms | Where-Object {
    $_.severity -eq "ERROR" -and $_.code -in @("ECONOMY_SATURATION", "FORTIFICATION_SATURATES_EARLY")
})

$victoryEndingBudgets = @($victories | ForEach-Object { [int]$_.endingBudget })
$victoryLatePurchases = @($victories | ForEach-Object { [int]$_.finalFivePurchases })
$maxEndingBudget = if ($victoryEndingBudgets.Count -gt 0) {
    ($victoryEndingBudgets | Measure-Object -Maximum).Maximum
} else { 0 }
$minLatePurchases = if ($victoryLatePurchases.Count -gt 0) {
    ($victoryLatePurchases | Measure-Object -Minimum).Minimum
} else { 0 }
$averageLateConversion = if ($victories.Count -gt 0) {
    [Math]::Round((($victories.finalFiveSpendConversionPct | Measure-Object -Average).Average), 1)
} else { 0 }

$allCompleted = $runs.Count -gt 0 -and @($runs | Where-Object { -not [bool]$_.completed -or [bool]$_.stalled }).Count -eq 0
$realEconomyPass = $victories.Count -gt 0 -and $economyFailures.Count -eq 0 -and
                   $earlySaturation.Count -eq 0 -and $legacyOverflow.Count -eq 0 -and
                   $telemetryFailures.Count -eq 0
$fastEconomyPass = [bool]$fast.hardPass -and $fastEconomyErrors.Count -eq 0
$hardPass = $allCompleted -and $realEconomyPass -and $fastEconomyPass

$rows = @($runs | Sort-Object levelIndex, difficultyId, strategyId, siteVariant | ForEach-Object {
    [pscustomobject]@{
        level = $_.levelIndex
        difficulty = $_.difficultyId
        strategy = $_.strategyId
        victory = $_.victory
        endingBudget = $_.endingBudget
        towers = $_.towersBuilt
        upgrades = $_.upgradesPurchased
        firstSaturatedWave = $_.firstSaturatedWave
        finalFiveStartWave = $_.finalFiveStartWave
        finalFiveStartingBudget = $_.finalFiveStartingBudget
        finalFiveIncome = $_.finalFiveGrossIncome
        finalFiveSpend = $_.finalFiveSpend
        finalFivePurchases = $_.finalFivePurchases
        finalFiveSpendConversionPct = $_.finalFiveSpendConversionPct
        economyDecisionValue = $_.economyDecisionValue
    }
})
$csvPath = Join-Path $OutputRoot "p1250_economy_runs.csv"
$rows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8

$reportPath = Join-Path $OutputRoot "p1250_economy_report.md"
$report = @"
# P12.5.0 Economy Gate

- Generated UTC: $([DateTime]::UtcNow.ToString("o"))
- Scope: $Scope
- Runtime runs: $($runs.Count), victories: $($victories.Count)
- Completed without stall: $allCompleted
- Runtime economy gate: $realEconomyPass
- Fast 180-run matrix: $fastEconomyPass
- Hard pass: $hardPass

## Final-five decisions

- Victory reserve maximum: $maxEndingBudget
- Victory late purchases minimum: $minLatePurchases
- Average late spend conversion: $averageLateConversion%
- Economy decision failures: $($economyFailures.Count)
- Saturated before final five: $($earlySaturation.Count)
- Legacy full-build overflow (12 towers / 36 upgrades / >=1000): $($legacyOverflow.Count)
- Telemetry failures: $($telemetryFailures.Count)

## Scope guard

- Enemy HP and wave pressure are not modified by P12.5.0.
- Runtime and deterministic simulation both use `TDEconomyTuning` for bounty, clear reward, upgrade price and command cost rules.
"@
Set-Content -LiteralPath $reportPath -Value $report -Encoding UTF8

$audit = [ordered]@{
    schemaVersion = "p1250-economy-audit-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $startedUtc).TotalSeconds, 2)
    scope = $Scope
    totalRuns = $runs.Count
    victories = $victories.Count
    allCompleted = $allCompleted
    economyDecisionFailures = $economyFailures.Count
    earlySaturationRuns = $earlySaturation.Count
    legacyOverflowRuns = $legacyOverflow.Count
    telemetryFailures = $telemetryFailures.Count
    maxVictoryEndingBudget = $maxEndingBudget
    minVictoryLatePurchases = $minLatePurchases
    averageLateSpendConversionPct = $averageLateConversion
    fastHardPass = [bool]$fast.hardPass
    fastEconomyErrorCount = $fastEconomyErrors.Count
    hardPass = $hardPass
    artifacts = [ordered]@{
        report = $reportPath
        runsCsv = $csvPath
        fastMatrix = $fastPath
    }
}
$auditPath = Join-Path $OutputRoot "p1250_economy_audit.json"
$audit | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $auditPath -Encoding UTF8
$audit | ConvertTo-Json -Depth 6

if (-not $hardPass) {
    throw "P12.5.0 economy gate failed. Inspect $reportPath"
}
