<#
.SYNOPSIS
Creates a local ProtocolLab public report bundle.

.DESCRIPTION
Runs a local ProtocolLab benchmark that stages the schema-validated public
report bundle under .artifacts/publication/{runId}. The staged bundle contains
the same files expected under public/runs/{runId}/ in R2, including
evidence-report-v1.json, artifacts-index.json, publication-manifest.json,
report-index-entry.json, and report-index.json.

This script does not upload to R2 and does not write D1 metadata. Use
Publish-ProtocolLabReport.ps1 for the Cloudflare handoff after the local bundle
has been inspected.

When -RunRoot is supplied, the script skips the benchmark step and only stages
a public bundle from that completed run.

.PARAMETER RunId
Run identifier to create or publish. Defaults to a timestamped local-public-report
ID when -RunRoot is not supplied.

.PARAMETER RunRoot
Existing completed run root under .artifacts/runs/{runId}. Supplying this skips
the local benchmark step.

.PARAMETER RunOutputRoot
Root directory for completed run artifacts. Defaults to .artifacts/runs.

.PARAMETER BundleRoot
Output directory for the staged publication bundle. Defaults to
.artifacts/publication/{runId}.

.PARAMETER Implementations
Comma-separated implementation IDs for the local benchmark.

.PARAMETER Scenarios
Comma-separated scenario IDs for the local benchmark.

.PARAMETER Protocol
Protocol ID for the local benchmark.

.PARAMETER LoadTool
Load tool ID for the local benchmark.

.PARAMETER LoadToolMode
Load tool mode for the local benchmark.

.PARAMETER LoadProfile
Optional load profile ID for the local benchmark.

.PARAMETER RequirePublishable
Do not pass --allow-diagnostic-publication to publish-report. By default local
diagnostic bundles are allowed and remain explicitly labeled diagnostic-only.

.PARAMETER DryRun
Validate the publication bundle plan without writing the bundle files.
#>
[CmdletBinding()]
param(
    [string]$RunId,
    [string]$RunRoot,
    [string]$RunOutputRoot = ".artifacts\runs",
    [string]$BundleRoot,
    [string]$Implementations = "kestrel-http3",
    [string]$Scenarios = "http.core.plaintext",
    [string]$Protocol = "h3",
    [string]$LoadTool = "managed-httpclient-h3-load",
    [ValidateSet("managed", "process", "docker")]
    [string]$LoadToolMode = "managed",
    [string]$LoadProfile = "smoke",
    [ValidateSet("process", "docker", "external")]
    [string]$TargetMode = "process",
    [ValidateSet("published-port", "shared-docker-network")]
    [string]$TargetNetworkMode = "published-port",
    [string]$TargetConfiguration = "Release",
    [int]$DurationSeconds = 3,
    [int]$WarmupSeconds = 0,
    [int]$Repetitions = 1,
    [int]$Connections = 4,
    [int]$StreamsPerConnection = 1,
    [string]$BaseUrl,
    [switch]$RequirePublishable,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$CliProject = Join-Path $RepoRoot "src\Incursa.ProtocolLab.Cli"
$shouldPublish = $true

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$BasePath
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Invoke-ProtocolLabCli {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Push-Location $RepoRoot
    try {
        Write-Host "dotnet run --project $CliProject -- $($Arguments -join ' ')"
        & dotnet run --project $CliProject -- @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "protocol-lab CLI failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

if ([string]::IsNullOrWhiteSpace($RunRoot)) {
    if ([string]::IsNullOrWhiteSpace($RunId)) {
        $RunId = "local-public-report-$(Get-Date -Format 'yyyyMMddHHmmss')"
    }

    $runOutputRootFull = Resolve-AbsolutePath -Path $RunOutputRoot -BasePath $RepoRoot
    $RunRoot = Join-Path $runOutputRootFull $RunId
    if ([string]::IsNullOrWhiteSpace($BundleRoot)) {
        $BundleRoot = Join-Path (Join-Path $RepoRoot ".artifacts\publication") $RunId
    }
    else {
        $BundleRoot = Resolve-AbsolutePath -Path $BundleRoot -BasePath $RepoRoot
    }

    $shouldPublish = $DryRun.IsPresent -or $RequirePublishable.IsPresent

    $runArgs = @(
        "run",
        "--root", $RepoRoot,
        "--implementations", $Implementations,
        "--scenarios", $Scenarios,
        "--protocol", $Protocol,
        "--target-mode", $TargetMode,
        "--target-network-mode", $TargetNetworkMode,
        "--target-configuration", $TargetConfiguration,
        "--load-tool", $LoadTool,
        "--load-tool-mode", $LoadToolMode,
        "--duration", "$DurationSeconds",
        "--warmup", "$WarmupSeconds",
        "--repetitions", "$Repetitions",
        "--concurrency", "$Connections",
        "--streams-per-connection", "$StreamsPerConnection",
        "--run-id", $RunId,
        "--output", $runOutputRootFull,
        "--publication-output", $BundleRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($LoadProfile)) {
        $runArgs += @("--load-profile", $LoadProfile)
    }

    if (-not [string]::IsNullOrWhiteSpace($BaseUrl)) {
        $runArgs += @("--base-url", $BaseUrl)
    }

    if ($DryRun -or $RequirePublishable) {
        $runArgs += "--skip-publication-bundle"
    }

    Invoke-ProtocolLabCli -Arguments $runArgs
}
else {
    $RunRoot = Resolve-AbsolutePath -Path $RunRoot -BasePath $RepoRoot
    if ([string]::IsNullOrWhiteSpace($RunId)) {
        $RunId = Split-Path -Path $RunRoot -Leaf
    }
}

if (-not (Test-Path -LiteralPath $RunRoot -PathType Container)) {
    throw "Completed run root was not found: $RunRoot"
}

if ($shouldPublish) {
    if ([string]::IsNullOrWhiteSpace($BundleRoot)) {
        $BundleRoot = Join-Path (Join-Path $RepoRoot ".artifacts\publication") $RunId
    }
    else {
        $BundleRoot = Resolve-AbsolutePath -Path $BundleRoot -BasePath $RepoRoot
    }

    $publishArgs = @(
        "publish-report",
        "--root", $RepoRoot,
        "--run", $RunRoot,
        "--output", $BundleRoot,
        "--visibility", "public"
    )

    if (-not $RequirePublishable) {
        $publishArgs += "--allow-diagnostic-publication"
    }

    if ($DryRun) {
        $publishArgs += "--dry-run"
    }

    Invoke-ProtocolLabCli -Arguments $publishArgs
}

Write-Host ""
Write-Host "Run root: $RunRoot"
Write-Host "Bundle root: $BundleRoot"

if (-not $DryRun) {
    Write-Host "Local public report files:"
    Write-Host "  $(Join-Path $BundleRoot 'evidence-report-v1.json')"
    Write-Host "  $(Join-Path $BundleRoot 'artifacts-index.json')"
    Write-Host "  $(Join-Path $BundleRoot 'publication-manifest.json')"
    Write-Host "  $(Join-Path $BundleRoot 'report-index-entry.json')"
    Write-Host "  $(Join-Path $BundleRoot 'report-index.json')"
}
