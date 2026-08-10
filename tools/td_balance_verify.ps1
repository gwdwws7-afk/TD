<#
.SYNOPSIS
  Verify balance changes: run RailLancer-only plan on L01/L09/L20 with extended time.
  Also tests R2 (endgame completion) and R3 (economy saturation).
#>
param(
    [string]$OutputDir = "E:/TD/output/playtest/balance_verify"
)

$ErrorActionPreference = "Continue"
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }

# Test configs: level, strategy, time seconds, tag
$Configs = @(
    @{ Level=1;  Strategy="focused_fire";       Secs=200; Tag="L01_rail_only_long" },
    @{ Level=9;  Strategy="focused_fire";       Secs=200; Tag="L09_rail_only_long" },
    @{ Level=20; Strategy="focused_fire";       Secs=250; Tag="L20_rail_only_long" },
    @{ Level=9;  Strategy="control_lattice";    Secs=200; Tag="L09_mixed_strategy" }
)

$allResults = @()
$start = Get-Date

foreach ($cfg in $Configs) {
    $tag = $cfg.Tag
    $screenshot = (Join-Path $OutputDir "${tag}.png").Replace("\", "/")
    $summary = (Join-Path $OutputDir "${tag}_summary.json").Replace("\", "/")
    $elapsed = [int]((Get-Date) - $start).TotalSeconds

    Write-Host "[$tag ${elapsed}s] Level $($cfg.Level) $($cfg.Strategy) $($cfg.Secs)s ..." -NoNewline

    $childArgs = @(
        "-ExecutionPolicy","Bypass","-File","tools/td_mcp_playtest.ps1",
        "-LevelIndex",$cfg.Level,
        "-RandomSeed","20260810",
        "-TimeScale","16",
        "-P124AutoplayStrategy",$cfg.Strategy,
        "-P124MaxRealSeconds",$cfg.Secs,
        "-SummaryPath",$summary,
        "-ScreenshotPath",$screenshot,
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
    $proc.WaitForExit()

    if (Test-Path $summary) {
        $s = Get-Content $summary -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json -ErrorAction SilentlyContinue
        if ($s) {
            $logs = $s.consoleLogs.data
            $waveLines = $logs | Where-Object { $_ -like "*WaveStat*" }
            $runLines = $logs | Where-Object { $_ -like "*RunSummary*" }
            $errs = $logs | Where-Object { $_ -match "Exception|NullReference|error CS" }

            $reached = 0; $totalKills = 0; $totalEscapes = 0; $endBudget = "?"; $endIntegrity = "?"
            foreach ($wl in $waveLines) {
                if ($wl -match "wave=(\d+)") { $reached = [int]$Matches[1] }
                if ($wl -match "kills=(\d+)") { $totalKills += [int]$Matches[1] }
                if ($wl -match "escapes=(\d+)") { $totalEscapes += [int]$Matches[1] }
                if ($wl -match "budget=\S+->(\d+)") { $endBudget = $Matches[1] }
                if ($wl -match "integrity=\S+->(\d+)") { $endIntegrity = $Matches[1] }
            }

            $hasVictory = $runLines.Count -gt 0
            $tc = $s.tacticalScore
            $contribution = ""
            $p124data = $s.p124Write.data
            if ($p124data -is [string]) {
                $contribMatch = [regex]::Match($p124data, "site=([^,]+),kindValue=([^,]+),kindDamage=([^]]+)")
                if ($contribMatch.Success) { $contribution = "site=$($contribMatch.Groups[1].Value) kindValue=$($contribMatch.Groups[2].Value) kindDamage=$($contribMatch.Groups[3].Value)" }
            }

            Write-Host " wave=$reached/20 kills=$totalKills esc=$totalEscapes budget=$endBudget integ=$endIntegrity tc=$tc victory=$hasVictory errs=$($errs.Count)"
            Write-Host "    contribution: $contribution"

            $allResults += [ordered]@{
                tag=$tag; level=$cfg.Level; strategy=$cfg.Strategy
                reachedWave=$reached; kills=$totalKills; escapes=$totalEscapes
                endBudget=$endBudget; endIntegrity=$endIntegrity
                tacticalScore=$tc; victory=$hasVictory; errors=$errs.Count
                contribution=$contribution
            }
        }
    } else {
        Write-Host " FAILED (no summary)"
    }
}

Write-Host ""
Write-Host "=== BALANCE VERIFICATION SUMMARY ==="
$allResults | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $OutputDir "balance_results.json") -Encoding UTF8
Write-Host "Results saved to $OutputDir/balance_results.json"
