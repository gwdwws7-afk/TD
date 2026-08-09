$ErrorActionPreference = "Stop"

function Resolve-TDCodexHome {
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
        return $env:CODEX_HOME
    }

    return Join-Path $HOME ".codex"
}

function Resolve-TDImageGenCli {
    $codexHome = Resolve-TDCodexHome
    $candidates = @(
        (Join-Path $codexHome "skills\imagegen\scripts\image_gen.py"),
        (Join-Path $codexHome "skills\.system\imagegen\scripts\image_gen.py")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw ("image_gen.py not found. Checked: " + ($candidates -join "; "))
}

function Import-TDOpenAIApiKey {
    param(
        [bool]$Required = $false
    )

    if ([string]::IsNullOrWhiteSpace($env:OPENAI_API_KEY)) {
        foreach ($scope in @("User", "Machine")) {
            $key = [Environment]::GetEnvironmentVariable("OPENAI_API_KEY", $scope)
            if (-not [string]::IsNullOrWhiteSpace($key)) {
                $env:OPENAI_API_KEY = $key
                break
            }
        }
    }

    if ([string]::IsNullOrWhiteSpace($env:OPENAI_API_KEY)) {
        $projectKeyPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")) "Assets\apikey.txt"
        if (Test-Path -LiteralPath $projectKeyPath) {
            $projectKey = (Get-Content -LiteralPath $projectKeyPath -Raw).Trim()
            if (-not [string]::IsNullOrWhiteSpace($projectKey)) {
                $env:OPENAI_API_KEY = $projectKey
            }
        }
    }

    if ($Required -and [string]::IsNullOrWhiteSpace($env:OPENAI_API_KEY)) {
        throw "OPENAI_API_KEY is missing in process, user, and machine environment."
    }
}
