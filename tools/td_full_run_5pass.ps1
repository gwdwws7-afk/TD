<#
.SYNOPSIS
  Full-campaign run: 5 representative levels x 5 passes with staged screenshots.
.DESCRIPTION
  L01 (tutorial), L05 (first boss), L09 (midgame), L13 (pressure), L20 (finale).
  Each level runs 5 times via P124 autoplay with focused_fire/control_lattice/
  adaptive_network strategies. Captures a final screenshot per run + collects
  WaveStat telemetry and console errors.
#>
param(
    [string]$OutputDir = "E:/TD/output/playtest/full_run_5pass",
    [int]$Passes = 5,
    [int]$RealSeconds = 95,
    [int]$TimeScale = 16,
    [int]$Seed = 20260810
)

$ErrorActionPreference = "Continue"
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }

# Representative levels: tutorial, first boss, midgame, pressure, finale
$Levels = @(1, 5, 9, 13, 20)
$LevelNames = @{ 1="L01_tutorial"; 5="L05_first_boss"; 9="L09_midgame"; 13="L13_pressure"; 20="L20_finale" }
$Strategies = @("focused_fire", "control_lattice", "adaptive_network")

$allResults = @()
$runIndex = 0
$totalRuns = $Levels.Count * $Passes
$startTime = Get-Date

foreach ($pass in 1..$Passes) {
    foreach ($level in $Levels) {
        $runIndex++
        $strategy = $Strategies[($level + $pass) % 3]
        $tag = "$($LevelNames[$level])_pass${pass}_${strategy}"
        $levelDir = Join-Path $OutputDir $tag
        if (-not (Test-Path $levelDir)) { New-Item -ItemType Directory -Path $levelDir -Force | Out-Null }

        $screenshotPath = (Join-Path $levelDir "${tag}_result.png").Replace("\", "/")
        $summaryPath = (Join-Path $levelDir "${tag}_summary.json").Replace("\", "/")
        $elapsed = [int]((Get-Date) - $startTime).TotalSeconds

        Write-Host "[$runIndex/$totalRuns ${elapsed}s] $tag ..." -NoNewline

        # Late levels (16-20) get more wall-clock time at 16x
        $levelSeconds = if ($level -ge 16) { [int]($RealSeconds * 1.4) } else { $RealSeconds }

        $childArgs = @(
            "-ExecutionPolicy","Bypass","-File","tools/td_mcp_playtest.ps1",
            "-LevelIndex",$level,
            "-RandomSeed",$Seed,
            "-TimeScale",$TimeScale,
            "-P124AutoplayStrategy",$strategy,
            "-P124MaxRealSeconds",$levelSeconds,
            "-SummaryPath",$summaryPath,
            "-ScreenshotPath",$screenshotPath,
            "-AllowConsoleIssues"
        )

        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = "powershell.exe"
        $psi.Arguments = ($childArgs -join ' ')
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true
        $proc = [System.Diagnostics.Process]::Start($psi)
        $stdoutTask = $proc.StandardOutput.ReadToEndAsync()
        $stderrTask = $proc.StandardError.ReadToEndAsync()
        $proc.WaitForExit()
        $exitCode = $proc.ExitCode
        $stdout = $stdoutTask.Result
        $stderr = $stderrTask.Result

        $row = [ordered]@{
            tag = $tag
            level = $level
            pass = $pass
            strategy = $strategy
            exitCode = $exitCode
        }

        # Parse summary JSON
        if (Test-Path $summaryPath) {
            $s = Get-Content $summaryPath -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json -ErrorAction SilentlyContinue
            if ($s) {
                $logs = $s.consoleLogs.data
                $row.logCount = $logs.Count
                $row.autoBuildOk = $s.autoBuild.success

                # Parse RunSummary or WaveStat
                $runLine = ($logs | Where-Object { $_ -like "*RunSummary*" } | Select-Object -Last 1)
                if ($runLine) {
                    foreach ($pair in ($runLine -split " ")) {
                        $kv = $pair -split "=",2
                        if ($kv.Count -eq 2) { $row[$kv[0]] = $kv[1] }
                    }
                    $row.dataSource = "RunSummary"
                } else {
                    $waveLines = $logs | Where-Object { $_ -like "*WaveStat*" }
                    if ($waveLines) {
                        $reached = 0; $totalKills = 0; $totalEscapes = 0
                        foreach ($wl in $waveLines) {
                            if ($wl -match "wave=(\d+)") { $reached = [int]$Matches[1] }
                            if ($wl -match "kills=(\d+)") { $totalKills += [int]$Matches[1] }
                            if ($wl -match "escapes=(\d+)") { $totalEscapes += [int]$Matches[1] }
                        }
                        $row.reachedWave = $reached
                        $row.totalKills = $totalKills
                        $row.totalEscapes = $totalEscapes
                        $lastWave = $waveLines | Select-Object -Last 1
                        if ($lastWave -match "readiness=(\S+)") { $row.lastReadiness = $Matches[1] }
                        if ($lastWave -match "budget=\S+->(\d+)") { $row.endingBudget = $Matches[1] }
                        if ($lastWave -match "integrity=\S+->(\d+)") { $row.endingIntegrity = $Matches[1] }
                        $row.dataSource = "WaveStat"
                    }
                }

                # Detect errors
                $errorLines = $logs | Where-Object {
                    $_ -match "error CS" -or $_ -match "Exception" -or $_ -match "NullReference" -or
                    $_ -match "Resources.Load.*null" -or $_ -match "audio.*fail" -or $_ -match "clip.*null"
                }
                $row.errorCount = $errorLines.Count
                $row.errors = $errorLines | Select-Object -First 5
            }
        }

        # Check screenshot
        $row.screenshotExists = (Test-Path $screenshotPath)
        if ($row.screenshotExists) {
            $row.screenshotSize = (Get-Item $screenshotPath).Length
        }

        $allResults += $row
        $status = if ($row.dataSource) { "$($row.dataSource) wave=$($row.reachedWave)" } else { "no-data" }
        $errStr = if ($row.errorCount -gt 0) { " ERRORS=$($row.errorCount)" } else { "" }
        Write-Host " exit=$exitCode $status$errStr"

        # Save aggregate after each run
        $allResults | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDir "aggregate_results.json") -Encoding UTF8
    }
}

# Final summary
Write-Host ""
Write-Host "=== RUN COMPLETE ==="
Write-Host "Total runs: $($allResults.Count)"
$totalElapsed = [int]((Get-Date) - $startTime).TotalSeconds
Write-Host "Total time: ${totalElapsed}s ($([math]::Round($totalElapsed/60,1)) min)"
$totalErrors = ($allResults | Measure-Object -Property errorCount -Sum).Sum
Write-Host "Total errors: $totalErrors"

$victories = ($allResults | Where-Object { $_.victory -eq "True" -or $_.result -eq "Victory" }).Count
Write-Host "Victories: $victories / $($allResults.Count)"

Write-Host ""
Write-Host "=== Per-level breakdown ==="
foreach ($level in $Levels) {
    $levelResults = $allResults | Where-Object { $_.level -eq $level }
    $avgWave = if ($levelResults) {
        ($levelResults | ForEach-Object { [int]$_.reachedWave } | Measure-Object -Average).Average
    } else { 0 }
    $levelErrors = ($levelResults | Measure-Object -Property errorCount -Sum).Sum
    Write-Host "  $($LevelNames[$level]): avgWave=$([math]::Round($avgWave,1)) errors=$levelErrors runs=$($levelResults.Count)"
}
