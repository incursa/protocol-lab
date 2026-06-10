<#
.SYNOPSIS
Runs the ProtocolLab v1 local acceptance workflow.

.DESCRIPTION
Runs build, tests, check, Kestrel H3 validation, managed-lab H3 comparison,
Docker h2load external-reference H3 comparison, and counter-enabled Docker
h2load H3 comparison when dotnet-counters is available. Artifacts are written
under the configured output root. The script does not stage files, commit
changes, or alter benchmark semantics.

.PARAMETER RunIdPrefix
Prefix used for generated run IDs.

.PARAMETER SkipManaged
Skip the managed-lab H3 comparison stage.

.PARAMETER SkipExternal
Skip Docker h2load external-reference stages.

.PARAMETER SkipCounters
Skip the counter-enabled Docker h2load stage.

.PARAMETER DurationSeconds
Measured duration for benchmark stages.

.PARAMETER WarmupSeconds
Warmup duration for benchmark stages.

.PARAMETER Repetitions
Number of repetitions for benchmark stages.

.PARAMETER Connections
Requested connection count.

.PARAMETER StreamsPerConnection
Requested streams per connection.

.PARAMETER Output
Run artifact output root. Defaults to .artifacts\runs.

.PARAMETER TargetMode
Target execution mode. Defaults to process. Docker mode runs public reference
target paths.

.PARAMETER TargetNetworkMode
Docker target network mode. Defaults to published-port. shared-docker-network
uses a generated per-run Docker network for Docker h2load benchmark traffic.

.PARAMETER BuildTargetImages
Build local Docker target images before Docker target acceptance stages.

.PARAMETER IncludeCaddy
Include optional Caddy HTTP/3 Docker target validation and benchmark stages.
Caddy is Docker-only in Phase 3G and is not part of default v1 acceptance.

.PARAMETER IncludeNginx
Include optional nginx HTTP/3 Docker target validation and benchmark stages.
nginx is Docker-only in Phase 3H and is not part of default v1 acceptance.

.PARAMETER TargetCpus
Optional Docker CPU quota for target containers.

.PARAMETER TargetMemory
Optional Docker memory limit for target containers, for example 1g.

.PARAMETER LoadToolCpus
Optional Docker CPU quota for load-tool containers.

.PARAMETER LoadToolMemory
Optional Docker memory limit for load-tool containers, for example 1g.

.PARAMETER DockerCpusetCpus
Optional Docker cpuset for both target and load-tool containers.

.PARAMETER CaptureLoadToolMetrics
Capture Docker stats telemetry for Docker load-tool containers during h2load stages.

.PARAMETER LoadToolMetricsIntervalSeconds
Docker stats sampling interval for load-tool metrics capture.

.PARAMETER CaptureTargetContainerMetrics
Capture Docker stats telemetry for Docker target containers during benchmark stages.

.PARAMETER TargetContainerMetricsIntervalSeconds
Docker stats sampling interval for target container metrics capture.
#>
[CmdletBinding()]
param(
    [string]$RunIdPrefix = "local-v1-acceptance",
    [switch]$SkipManaged,
    [switch]$SkipExternal,
    [switch]$SkipCounters,
    [int]$DurationSeconds = 5,
    [int]$WarmupSeconds = 1,
    [int]$Repetitions = 1,
    [int]$Connections = 16,
    [int]$StreamsPerConnection = 10,
    [string]$Output = ".artifacts\runs",
    [ValidateSet("process", "docker", "external")]
    [string]$TargetMode = "process",
    [ValidateSet("published-port", "shared-docker-network")]
    [string]$TargetNetworkMode = "published-port",
    [switch]$BuildTargetImages,
    [switch]$IncludeCaddy,
    [switch]$IncludeNginx,
    [string]$TargetCpus,
    [string]$TargetMemory,
    [string]$LoadToolCpus,
    [string]$LoadToolMemory,
    [string]$DockerCpusetCpus,
    [switch]$CaptureLoadToolMetrics,
    [int]$LoadToolMetricsIntervalSeconds = 1,
    [switch]$CaptureTargetContainerMetrics,
    [int]$TargetContainerMetricsIntervalSeconds = 1
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$OutputRoot = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $RepoRoot $Output }
$IndexScript = Join-Path $RepoRoot "scripts\analysis\New-ProtocolLabRunIndex.ps1"
$H2LoadImage = "incursa/protocol-lab-h2load-http3:local"
$KestrelTargetImage = "incursa/protocol-lab-kestrel-bench-server:local"
$CaddyTargetImage = "incursa/protocol-lab-caddy-bench-server:local"
$NginxTargetImage = "incursa/protocol-lab-nginx-bench-server:local"
$KestrelTargetImageScript = Join-Path $RepoRoot "scripts\build\Build-KestrelBenchServerImage.ps1"
$CaddyTargetImageScript = Join-Path $RepoRoot "scripts\build\Build-CaddyBenchServerImage.ps1"
$NginxTargetImageScript = Join-Path $RepoRoot "scripts\build\Build-NginxBenchServerImage.ps1"
$RunIds = New-Object System.Collections.Generic.List[string]
$StageResults = New-Object System.Collections.Generic.List[object]

function Write-Stage {
    param([string]$Name)
    Write-Host ""
    Write-Host "==> $Name"
}

function Invoke-Stage {
    param(
        [string]$Name,
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$RunId
    )

    Write-Stage $Name
    Write-Host ("$FilePath " + ($Arguments -join " "))
    & $FilePath @Arguments
    $exitCode = $LASTEXITCODE
    $status = if ($exitCode -eq 0) { "passed" } else { "failed" }
    $artifactPath = if ($RunId) { Join-Path $OutputRoot $RunId } else { $null }
    $StageResults.Add([pscustomobject]@{
        Name = $Name
        Status = $status
        ExitCode = $exitCode
        ArtifactPath = $artifactPath
    }) | Out-Null

    if ($exitCode -ne 0) {
        throw "$Name failed with exit code $exitCode."
    }

    if ($RunId) {
        $RunIds.Add($RunId) | Out-Null
        Write-Host "Artifacts: $artifactPath"
        if (Test-Path -LiteralPath $IndexScript) {
            & powershell -NoProfile -ExecutionPolicy Bypass -File $IndexScript -RunsRoot $OutputRoot -RunId $RunId | Out-Host
        }
    }
}

function Test-DotnetCountersAvailable {
    $output = & dotnet tool run dotnet-counters -- --version 2>$null
    return $LASTEXITCODE -eq 0
}

Set-Location $RepoRoot
New-Item -ItemType Directory -Force $OutputRoot | Out-Null

$TargetArgs = if ($TargetMode -eq "process") { @() } else { @("--target-mode", $TargetMode, "--target-network-mode", $TargetNetworkMode) }
$ResourceArgs = @()
if ($TargetCpus) { $ResourceArgs += @("--target-cpus", $TargetCpus) }
if ($TargetMemory) { $ResourceArgs += @("--target-memory", $TargetMemory) }
if ($LoadToolCpus) { $ResourceArgs += @("--load-tool-cpus", $LoadToolCpus) }
if ($LoadToolMemory) { $ResourceArgs += @("--load-tool-memory", $LoadToolMemory) }
if ($DockerCpusetCpus) { $ResourceArgs += @("--docker-cpuset-cpus", $DockerCpusetCpus) }
$LoadToolMetricsArgs = @()
if ($CaptureLoadToolMetrics) {
    $LoadToolMetricsArgs += @("--capture-load-tool-metrics", "--load-tool-metrics-interval", "$LoadToolMetricsIntervalSeconds")
}
$TargetContainerMetricsArgs = @()
if ($CaptureTargetContainerMetrics) {
    $TargetContainerMetricsArgs += @("--capture-target-container-metrics", "--target-container-metrics-interval", "$TargetContainerMetricsIntervalSeconds")
}
if ($IncludeCaddy -and $TargetMode -ne "docker") {
    throw "Caddy is Docker-only in Phase 3G. Rerun with -TargetMode docker to use -IncludeCaddy."
}
if ($IncludeNginx -and $TargetMode -ne "docker") {
    throw "nginx is Docker-only in Phase 3H. Rerun with -TargetMode docker to use -IncludeNginx."
}
if ($TargetMode -eq "docker") {
    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if (-not $docker) {
        throw "Docker was not found on PATH. Install/start Docker Desktop for Docker target acceptance, or rerun with -TargetMode process."
    }

    if ($BuildTargetImages) {
        if (-not (Test-Path -LiteralPath $KestrelTargetImageScript)) {
            throw "Kestrel target image build script not found: $KestrelTargetImageScript"
        }

        Invoke-Stage -Name "Build Kestrel Docker target image" -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $KestrelTargetImageScript) -RunId $null

        if ($IncludeCaddy) {
            if (-not (Test-Path -LiteralPath $CaddyTargetImageScript)) {
                throw "Caddy target image build script not found: $CaddyTargetImageScript"
            }

            Invoke-Stage -Name "Build Caddy HTTP/3 Docker target image" -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $CaddyTargetImageScript) -RunId $null
        }

        if ($IncludeNginx) {
            if (-not (Test-Path -LiteralPath $NginxTargetImageScript)) {
                throw "nginx target image build script not found: $NginxTargetImageScript"
            }

            Invoke-Stage -Name "Build nginx HTTP/3 Docker target image" -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $NginxTargetImageScript) -RunId $null
        }
    }

    Write-Stage "Checking required Kestrel Docker target image"
    Write-Host "docker image inspect $KestrelTargetImage"
    & docker image inspect $KestrelTargetImage *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Kestrel Docker target image '$KestrelTargetImage' is required for Docker target acceptance. Run scripts\bootstrap\Initialize-ProtocolLab.ps1 -BuildTargetImages, or rerun acceptance with -TargetMode process."
    }

    if ($IncludeCaddy) {
        Write-Stage "Checking required Caddy Docker target image"
        Write-Host "docker image inspect $CaddyTargetImage"
        & docker image inspect $CaddyTargetImage *> $null
        if ($LASTEXITCODE -ne 0) {
            throw "Caddy Docker target image '$CaddyTargetImage' is required when -IncludeCaddy is supplied. Run scripts\build\Build-CaddyBenchServerImage.ps1, or rerun without -IncludeCaddy."
        }
    }

    if ($IncludeNginx) {
        Write-Stage "Checking required nginx Docker target image"
        Write-Host "docker image inspect $NginxTargetImage"
        & docker image inspect $NginxTargetImage *> $null
        if ($LASTEXITCODE -ne 0) {
            throw "nginx Docker target image '$NginxTargetImage' is required when -IncludeNginx is supplied. Run scripts\build\Build-NginxBenchServerImage.ps1, or rerun without -IncludeNginx."
        }
    }
}

Invoke-Stage -Name "Build" -FilePath "dotnet" -Arguments @("build", "Incursa.ProtocolLab.sln") -RunId $null
Invoke-Stage -Name "Test" -FilePath "dotnet" -Arguments @("test", "Incursa.ProtocolLab.sln") -RunId $null
Invoke-Stage -Name "Check" -FilePath "dotnet" -Arguments @("run", "--project", "src\Incursa.ProtocolLab.Cli", "--", "check") -RunId $null

$kestrelValidationRunId = "$RunIdPrefix-kestrel-h3-validation"
Invoke-Stage -Name "Kestrel H3 validation" -FilePath "dotnet" -Arguments (@(
    "run", "--project", "src\Incursa.ProtocolLab.Cli", "--",
    "validate",
    "--implementations", "kestrel-http3",
    "--scenarios", "http.core.plaintext,http.core.json",
    "--protocol", "h3"
) + $TargetArgs + $ResourceArgs + @(
    "--output", $OutputRoot,
    "--run-id", $kestrelValidationRunId
)) -RunId $kestrelValidationRunId

$implementationSet = "kestrel-http3"

if ($IncludeCaddy) {
    $caddyValidationRunId = "$RunIdPrefix-caddy-h3-validation"
    Invoke-Stage -Name "Caddy H3 validation" -FilePath "dotnet" -Arguments (@(
        "run", "--project", "src\Incursa.ProtocolLab.Cli", "--",
        "validate",
        "--implementations", "caddy-http3",
        "--scenarios", "http.core.plaintext,http.core.json",
        "--protocol", "h3"
    ) + $TargetArgs + $ResourceArgs + @(
        "--output", $OutputRoot,
        "--run-id", $caddyValidationRunId
    )) -RunId $caddyValidationRunId
    $implementationSet = "$implementationSet,caddy-http3"
}
else {
    $StageResults.Add([pscustomobject]@{ Name = "Caddy H3 validation"; Status = "skipped"; ExitCode = $null; ArtifactPath = "Optional; pass -IncludeCaddy with -TargetMode docker." }) | Out-Null
}

if ($IncludeNginx) {
    $nginxValidationRunId = "$RunIdPrefix-nginx-h3-validation"
    Invoke-Stage -Name "nginx H3 validation" -FilePath "dotnet" -Arguments (@(
        "run", "--project", "src\Incursa.ProtocolLab.Cli", "--",
        "validate",
        "--implementations", "nginx-http3",
        "--scenarios", "http.core.plaintext,http.core.json",
        "--protocol", "h3"
    ) + $TargetArgs + $ResourceArgs + @(
        "--output", $OutputRoot,
        "--run-id", $nginxValidationRunId
    )) -RunId $nginxValidationRunId
    $implementationSet = "$implementationSet,nginx-http3"
}
else {
    $StageResults.Add([pscustomobject]@{ Name = "nginx H3 validation"; Status = "skipped"; ExitCode = $null; ArtifactPath = "Optional; pass -IncludeNginx with -TargetMode docker." }) | Out-Null
}

$managedImplementationSet = $implementationSet
if ($IncludeNginx) {
    $managedImplementationSet = (($implementationSet -split ",") | Where-Object { $_ -ne "nginx-http3" }) -join ","
    $StageResults.Add([pscustomobject]@{
        Name = "nginx managed-lab H3 comparison"
        Status = "skipped"
        ExitCode = $null
        ArtifactPath = "nginx Phase 3H acceptance uses Docker h2load; managed-httpclient-h3-load is not part of the nginx acceptance gate."
    }) | Out-Null
}

if (-not $SkipManaged) {
    $managedRunId = "$RunIdPrefix-managed-h3"
    Invoke-Stage -Name "Managed-lab H3 comparison" -FilePath "dotnet" -Arguments (@(
        "run", "--project", "src\Incursa.ProtocolLab.Cli", "--",
        "run",
        "--implementations", $managedImplementationSet,
        "--scenarios", "http.core.plaintext,http.core.json",
        "--protocol", "h3",
        "--load-tool", "managed-httpclient-h3-load"
    ) + $TargetArgs + $ResourceArgs + @(
        "--connections", "$Connections",
        "--streams-per-connection", "$StreamsPerConnection",
        "--duration", "$DurationSeconds",
        "--warmup", "$WarmupSeconds",
        "--repetitions", "$Repetitions",
        "--output", $OutputRoot,
        "--run-id", $managedRunId
    )) -RunId $managedRunId
}
else {
    $StageResults.Add([pscustomobject]@{ Name = "Managed-lab H3 comparison"; Status = "skipped"; ExitCode = $null; ArtifactPath = $null }) | Out-Null
}

if (-not $SkipExternal) {
    Write-Stage "Checking required h2load HTTP/3 Docker image"
    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if (-not $docker) {
        throw "Docker was not found on PATH. Install/start Docker Desktop for external-reference acceptance, or rerun with -SkipExternal."
    }

    Write-Host "docker image inspect $H2LoadImage"
    & docker image inspect $H2LoadImage *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker h2load image '$H2LoadImage' is required for v1 acceptance. Run scripts\bootstrap\Initialize-ProtocolLab.ps1 -BuildH2LoadImage, or rerun acceptance with -SkipExternal."
    }

    $externalRunId = "$RunIdPrefix-h2load-h3"
    Invoke-Stage -Name "External-reference h2load H3 comparison" -FilePath "dotnet" -Arguments (@(
        "run", "--project", "src\Incursa.ProtocolLab.Cli", "--",
        "run",
        "--implementations", $implementationSet,
        "--scenarios", "http.core.plaintext,http.core.json",
        "--protocol", "h3",
        "--load-tool", "h2load",
        "--load-tool-mode", "docker"
    ) + $TargetArgs + $ResourceArgs + $LoadToolMetricsArgs + $TargetContainerMetricsArgs + @(
        "--connections", "$Connections",
        "--streams-per-connection", "$StreamsPerConnection",
        "--duration", "$DurationSeconds",
        "--warmup", "$WarmupSeconds",
        "--repetitions", "$Repetitions",
        "--output", $OutputRoot,
        "--run-id", $externalRunId
    )) -RunId $externalRunId
}
else {
    $StageResults.Add([pscustomobject]@{ Name = "External-reference h2load H3 comparison"; Status = "skipped"; ExitCode = $null; ArtifactPath = $null }) | Out-Null
}

if (-not $SkipCounters) {
    if (Test-DotnetCountersAvailable) {
        $countersRunId = "$RunIdPrefix-h2load-h3-counters"
        Invoke-Stage -Name "Counter-enabled h2load H3 comparison" -FilePath "dotnet" -Arguments (@(
            "run", "--project", "src\Incursa.ProtocolLab.Cli", "--",
            "run",
            "--implementations", $implementationSet,
            "--scenarios", "http.core.plaintext,http.core.json",
            "--protocol", "h3",
        "--load-tool", "h2load",
        "--load-tool-mode", "docker"
        ) + $TargetArgs + $ResourceArgs + $LoadToolMetricsArgs + $TargetContainerMetricsArgs + @(
            "--capture-counters",
            "--counter-refresh-interval", "1",
            "--connections", "$Connections",
            "--streams-per-connection", "$StreamsPerConnection",
            "--duration", "$DurationSeconds",
            "--warmup", "$WarmupSeconds",
            "--repetitions", "$Repetitions",
            "--output", $OutputRoot,
            "--run-id", $countersRunId
        )) -RunId $countersRunId
    }
    else {
        $StageResults.Add([pscustomobject]@{
            Name = "Counter-enabled h2load H3 comparison"
            Status = "skipped"
            ExitCode = $null
            ArtifactPath = "dotnet-counters unavailable; run dotnet tool restore."
        }) | Out-Null
        Write-Warning "dotnet-counters is unavailable; counter-enabled acceptance stage skipped. Run 'dotnet tool restore' to enable it."
    }
}
else {
    $StageResults.Add([pscustomobject]@{ Name = "Counter-enabled h2load H3 comparison"; Status = "skipped"; ExitCode = $null; ArtifactPath = $null }) | Out-Null
}

if (Test-Path -LiteralPath $IndexScript) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $IndexScript -RunsRoot $OutputRoot | Out-Host
}

Write-Host ""
Write-Host "ProtocolLab v1 acceptance stages:"
$StageResults | Format-Table -AutoSize | Out-String | Write-Host

$lastRunId = if ($RunIds.Count -gt 0) { $RunIds[$RunIds.Count - 1] } else { $null }
$finalSummary = if ($lastRunId) { Join-Path (Join-Path $OutputRoot $lastRunId) "summary.md" } else { $null }
$rootIndex = Join-Path $OutputRoot "index.md"

Write-Host "Final summary path: $finalSummary"
Write-Host "Consolidated index: $rootIndex"
Write-Host "Next suggested command: dotnet run --project src\Incursa.ProtocolLab.Cli -- report --run-id $lastRunId --output $OutputRoot"
