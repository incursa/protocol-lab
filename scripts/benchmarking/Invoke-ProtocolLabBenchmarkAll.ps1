<#
.SYNOPSIS
Runs the full ProtocolLab benchmark suite catalog.

.DESCRIPTION
This is the no-suite entrypoint for the benchmark runbook. It runs the
benchmark suite catalog through Invoke-ProtocolLabBenchmarkSet.ps1 after
enabling build, test, and repository check stages.

.PARAMETER RunIdPrefix
Prefix used for generated benchmark run IDs.

.PARAMETER Output
Run artifact output root. Defaults to .artifacts\runs.

.PARAMETER PublicationOutputRoot
Bundle output root used by the benchmark runs. Defaults to
.artifacts\publication.

.PARAMETER Configuration
Build configuration for the build/test/check stages and the CLI host when
running benchmarks. Defaults to Release.

.PARAMETER WorkflowProfile
Benchmark workflow profile. Quick runs only the smallest public-report artifact
proof, Regression uses local-regression load shapes, and Comparison preserves
the full local-comparison behavior. Defaults to Comparison for this run-all
entrypoint.

.PARAMETER ExecutionProfile
Optional execution profile to pass through to benchmark runs.

.PARAMETER DurationSeconds
Optional duration override for benchmark runs.

.PARAMETER WarmupSeconds
Optional warmup override for benchmark runs.

.PARAMETER Repetitions
Optional repetition override for benchmark runs.

.PARAMETER Connections
Optional connection count override for benchmark runs.

.PARAMETER StreamsPerConnection
Optional streams-per-connection override for benchmark runs.

.PARAMETER BaseUrl
Optional base URL override for benchmark runs.

.PARAMETER NoRestore
Pass --no-restore to dotnet build/test/run stages. Intended for tight
source-reference loops after the first restore.

.PARAMETER DryRun
Print the planned commands without executing them.

.PARAMETER FailOnError
Throw after the workflow summary is written if any requested stage failed.
#>
[CmdletBinding()]
param(
    [string]$RunIdPrefix = ("local-all-" + (Get-Date -Format "yyyyMMddHHmmss")),
    [string]$Output = ".artifacts\runs",
    [string]$PublicationOutputRoot = ".artifacts\publication",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("Quick", "Regression", "Comparison")]
    [string]$WorkflowProfile = "Comparison",
    [string]$ExecutionProfile,
    [int]$DurationSeconds,
    [int]$WarmupSeconds,
    [int]$Repetitions,
    [int]$Connections,
    [int]$StreamsPerConnection,
    [string]$BaseUrl,
    [switch]$NoRestore,
    [switch]$DryRun,
    [switch]$FailOnError
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$BenchmarkSetScript = Join-Path $RepoRoot "scripts\benchmarking\Invoke-ProtocolLabBenchmarkSet.ps1"
$BenchmarkSuites = if ($WorkflowProfile -eq "Quick") {
    @("ci-public-report")
}
else {
    @(
        "ci-public-report",
        "h3-local-v1-comparison"
    )
}

if (-not (Test-Path -LiteralPath $BenchmarkSetScript)) {
    throw "Benchmark set script not found: $BenchmarkSetScript"
}

$arguments = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $BenchmarkSetScript,
    "-RunBuild",
    "-RunTests",
    "-RunCheck",
    "-Suite", ($BenchmarkSuites -join ","),
    "-RunIdPrefix", $RunIdPrefix,
    "-WorkflowProfile", $WorkflowProfile,
    "-Output", $Output,
    "-PublicationOutputRoot", $PublicationOutputRoot,
    "-Configuration", $Configuration
)

if (-not [string]::IsNullOrWhiteSpace($ExecutionProfile)) {
    $arguments += @("-ExecutionProfile", $ExecutionProfile)
}

if ($PSBoundParameters.ContainsKey("DurationSeconds")) {
    $arguments += @("-DurationSeconds", "$DurationSeconds")
}

if ($PSBoundParameters.ContainsKey("WarmupSeconds")) {
    $arguments += @("-WarmupSeconds", "$WarmupSeconds")
}

if ($PSBoundParameters.ContainsKey("Repetitions")) {
    $arguments += @("-Repetitions", "$Repetitions")
}

if ($PSBoundParameters.ContainsKey("Connections")) {
    $arguments += @("-Connections", "$Connections")
}

if ($PSBoundParameters.ContainsKey("StreamsPerConnection")) {
    $arguments += @("-StreamsPerConnection", "$StreamsPerConnection")
}

if (-not [string]::IsNullOrWhiteSpace($BaseUrl)) {
    $arguments += @("-BaseUrl", $BaseUrl)
}

if ($NoRestore) {
    $arguments += "-NoRestore"
}

if ($DryRun) {
    $arguments += "-DryRun"
}

if ($FailOnError) {
    $arguments += "-FailOnError"
}

Write-Host ("powershell " + ($arguments -join " "))
& powershell @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Benchmark-all workflow failed with exit code $LASTEXITCODE."
}
