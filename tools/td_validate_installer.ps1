param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,
    [string]$OutputRoot = "E:/TD/output/release/p1252/install_validation",
    [string]$ExpectedVersion = "0.12.5",
    [string]$ExpectedSignerThumbprint = "",
    [switch]$RequireTrustedSignature,
    [switch]$CleanMachine,
    [ValidateRange(100, 5000)]
    [int]$TechnicalIntegrity = 1000,
    [ValidateRange(60, 600)]
    [int]$SmokeTimeoutSeconds = 240,
    [ValidateRange(1, 20)]
    [float]$SmokeTimeScale = 16
)

$ErrorActionPreference = "Stop"
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$workspacePrefix = $projectRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$installerFull = [IO.Path]::GetFullPath($InstallerPath)
$outputFull = [IO.Path]::GetFullPath($OutputRoot)
foreach ($path in @($installerFull, $outputFull)) {
    if (-not $path.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Validation paths must stay inside the workspace: $path"
    }
}
if (-not (Test-Path -LiteralPath $installerFull -PathType Leaf)) {
    throw "Installer does not exist: $installerFull"
}

if (Test-Path -LiteralPath $outputFull) {
    $resolvedOutput = (Resolve-Path -LiteralPath $outputFull).Path
    if (-not $resolvedOutput.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase) -or
        $resolvedOutput -eq $projectRoot) {
        throw "Refusing to clean unsafe validation path: $resolvedOutput"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $outputFull -Force | Out-Null

$installRoot = Join-Path $outputFull "clean-install-root"
$installLog = Join-Path $outputFull "install.log"
$uninstallLog = Join-Path $outputFull "uninstall.log"
$smokeReportPath = Join-Path $outputFull "installed-smoke.json"
$playerLogPath = Join-Path $outputFull "installed-player.log"
$reportPath = Join-Path $outputFull "p1252_install_validation.json"
$prefsBackupPath = Join-Path $outputFull "playerprefs-backup.reg"
$playerPrefsKey = "HKCU\Software\Emberline\Emberline Defense"
$uninstallRegistryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{4FA8D424-E4F8-4283-87D6-A1CA52DAED3C}_is1"
$hadPlayerPrefs = $false
$playerPrefsRestored = $false

function Test-Signature {
    param([string]$Path)

    $signature = Get-AuthenticodeSignature -FilePath $Path
    $thumbprint = if ($null -ne $signature.SignerCertificate) {
        $signature.SignerCertificate.Thumbprint
    } else {
        ""
    }
    $present = -not [string]::IsNullOrWhiteSpace($thumbprint)
    $matches = [string]::IsNullOrWhiteSpace($ExpectedSignerThumbprint) -or
               $thumbprint -eq $ExpectedSignerThumbprint
    $accepted = $present -and $matches -and
                (-not $RequireTrustedSignature -or $signature.Status -eq "Valid")
    return [ordered]@{
        path = $Path
        status = $signature.Status.ToString()
        statusMessage = $signature.StatusMessage
        thumbprint = $thumbprint
        present = $present
        matchesExpected = $matches
        accepted = $accepted
    }
}

function Invoke-RegCommand {
    param([string[]]$Arguments)

    $process = Start-Process `
        -FilePath "$env:SystemRoot/System32/reg.exe" `
        -ArgumentList $Arguments `
        -PassThru `
        -Wait `
        -WindowStyle Hidden
    return $process.ExitCode
}

$report = [ordered]@{
    schemaVersion = "p1252-install-validation-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    computerName = $env:COMPUTERNAME
    osVersion = [Environment]::OSVersion.VersionString
    cleanMachineRequested = [bool]$CleanMachine
    isolationMode = if ($CleanMachine) { "clean_machine" } else { "clean_install_root_current_host" }
    installer = $installerFull
    installRoot = $installRoot
    installerSignature = $null
    installedSignatures = @()
    installExitCode = $null
    installFilesPresent = $false
    smokeExitCode = $null
    smokePassed = $false
    runtimeIdentityPassed = $false
    uninstallExitCode = $null
    uninstallRemovedFiles = $false
    uninstallRemovedRegistry = $false
    playerPrefsRestored = $false
    passed = $false
    error = ""
}

try {
    $report.installerSignature = Test-Signature -Path $installerFull
    if (-not $report.installerSignature.accepted) {
        throw "Installer signature was not accepted."
    }

    $hadPlayerPrefs = (Invoke-RegCommand -Arguments @("query", $playerPrefsKey)) -eq 0
    if ($hadPlayerPrefs) {
        $exportExitCode = Invoke-RegCommand -Arguments @(
            "export",
            $playerPrefsKey,
            $prefsBackupPath,
            "/y"
        )
        if ($exportExitCode -ne 0 -or -not (Test-Path -LiteralPath $prefsBackupPath)) {
            throw "Could not back up Emberline PlayerPrefs."
        }
    }

    $installArguments = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/SP-",
        "/CURRENTUSER",
        "/DIR=$installRoot",
        "/LOG=$installLog"
    )
    $installer = Start-Process -FilePath $installerFull -ArgumentList $installArguments -PassThru -WindowStyle Hidden
    if (-not $installer.WaitForExit(300000)) {
        Stop-Process -Id $installer.Id -Force
        throw "Installer timed out."
    }
    $report.installExitCode = $installer.ExitCode
    if ($installer.ExitCode -ne 0) {
        throw "Installer exited with code $($installer.ExitCode)."
    }

    $installedExe = Join-Path $installRoot "EmberlineDefense.exe"
    $installedData = Join-Path $installRoot "EmberlineDefense_Data"
    $essentialBinaries = @(
        $installedExe,
        (Join-Path $installRoot "GameAssembly.dll"),
        (Join-Path $installRoot "UnityPlayer.dll"),
        (Join-Path $installRoot "UnityCrashHandler64.exe"),
        (Join-Path $installRoot "baselib.dll")
    )
    $report.installFilesPresent =
        (Test-Path -LiteralPath $installedData -PathType Container) -and
        @($essentialBinaries | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }).Count -eq 0
    if (-not $report.installFilesPresent) {
        throw "Installed player layout is incomplete."
    }

    $report.installedSignatures = @($essentialBinaries | ForEach-Object { Test-Signature -Path $_ })
    if (@($report.installedSignatures | Where-Object { -not $_.accepted }).Count -gt 0) {
        throw "One or more installed binaries failed signature validation."
    }

    $smokeArguments = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 " +
                      "-logFile `"$playerLogPath`" --td-smoke-test " +
                      "--td-smoke-report `"$smokeReportPath`" " +
                      "--td-smoke-time-scale $SmokeTimeScale --td-smoke-timeout $SmokeTimeoutSeconds " +
                      "--td-smoke-technical-integrity $TechnicalIntegrity"
    $player = Start-Process -FilePath $installedExe -ArgumentList $smokeArguments -PassThru -WindowStyle Hidden
    if (-not $player.WaitForExit(($SmokeTimeoutSeconds + 60) * 1000)) {
        Stop-Process -Id $player.Id -Force
        throw "Installed player smoke timed out."
    }
    $report.smokeExitCode = $player.ExitCode
    if (-not (Test-Path -LiteralPath $smokeReportPath)) {
        throw "Installed player did not write a smoke report."
    }

    $smoke = Get-Content -LiteralPath $smokeReportPath -Raw | ConvertFrom-Json
    $report.smokePassed = $player.ExitCode -eq 0 -and [bool]$smoke.passed
    $report.runtimeIdentityPassed =
        $smoke.productName -eq "Emberline Defense" -and
        $smoke.version -eq $ExpectedVersion -and
        $smoke.sceneName -eq "EmberlineBootstrap" -and
        $smoke.scriptingBackend -eq "IL2CPP" -and
        [bool]$smoke.technicalAssistApplied
    if (-not $report.smokePassed -or -not $report.runtimeIdentityPassed) {
        throw "Installed IL2CPP smoke or runtime identity failed."
    }

    $uninstallerPath = Get-ChildItem -LiteralPath $installRoot -Filter "unins*.exe" -File |
        Select-Object -First 1 -ExpandProperty FullName
    if ([string]::IsNullOrWhiteSpace($uninstallerPath)) {
        throw "Inno Setup uninstaller was not installed."
    }
    $uninstallerSignature = Test-Signature -Path $uninstallerPath
    $report.installedSignatures += $uninstallerSignature
    if (-not $uninstallerSignature.accepted) {
        throw "Uninstaller signature was not accepted."
    }

    $uninstaller = Start-Process -FilePath $uninstallerPath -ArgumentList @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/LOG=$uninstallLog"
    ) -PassThru -WindowStyle Hidden
    if (-not $uninstaller.WaitForExit(300000)) {
        Stop-Process -Id $uninstaller.Id -Force
        throw "Uninstaller timed out."
    }
    $report.uninstallExitCode = $uninstaller.ExitCode
    Start-Sleep -Seconds 2
    $report.uninstallRemovedFiles = -not (Test-Path -LiteralPath $installRoot)
    $report.uninstallRemovedRegistry = -not (Test-Path -LiteralPath $uninstallRegistryPath)
    if ($uninstaller.ExitCode -ne 0 -or
        -not $report.uninstallRemovedFiles -or
        -not $report.uninstallRemovedRegistry) {
        throw "Uninstall cleanup validation failed."
    }

    $report.passed = $true
} catch {
    $report.error = $_.Exception.ToString()
} finally {
    $deleteExitCode = Invoke-RegCommand -Arguments @("delete", $playerPrefsKey, "/f")
    if ($hadPlayerPrefs) {
        $playerPrefsRestored =
            (Invoke-RegCommand -Arguments @("import", $prefsBackupPath)) -eq 0
    } else {
        $playerPrefsRestored = $deleteExitCode -eq 0 -or $deleteExitCode -eq 1
    }
    $report.playerPrefsRestored = $playerPrefsRestored
    if (-not $playerPrefsRestored) {
        $report.passed = $false
        if ([string]::IsNullOrWhiteSpace($report.error)) {
            $report.error = "Emberline PlayerPrefs could not be restored after validation."
        }
    }
    $report | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $reportPath -Encoding utf8
}

$report | ConvertTo-Json -Depth 15
if (-not $report.passed) {
    throw "P12.5.2 installer validation failed. Inspect $reportPath"
}
