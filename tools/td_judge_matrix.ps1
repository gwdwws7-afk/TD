param(
    [string]$OutputDir = "E:/TD/output/playtest/judge_matrix",
    [string]$LevelsCsv = "1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20",
    [string]$StrategiesCsv = "focused_fire,control_lattice,adaptive_network",
    [int]$Seed = 20260807,
    [int]$RealSeconds = 95,
    [int]$TimeScale = 16,
    [int]$Pass = 1
)

$Levels = $LevelsCsv -split "," | ForEach-Object { [int]$_.Trim() }
$Strategies = $StrategiesCsv -split "," | ForEach-Object { $_.Trim() }

# Judge-grade matrix: P124 AI autoplay across all 20 levels x 3 strategies.
# Captures per-run summary JSON (victory, stars, score, waves, economy, combat)
# so the "annual judge" evaluation has quantitative evidence, not impressions.
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Path "$OutputDir/pass$Pass" -Force | Out-Null
$results = @()
$total = $Levels.Count * $Strategies.Count
$i = 0
$startTime = [DateTime]::UtcNow

foreach ($level in $Levels) {
    foreach ($strategy in $Strategies) {
        $i++
        $tag = "L{0:d2}_{1}" -f $level, $strategy
        $summaryPath = "$OutputDir/pass$Pass/$tag.json"
        $elapsed = ([DateTime]::UtcNow - $startTime).TotalSeconds
        Write-Host "[$i/$total ${elapsed}s] $tag ..." -NoNewline

        # Give late/resonance/boss levels (16-20) more wall-clock time at 16x.
        $levelSeconds = if ($level -ge 16) { [int]($RealSeconds * 1.4) } else { $RealSeconds }
        $childArgs = @(
            "-ExecutionPolicy","Bypass","-File","tools/td_mcp_playtest.ps1",
            "-LevelIndex",$level,
            "-RandomSeed",$Seed,
            "-TimeScale",$TimeScale,
            "-P124AutoplayStrategy",$strategy,
            "-P124MaxRealSeconds",$levelSeconds,
            "-SummaryPath",$summaryPath,
            "-ScreenshotPath","$OutputDir/pass$Pass/$tag.png",
            "-AllowConsoleIssues"
        )
        # Isolate subprocess: playtest throws on regression checks, which is informative
        # (AI lost / bad result) not fatal. Capture exit code without surfacing stderr as errors.
        $argString = $childArgs -join ' '
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = "powershell.exe"
        $psi.Arguments = $argString
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true
        $proc = [System.Diagnostics.Process]::Start($psi)
        $stdoutTask = $proc.StandardOutput.ReadToEndAsync()
        $stderrTask = $proc.StandardError.ReadToEndAsync()
        $proc.WaitForExit()
        $exitCode = $proc.ExitCode
        $null = $stdoutTask.Result
        $null = $stderrTask.Result

        $row = [ordered]@{ level=$level; strategy=$strategy; pass=$Pass; exitCode=$exitCode }
        if (Test-Path $summaryPath) {
            $s = Get-Content $summaryPath -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json -ErrorAction SilentlyContinue
            if ($s) {
                $logs = $s.consoleLogs.data
                # Prefer RunSummary (full run completed). Fall back to WaveStat aggregation
                # when P124 autoplay ran out of time before the level finished (e.g. L20 20 waves at 16x).
                $runLine = ($logs | Where-Object { $_ -like "*RunSummary*" } | Select-Object -Last 1)
                if ($runLine) {
                    foreach ($pair in ($runLine -split " ")) {
                        $kv = $pair -split "=",2
                        if ($kv.Count -eq 2) { $row[$kv[0]] = $kv[1] }
                    }
                    $row.dataSource = "RunSummary"
                } else {
                    # Aggregate from WaveStat lines: reachedWave, total kills/escapes, last readiness/budget
                    $waveLines = $logs | Where-Object { $_ -like "*WaveStat*" }
                    if ($waveLines) {
                        $lastWave = $waveLines | Select-Object -Last 1
                        $reached = 0; $totalKills = 0; $totalEscapes = 0
                        foreach ($wl in $waveLines) {
                            if ($wl -match "wave=(\d+)") { $reached = [int]$Matches[1] }
                            if ($wl -match "kills=(\d+)") { $totalKills += [int]$Matches[1] }
                            if ($wl -match "escapes=(\d+)") { $totalEscapes += [int]$Matches[1] }
                        }
                        $row.reachedWave = $reached
                        $row.totalKills = $totalKills
                        $row.totalEscapes = $totalEscapes
                        # last readiness/grade + budget/integrity from final wave
                        if ($lastWave -match "readiness=(\S+)") { $row.lastReadiness = $Matches[1] }
                        if ($lastWave -match "budget=\S+->(\d+)") { $row.endingBudget = $Matches[1] }
                        if ($lastWave -match "integrity=\S+->(\d+)") { $row.endingIntegrity = $Matches[1] }
                        $row.dataSource = "WaveStat"
                    }
                }
                $row.logCount = $logs.Count
                $row.autoBuildOk = $s.autoBuild.success
                $row.consoleIssueCount = $s.effectiveConsoleIssues.Count
            }
        }
        $results += [pscustomobject]$row
        $verdict = if ($row.result) { $row.result } else { "(time-limited)" }
        $reach = if ($row.reachedWave) { $row.reachedWave } else { "?" }
        $esc = if ($row.totalEscapes -ne $null) { $row.totalEscapes } else { "" }
        Write-Host " $verdict reachedWave=$reach escapes=$esc [$($row.dataSource)]"
        $results | ConvertTo-Csv -NoTypeInformation | Set-Content "$OutputDir/pass$Pass/_results.csv" -Encoding UTF8
    }
}

$results | ConvertTo-Csv -NoTypeInformation | Set-Content "$OutputDir/pass$Pass/_results.csv" -Encoding UTF8
$results | ConvertTo-Json -Depth 10 | Set-Content "$OutputDir/pass$Pass/_results.json" -Encoding UTF8
$totalElapsed = ([DateTime]::UtcNow - $startTime).TotalSeconds
Write-Host ""
Write-Host "=== MATRIX COMPLETE: $($results.Count) runs in $([int]$totalElapsed)s ==="
$victories = ($results | Where-Object { $_.result -eq "victory" }).Count
Write-Host "Victories: $victories / $($results.Count)"
