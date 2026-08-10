<#
.SYNOPSIS
  Capture teaching-phase screenshots: mission board, formation, prep, combat, upgrade, result.
.DESCRIPTION
  Uses td_mcp_playtest.ps1 with presentation-prep flags to freeze specific UI states,
  then captures a screenshot for the teaching guide.
#>
param(
    [string]$OutputDir = "E:/TD/output/playtest/teaching_shots"
)

$ErrorActionPreference = "Continue"
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }

function Capture-Phase {
    param(
        [string]$Name,
        [string[]]$ExtraArgs,
        [int]$Level = 9,
        [int]$Duration = 8
    )
    $screenshot = (Join-Path $OutputDir "${Name}.png").Replace("\", "/")
    $summary = (Join-Path $OutputDir "${Name}_summary.json").Replace("\", "/")

    Write-Host "  Capturing $Name ..." -NoNewline

    $childArgs = @(
        "-ExecutionPolicy","Bypass","-File","tools/td_mcp_playtest.ps1",
        "-LevelIndex",$Level,
        "-DurationSeconds",$Duration,
        "-TimeScale", "0",
        "-SummaryPath",$summary,
        "-ScreenshotPath",$screenshot,
        "-AllowConsoleIssues",
        "-SkipAutoBuild",
        "-SkipStartWave"
    )
    $childArgs += $ExtraArgs

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "powershell.exe"
    $psi.Arguments = ($childArgs -join ' ')
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $proc = [System.Diagnostics.Process]::Start($psi)
    $proc.WaitForExit()

    $exists = Test-Path $screenshot
    $size = if ($exists) { (Get-Item $screenshot).Length } else { 0 }
    Write-Host " ${size} bytes $(if ($exists) { 'OK' } else { 'MISSING' })"
    return $exists
}

Write-Host "=== Teaching Phase Screenshots ==="

# Phase 1: Mission Board (level select screen)
Capture-Phase -Name "01_mission_board" -Level 9 -Duration 8 -ExtraArgs @("-KeepMissionBoardOpen", "-PrepareP84ChapterBoard")

# Phase 2: Formation / Loadout panel
Capture-Phase -Name "02_formation_loadout" -Level 9 -Duration 8 -ExtraArgs @("-KeepFormationOpen", "-KeepMissionBoardOpen")

# Phase 3: Campaign Profile (save slots / meta)
Capture-Phase -Name "03_campaign_profile" -Level 9 -Duration 8 -ExtraArgs @("-KeepCampaignProfileOpen", "-KeepMissionBoardOpen")

# Phase 4: Settings panel
Capture-Phase -Name "04_settings" -Level 9 -Duration 8 -ExtraArgs @("-PrepareP123Settings")

# Phase 5: Prep phase (board visible, pre-wave)
Capture-Phase -Name "05_prep_phase" -Level 1 -Duration 10 -ExtraArgs @("-SkipStartWave")

# Phase 6: Combat early (wave 1-3, short run)
Capture-Phase -Name "06_combat_early" -Level 1 -Duration 15 -ExtraArgs @()

# Phase 7: Combat mid (L09, medium run)
$screenshot7 = (Join-Path $OutputDir "07_combat_mid.png").Replace("\", "/")
Write-Host "  Capturing 07_combat_mid ..." -NoNewline
$childArgs7 = @(
    "-ExecutionPolicy","Bypass","-File","tools/td_mcp_playtest.ps1",
    "-LevelIndex","9",
    "-P124AutoplayStrategy","focused_fire",
    "-P124MaxRealSeconds","40",
    "-TimeScale","8",
    "-ScreenshotPath",$screenshot7,
    "-SummaryPath",(Join-Path $OutputDir "07_combat_mid_summary.json").Replace("\","/"),
    "-AllowConsoleIssues"
)
$psi7 = New-Object System.Diagnostics.ProcessStartInfo
$psi7.FileName = "powershell.exe"
$psi7.Arguments = ($childArgs7 -join ' ')
$psi7.UseShellExecute = $false; $psi7.RedirectStandardOutput = $true; $psi7.RedirectStandardError = $true; $psi7.CreateNoWindow = $true
$proc7 = [System.Diagnostics.Process]::Start($psi7); $proc7.WaitForExit()
$exists7 = Test-Path $screenshot7
Write-Host " $(if ($exists7) {(Get-Item $screenshot7).Length} else {0}) bytes $(if ($exists7) {'OK'} else {'MISSING'})"

# Phase 8: Boss combat (L20)
$screenshot8 = (Join-Path $OutputDir "08_boss_combat.png").Replace("\", "/")
Write-Host "  Capturing 08_boss_combat ..." -NoNewline
$childArgs8 = @(
    "-ExecutionPolicy","Bypass","-File","tools/td_mcp_playtest.ps1",
    "-LevelIndex","20",
    "-P124AutoplayStrategy","control_lattice",
    "-P124MaxRealSeconds","50",
    "-TimeScale","8",
    "-ScreenshotPath",$screenshot8,
    "-SummaryPath",(Join-Path $OutputDir "08_boss_combat_summary.json").Replace("\","/"),
    "-AllowConsoleIssues"
)
$psi8 = New-Object System.Diagnostics.ProcessStartInfo
$psi8.FileName = "powershell.exe"; $psi8.Arguments = ($childArgs8 -join ' ')
$psi8.UseShellExecute = $false; $psi8.RedirectStandardOutput = $true; $psi8.RedirectStandardError = $true; $psi8.CreateNoWindow = $true
$proc8 = [System.Diagnostics.Process]::Start($psi8); $proc8.WaitForExit()
$exists8 = Test-Path $screenshot8
Write-Host " $(if ($exists8) {(Get-Item $screenshot8).Length} else {0}) bytes $(if ($exists8) {'OK'} else {'MISSING'})"

Write-Host ""
Write-Host "=== Teaching shots complete ==="
$shots = Get-ChildItem (Join-Path $OutputDir "*.png") -ErrorAction SilentlyContinue
Write-Host "Total screenshots: $($shots.Count)"
foreach ($s in $shots) { Write-Host "  $($s.Name): $([math]::Round($s.Length/1MB, 1))MB" }
