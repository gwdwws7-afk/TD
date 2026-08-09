param(
    [string]$OutputRoot = "E:/TD/output/release/p1252",
    [string]$Version = "0.12.5",
    [ValidateRange(1, 2147483647)]
    [int]$BuildNumber = 2,
    [ValidateSet("Production", "Test")]
    [string]$SigningMode = "Production",
    [string]$CertificateThumbprint = "",
    [string]$PfxPath = "",
    [string]$PfxPasswordEnvironmentVariable = "EMBERLINE_SIGNING_PFX_PASSWORD",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [switch]$SkipBuild,
    [switch]$SkipMonoParity,
    [switch]$SkipInstallValidation,
    [ValidateRange(100, 5000)]
    [int]$TechnicalIntegrity = 1000,
    [ValidateRange(60, 600)]
    [int]$SmokeTimeoutSeconds = 240,
    [ValidateRange(1, 20)]
    [float]$SmokeTimeScale = 16
)

$ErrorActionPreference = "Stop"
$startedUtc = [DateTime]::UtcNow
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$workspacePrefix = $projectRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$outputFull = [IO.Path]::GetFullPath($OutputRoot)
if (-not $outputFull.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must stay inside the workspace: $outputFull"
}
if (Test-Path -LiteralPath $outputFull) {
    $resolvedOutput = (Resolve-Path -LiteralPath $outputFull).Path
    if (-not $resolvedOutput.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase) -or
        $resolvedOutput -eq $projectRoot) {
        throw "Refusing to clean unsafe release path: $resolvedOutput"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $outputFull -Force | Out-Null

$buildScript = Join-Path $PSScriptRoot "td_build_windows.ps1"
$validatorScript = Join-Path $PSScriptRoot "td_validate_installer.ps1"
$sandboxPreparerScript = Join-Path $PSScriptRoot "td_prepare_sandbox_validation.ps1"
$installerScript = Join-Path $PSScriptRoot "installer/EmberlineDefense.iss"
$buildsRoot = Join-Path $projectRoot "output/builds"
$monoRoot = Join-Path $buildsRoot "p1252_mono"
$il2cppRoot = Join-Path $buildsRoot "p1252_il2cpp"
$stageRoot = Join-Path $outputFull "stage"
$installerRoot = Join-Path $outputFull "installer"
$validationRoot = Join-Path $outputFull "install_validation"
$sandboxBundleRoot = Join-Path $outputFull "sandbox_validation_bundle"
$manifestPath = Join-Path $outputFull "p1252_file_manifest.json"
$auditPath = Join-Path $outputFull "p1252_release_audit.json"
$setupIconPath = Join-Path $outputFull "emberline_setup.ico"
$installerBaseName = "EmberlineDefense-$Version-build$BuildNumber-win-x64-setup"
$installerPath = Join-Path $installerRoot "$installerBaseName.exe"

function Get-TreeBytes {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return 0L
    }
    return [long]((Get-ChildItem -LiteralPath $Path -Recurse -File |
        Measure-Object -Property Length -Sum).Sum)
}

function Get-SignToolPath {
    $candidates = Get-ChildItem "C:/Program Files (x86)/Windows Kits/10/bin" `
        -Filter "signtool.exe" -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
        Sort-Object FullName -Descending
    if (-not $candidates) {
        throw "Windows SDK SignTool was not found."
    }
    return $candidates[0].FullName
}

function Get-InnoCompilerPath {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs/Inno Setup 6/ISCC.exe"),
        "C:/Program Files (x86)/Inno Setup 6/ISCC.exe"
    )
    $path = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($path)) {
        throw "Inno Setup 6 compiler was not found."
    }
    return $path
}

function Resolve-SigningCertificate {
    if ($SigningMode -eq "Test") {
        $subject = "CN=Emberline P12.5.2 Test Signing"
        $certificate = Get-ChildItem Cert:/CurrentUser/My -CodeSigningCert |
            Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date).AddDays(7) -and $_.HasPrivateKey } |
            Sort-Object NotAfter -Descending |
            Select-Object -First 1
        if ($null -eq $certificate) {
            $certificate = New-SelfSignedCertificate `
                -Type CodeSigningCert `
                -Subject $subject `
                -CertStoreLocation "Cert:/CurrentUser/My" `
                -HashAlgorithm SHA256 `
                -NotAfter (Get-Date).AddYears(1)
        }
        return $certificate
    }

    if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
        $pfxFull = [IO.Path]::GetFullPath($PfxPath)
        if (-not (Test-Path -LiteralPath $pfxFull -PathType Leaf)) {
            throw "Production PFX does not exist: $pfxFull"
        }
        $rawPassword = [Environment]::GetEnvironmentVariable($PfxPasswordEnvironmentVariable)
        if ([string]::IsNullOrWhiteSpace($rawPassword)) {
            throw "Production PFX password environment variable is missing: $PfxPasswordEnvironmentVariable"
        }
        $securePassword = ConvertTo-SecureString $rawPassword -AsPlainText -Force
        $imported = Import-PfxCertificate `
            -FilePath $pfxFull `
            -CertStoreLocation "Cert:/CurrentUser/My" `
            -Password $securePassword
        if ($null -eq $imported) {
            throw "Production PFX could not be imported."
        }
        $script:CertificateThumbprint = $imported.Thumbprint
    }

    if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
        throw "Production signing requires CertificateThumbprint or PfxPath."
    }
    $normalizedThumbprint = $CertificateThumbprint.Replace(" ", "").ToUpperInvariant()
    $certificate = Get-ChildItem Cert:/CurrentUser/My -CodeSigningCert |
        Where-Object { $_.Thumbprint -eq $normalizedThumbprint } |
        Select-Object -First 1
    if ($null -eq $certificate -or -not $certificate.HasPrivateKey) {
        throw "Production code-signing certificate is missing or has no private key: $normalizedThumbprint"
    }
    if ($certificate.NotAfter -le (Get-Date)) {
        throw "Production code-signing certificate has expired: $normalizedThumbprint"
    }
    return $certificate
}

function Invoke-CodeSign {
    param(
        [string]$Path,
        [string]$SignTool,
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate
    )

    $arguments = @(
        "sign",
        "/fd", "SHA256",
        "/sha1", $Certificate.Thumbprint
    )
    if ($SigningMode -eq "Production") {
        $arguments += @("/tr", $TimestampUrl, "/td", "SHA256")
    }
    $arguments += $Path
    $output = & $SignTool @arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "SignTool failed for $Path`n$($output -join [Environment]::NewLine)"
    }
}

function Get-SignatureEvidence {
    param(
        [string]$Path,
        [string]$ExpectedThumbprint
    )

    $signature = Get-AuthenticodeSignature -FilePath $Path
    $thumbprint = if ($null -ne $signature.SignerCertificate) {
        $signature.SignerCertificate.Thumbprint
    } else {
        ""
    }
    return [ordered]@{
        path = $Path
        status = $signature.Status.ToString()
        statusMessage = $signature.StatusMessage
        thumbprint = $thumbprint
        present = -not [string]::IsNullOrWhiteSpace($thumbprint)
        matchesExpected = $thumbprint -eq $ExpectedThumbprint
        trusted = $signature.Status -eq "Valid"
    }
}

function Read-BuildArtifacts {
    param([string]$Root)

    return [ordered]@{
        build = Get-Content (Join-Path $Root "build-result.json") -Raw | ConvertFrom-Json
        smoke = Get-Content (Join-Path $Root "standalone-smoke.json") -Raw | ConvertFrom-Json
        audit = Get-Content (Join-Path $Root "p1251_build_audit.json") -Raw | ConvertFrom-Json
    }
}

if (-not $SkipBuild) {
    if (-not $SkipMonoParity) {
        & $buildScript `
            -OutputRoot $monoRoot `
            -Version $Version `
            -BuildNumber $BuildNumber `
            -Backend Mono `
            -ForceBatchMode `
            -CleanOutput `
            -SmokeTimeScale $SmokeTimeScale `
            -SmokeTimeoutSeconds $SmokeTimeoutSeconds `
            -SmokeTechnicalIntegrity $TechnicalIntegrity `
            -BuildTimeoutSeconds 1800
    }

    & $buildScript `
        -OutputRoot $il2cppRoot `
        -Version $Version `
        -BuildNumber $BuildNumber `
        -Backend IL2CPP `
        -ForceBatchMode `
        -CleanOutput `
        -SmokeTimeScale $SmokeTimeScale `
        -SmokeTimeoutSeconds $SmokeTimeoutSeconds `
        -SmokeTechnicalIntegrity $TechnicalIntegrity `
        -BuildTimeoutSeconds 1800
}

$il2cppArtifacts = Read-BuildArtifacts -Root $il2cppRoot
$monoArtifacts = if ($SkipMonoParity) { $null } else { Read-BuildArtifacts -Root $monoRoot }
$il2cppLayoutPass =
    $il2cppArtifacts.build.backend -eq "IL2CPP" -and
    $il2cppArtifacts.smoke.scriptingBackend -eq "IL2CPP" -and
    (Test-Path -LiteralPath (Join-Path $il2cppRoot "GameAssembly.dll")) -and
    -not (Test-Path -LiteralPath (Join-Path $il2cppRoot "MonoBleedingEdge"))

$parity = [ordered]@{
    skipped = [bool]$SkipMonoParity
    monoPassed = if ($null -ne $monoArtifacts) { [bool]$monoArtifacts.audit.hardPass } else { $true }
    il2cppPassed = [bool]$il2cppArtifacts.audit.hardPass
    identityMatched = if ($null -ne $monoArtifacts) {
        $monoArtifacts.smoke.productName -eq $il2cppArtifacts.smoke.productName -and
        $monoArtifacts.smoke.version -eq $il2cppArtifacts.smoke.version -and
        $monoArtifacts.smoke.sceneName -eq $il2cppArtifacts.smoke.sceneName
    } else { $true }
    missionMatched = if ($null -ne $monoArtifacts) {
        $monoArtifacts.smoke.run.levelId -eq $il2cppArtifacts.smoke.run.levelId -and
        $monoArtifacts.smoke.run.difficultyId -eq $il2cppArtifacts.smoke.run.difficultyId -and
        $monoArtifacts.smoke.run.strategyId -eq $il2cppArtifacts.smoke.run.strategyId -and
        [bool]$monoArtifacts.smoke.run.completed -and
        [bool]$il2cppArtifacts.smoke.run.completed -and
        [bool]$monoArtifacts.smoke.run.victory -and
        [bool]$il2cppArtifacts.smoke.run.victory -and
        [bool]$monoArtifacts.smoke.run.economyDecisionValue -and
        [bool]$il2cppArtifacts.smoke.run.economyDecisionValue -and
        $monoArtifacts.smoke.run.wavesCleared -eq $monoArtifacts.smoke.run.waveCount -and
        $il2cppArtifacts.smoke.run.wavesCleared -eq $il2cppArtifacts.smoke.run.waveCount
    } else { $true }
    scoreDelta = if ($null -ne $monoArtifacts) {
        [Math]::Abs([int]$monoArtifacts.smoke.run.totalScore - [int]$il2cppArtifacts.smoke.run.totalScore)
    } else { 0 }
    passed = $false
}
$parity.passed =
    $parity.monoPassed -and $parity.il2cppPassed -and
    $parity.identityMatched -and $parity.missionMatched -and
    $parity.scoreDelta -le 5 -and $il2cppLayoutPass
if (-not $parity.passed) {
    throw "Mono/IL2CPP parity gate failed."
}

New-Item -ItemType Directory -Path $stageRoot,$installerRoot -Force | Out-Null
$requiredFiles = @(
    "EmberlineDefense.exe",
    "GameAssembly.dll",
    "UnityPlayer.dll",
    "UnityCrashHandler64.exe",
    "baselib.dll"
)
foreach ($name in $requiredFiles) {
    $source = Join-Path $il2cppRoot $name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "IL2CPP release file is missing: $source"
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $stageRoot $name)
}
$dataSource = Join-Path $il2cppRoot "EmberlineDefense_Data"
if (-not (Test-Path -LiteralPath $dataSource -PathType Container)) {
    throw "IL2CPP player data is missing: $dataSource"
}
Copy-Item -LiteralPath $dataSource -Destination (Join-Path $stageRoot "EmberlineDefense_Data") -Recurse

$forbidden = @(Get-ChildItem -LiteralPath $stageRoot -Recurse -Force |
    Where-Object {
        $_.Name -match "DoNotShip|DontShipItWithYourGame" -or
        -not $_.PSIsContainer -and $_.Extension -match "^\.(pdb|mdb|dbg|map|log)$"
    } |
    Select-Object -ExpandProperty FullName)
if ($forbidden.Count -gt 0) {
    throw "Forbidden debug artifacts entered the release stage: $($forbidden -join ', ')"
}

$p1251BuildResult = Join-Path $projectRoot "output/builds/p1251_windows/build-result.json"
$baselineBytes = if (Test-Path -LiteralPath $p1251BuildResult) {
    [long](Get-Content $p1251BuildResult -Raw | ConvertFrom-Json).totalSizeBytes
} else {
    0L
}
$stageBytes = Get-TreeBytes -Path $stageRoot
$sizeReductionPct = if ($baselineBytes -gt 0) {
    [Math]::Round((1.0 - ($stageBytes / [double]$baselineBytes)) * 100.0, 1)
} else {
    0.0
}
$sizePass = $stageBytes -le 450MB -and ($baselineBytes -le 0 -or $sizeReductionPct -ge 20.0)
if (-not $sizePass) {
    throw "Release size gate failed: stage=$stageBytes baseline=$baselineBytes reduction=$sizeReductionPct%."
}

$signTool = Get-SignToolPath
$certificate = Resolve-SigningCertificate
$signingEvidence = @()
$signableFiles = @(Get-ChildItem -LiteralPath $stageRoot -Recurse -File |
    Where-Object { $_.Extension -match "^\.(exe|dll)$" })
foreach ($file in $signableFiles) {
    Invoke-CodeSign -Path $file.FullName -SignTool $signTool -Certificate $certificate
    $signingEvidence += Get-SignatureEvidence -Path $file.FullName -ExpectedThumbprint $certificate.Thumbprint
}
$binarySignaturesPresent =
    @($signingEvidence | Where-Object { -not $_.present -or -not $_.matchesExpected }).Count -eq 0
$binarySignaturesTrusted =
    @($signingEvidence | Where-Object { -not $_.trusted }).Count -eq 0
if (-not $binarySignaturesPresent) {
    throw "One or more staged binaries are not signed with the selected certificate."
}

Add-Type -AssemblyName System.Drawing
$appIcon = [System.Drawing.Icon]::ExtractAssociatedIcon((Join-Path $stageRoot "EmberlineDefense.exe"))
if ($null -eq $appIcon) {
    throw "Could not extract the application icon for the installer."
}
$iconStream = [IO.File]::Create($setupIconPath)
try {
    $appIcon.Save($iconStream)
} finally {
    $iconStream.Dispose()
    $appIcon.Dispose()
}

$innoCompiler = Get-InnoCompilerPath
$signCommand = '$q' + $signTool + '$q sign /fd SHA256 /sha1 ' + $certificate.Thumbprint
if ($SigningMode -eq "Production") {
    $signCommand += " /tr $TimestampUrl /td SHA256"
}
$signCommand += ' $q$f$q'
$innoArguments = @(
    "/DSourceDir=$stageRoot",
    "/DOutputDir=$installerRoot",
    "/DOutputBaseFilename=$installerBaseName",
    "/DAppVersion=$Version",
    "/DBuildNumber=$BuildNumber",
    "/DSetupIcon=$setupIconPath",
    "/DSignToolName=emberline",
    "/Semberline=$signCommand",
    $installerScript
)
$innoOutput = & $innoCompiler @innoArguments 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Inno Setup compilation failed.`n$($innoOutput -join [Environment]::NewLine)"
}

$installerSignature = Get-SignatureEvidence `
    -Path $installerPath `
    -ExpectedThumbprint $certificate.Thumbprint
if (-not $installerSignature.present -or -not $installerSignature.matchesExpected) {
    Invoke-CodeSign -Path $installerPath -SignTool $signTool -Certificate $certificate
    $installerSignature = Get-SignatureEvidence `
        -Path $installerPath `
        -ExpectedThumbprint $certificate.Thumbprint
}
if (-not $installerSignature.present -or -not $installerSignature.matchesExpected) {
    throw "Installer was not signed with the selected certificate."
}

$sandboxBundleArguments = @{
    InstallerPath = $installerPath
    OutputRoot = $sandboxBundleRoot
    ExpectedVersion = $Version
    ExpectedSignerThumbprint = $certificate.Thumbprint
    TechnicalIntegrity = $TechnicalIntegrity
    SmokeTimeoutSeconds = $SmokeTimeoutSeconds
    SmokeTimeScale = $SmokeTimeScale
}
if ($SigningMode -eq "Production") {
    $sandboxBundleArguments.RequireTrustedSignature = $true
}
& $sandboxPreparerScript @sandboxBundleArguments | Out-Null
$sandboxBundleAudit = Get-Content `
    (Join-Path $sandboxBundleRoot "p1252_sandbox_bundle.json") -Raw |
    ConvertFrom-Json

$manifestFiles = @(Get-ChildItem -LiteralPath $stageRoot -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        [ordered]@{
            path = $_.FullName.Substring($stageRoot.Length).TrimStart('\', '/').Replace('\', '/')
            bytes = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    })
$manifest = [ordered]@{
    schemaVersion = "p1252-file-manifest-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    product = "Emberline Defense"
    version = $Version
    buildNumber = $BuildNumber
    backend = "IL2CPP"
    totalBytes = $stageBytes
    fileCount = $manifestFiles.Count
    files = $manifestFiles
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8

$installValidation = $null
if (-not $SkipInstallValidation) {
    $validatorArguments = @{
        InstallerPath = $installerPath
        OutputRoot = $validationRoot
        ExpectedVersion = $Version
        ExpectedSignerThumbprint = $certificate.Thumbprint
        TechnicalIntegrity = $TechnicalIntegrity
        SmokeTimeoutSeconds = $SmokeTimeoutSeconds
        SmokeTimeScale = $SmokeTimeScale
    }
    if ($SigningMode -eq "Production") {
        $validatorArguments.RequireTrustedSignature = $true
    }
    & $validatorScript @validatorArguments | Out-Null
    $installValidation = Get-Content `
        (Join-Path $validationRoot "p1252_install_validation.json") -Raw |
        ConvertFrom-Json
}

$windowsSandboxAvailable = Test-Path "C:/Windows/System32/WindowsSandbox.exe"
$engineeringPass =
    $parity.passed -and $sizePass -and $forbidden.Count -eq 0 -and
    $binarySignaturesPresent -and
    $installerSignature.present -and $installerSignature.matchesExpected -and
    ($SkipInstallValidation -or [bool]$installValidation.passed)
$shippingSignaturePass =
    $SigningMode -eq "Production" -and
    $binarySignaturesTrusted -and
    [bool]$installerSignature.trusted
$cleanMachinePass =
    $null -ne $installValidation -and
    [bool]$installValidation.cleanMachineRequested -and
    [bool]$installValidation.passed
$releaseCandidatePass = $engineeringPass -and $shippingSignaturePass -and $cleanMachinePass
$externalBlockers = @()
if (-not $shippingSignaturePass) {
    $externalBlockers += "trusted_production_code_signing_certificate_required"
}
if (-not $cleanMachinePass) {
    $externalBlockers += if ($windowsSandboxAvailable) {
        "clean_machine_validation_not_executed"
    } else {
        "windows_sandbox_or_external_clean_vm_required"
    }
}

$audit = [ordered]@{
    schemaVersion = "p1252-release-audit-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $startedUtc).TotalSeconds, 2)
    product = "Emberline Defense"
    version = $Version
    buildNumber = $BuildNumber
    backend = "IL2CPP"
    signingMode = $SigningMode
    certificateSubject = $certificate.Subject
    certificateThumbprint = $certificate.Thumbprint
    certificateNotAfter = $certificate.NotAfter.ToUniversalTime().ToString("o")
    parity = $parity
    il2cppLayoutPassed = $il2cppLayoutPass
    baselineBytes = $baselineBytes
    stageBytes = $stageBytes
    sizeReductionPct = $sizeReductionPct
    sizePassed = $sizePass
    forbiddenArtifactCount = $forbidden.Count
    signedBinaryCount = $signingEvidence.Count
    binarySignaturesPresent = $binarySignaturesPresent
    binarySignaturesTrusted = $binarySignaturesTrusted
    installer = $installerPath
    installerBytes = (Get-Item -LiteralPath $installerPath).Length
    installerSha256 = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
    installerSignature = $installerSignature
    installValidationPassed = $SkipInstallValidation -or [bool]$installValidation.passed
    cleanMachinePassed = $cleanMachinePass
    windowsSandboxAvailable = $windowsSandboxAvailable
    sandboxValidationPrepared = $null -ne $sandboxBundleAudit
    engineeringPass = $engineeringPass
    shippingSignaturePass = $shippingSignaturePass
    releaseCandidatePass = $releaseCandidatePass
    externalBlockers = $externalBlockers
    artifacts = [ordered]@{
        stage = $stageRoot
        installer = $installerPath
        manifest = $manifestPath
        monoBuild = if ($SkipMonoParity) { $null } else { $monoRoot }
        il2cppBuild = $il2cppRoot
        installValidation = if ($SkipInstallValidation) { $null } else { $validationRoot }
        sandboxValidationBundle = $sandboxBundleRoot
    }
}
$audit | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $auditPath -Encoding utf8
$audit | ConvertTo-Json -Depth 20

if (-not $engineeringPass) {
    throw "P12.5.2 engineering release gate failed. Inspect $auditPath"
}
if ($SigningMode -eq "Production" -and -not $releaseCandidatePass) {
    throw "P12.5.2 production RC gate failed. Inspect $auditPath"
}
