param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [int]$LevelIndex = 9,
    [int]$DurationSeconds = 45,
    [float]$TimeScale = 2,
    [string]$OutputDirectory = "E:/TD/output/playtest/visual_audit",
    [string]$OutputName = "current_l09_full_run",
    [string]$BuildPlan = "4,4:CinderMortar;7,7:FrostCoil;11,5:RailLancer;11,2:ArcWelder",
    [string]$UpgradePlan = "4,4:Damage,Damage;7,7:Utility,Utility;11,5:Damage,Damage;11,2:Utility,Utility",
    [int]$BonusBudget = 900,
    [string]$FfmpegPath = "C:/Users/gwdww/AppData/Local/Programs/Python/Python310/lib/site-packages/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe"
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $FfmpegPath)) {
    throw "ffmpeg not found at $FfmpegPath. Run: uv run --with imageio-ffmpeg python -c `"import imageio_ffmpeg; print(imageio_ffmpeg.get_ffmpeg_exe())`""
}

$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$videoPath = Join-Path $OutputDirectory "$OutputName.mp4"
$recorderLogPath = Join-Path $OutputDirectory "$OutputName`_ffmpeg.log"
$screenshotPath = (Join-Path $OutputDirectory "$OutputName`_final.png").Replace("\", "/")
$summaryPath = Join-Path $OutputDirectory "$OutputName`_summary.json"
$recordSeconds = $DurationSeconds + 15
$unityTitle = '"title=TD - Untitled - Windows, Mac, Linux - Unity 2022.3.12f1 <DX11>"'
$arguments = @(
    "-y",
    "-f", "gdigrab",
    "-framerate", "30",
    "-i", $unityTitle,
    "-t", $recordSeconds,
    "-vf", "scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2",
    "-c:v", "libx264",
    "-preset", "veryfast",
    "-crf", "20",
    "-pix_fmt", "yuv420p",
    ('"' + $videoPath + '"')
)

$recorderStartInfo = New-Object System.Diagnostics.ProcessStartInfo
$recorderStartInfo.FileName = $FfmpegPath
$recorderStartInfo.Arguments = $arguments -join " "
$recorderStartInfo.UseShellExecute = $false
$recorderStartInfo.RedirectStandardError = $true
$recorderStartInfo.CreateNoWindow = $true
$recorderStartInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$recorder = New-Object System.Diagnostics.Process
$recorder.StartInfo = $recorderStartInfo
if (-not $recorder.Start()) {
    throw "Visual recorder failed to start at $FfmpegPath."
}
$recorderErrorTask = $recorder.StandardError.ReadToEndAsync()
Start-Sleep -Seconds 1
$playtestError = ""
try {
    try {
        & $runner `
            -McpUrl $McpUrl `
            -LevelIndex $LevelIndex `
            -DurationSeconds $DurationSeconds `
            -TimeScale $TimeScale `
            -ViewportWidth 1280 `
            -ViewportHeight 720 `
            -BuildPlan $BuildPlan `
            -UpgradePlan $UpgradePlan `
            -BonusBudget $BonusBudget `
            -WaitFullDuration `
            -PreserveCampaignProgress `
            -ScreenshotPath $screenshotPath `
            -SummaryPath $summaryPath | Out-Null
    } catch {
        $playtestError = $_.Exception.Message
    }
} finally {
    $recorderCompleted = $recorder.WaitForExit(($recordSeconds + 10) * 1000)
}

if (-not $recorderCompleted) {
    if (-not $recorder.HasExited) {
        $recorder.Kill()
        $recorder.WaitForExit()
    }
    throw "Visual recorder did not exit within $($recordSeconds + 10) seconds. Inspect $recorderLogPath."
}

$recorder.WaitForExit()
$recorderExitCode = $recorder.ExitCode
$recorderLog = $recorderErrorTask.Result
[IO.File]::WriteAllText([IO.Path]::GetFullPath($recorderLogPath), $recorderLog)
if ($recorderExitCode -ne 0) {
    $tail = ($recorderLog -split "`r?`n" | Select-Object -Last 12) -join "`n"
    throw "Visual recorder exited with code $recorderExitCode. Inspect $recorderLogPath.`n$tail"
}

if (-not (Test-Path -LiteralPath $videoPath) -or (Get-Item -LiteralPath $videoPath).Length -lt 10240) {
    throw "Visual recording is missing or too small at $videoPath. Inspect $recorderLogPath."
}

$decodeOutput = & $FfmpegPath -v error -i $videoPath -f null - 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Visual recording failed decode validation at $videoPath.`n$($decodeOutput -join "`n")"
}

if (-not [string]::IsNullOrWhiteSpace($playtestError)) {
    throw "Visual playtest failed after recording: $playtestError"
}

[ordered]@{
    videoPath = [IO.Path]::GetFullPath($videoPath)
    screenshotPath = [IO.Path]::GetFullPath($screenshotPath)
    summaryPath = [IO.Path]::GetFullPath($summaryPath)
    levelIndex = $LevelIndex
    durationSeconds = $DurationSeconds
    timeScale = $TimeScale
    videoBytes = (Get-Item -LiteralPath $videoPath).Length
    recorderExitCode = $recorderExitCode
    recorderLogPath = [IO.Path]::GetFullPath($recorderLogPath)
    decodeValid = $true
    playtestError = ""
} | ConvertTo-Json -Depth 5
