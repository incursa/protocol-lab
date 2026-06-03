<#
.SYNOPSIS
Runs selected ProtocolLab local benchmark and test stages without fail-fast.

.DESCRIPTION
Runs build, test, check, and any number of named benchmark suites. Each stage
records its exit code and the script keeps going so failures become data points
instead of aborting the rest of the workflow. Benchmark runs write their run
artifacts under the selected output root and stage publication bundles beside
them under .artifacts\publication.

.PARAMETER Suite
One or more benchmark suite names. Pass them as a comma-separated list such as
`-Suite ci-public-report,h3-local-v1-comparison`. Supported values are:
  - ci-public-report
  - h3-local-v1-comparison
  - quic-transport-v1-comparison

.PARAMETER RunBuild
Run dotnet build for the solution before any benchmark suites.

.PARAMETER RunTests
Run dotnet test for the solution before any benchmark suites.

.PARAMETER RunCheck
Run the repository check command before any benchmark suites.

.PARAMETER RunIdPrefix
Prefix used for generated benchmark run IDs. Each benchmark suite uses
<prefix>-<suite>.

.PARAMETER Output
Run artifact output root. Defaults to .artifacts\runs.

.PARAMETER PublicationOutputRoot
Bundle output root used by the benchmark runs. Defaults to
.artifacts\publication.

.PARAMETER Configuration
Build configuration for the build/test/check stages and the CLI host when
running benchmarks. Defaults to Release so benchmark runs use the same
configuration end-to-end.

.PARAMETER TargetMode
Overrides the target mode for benchmark runs.

.PARAMETER TargetNetworkMode
Overrides the target network mode for benchmark runs.

.PARAMETER TargetConfiguration
Overrides the target configuration for benchmark runs.

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

.PARAMETER DryRun
Print the planned commands without executing them.

.PARAMETER FailOnError
Throw after the workflow summary is written if any requested stage failed.
#>
[CmdletBinding()]
param(
    [Alias("Benchmark")]
    [string[]]$Suite,
    [switch]$RunBuild,
    [switch]$RunTests,
    [switch]$RunCheck,
    [string]$RunIdPrefix = ("local-workflow-" + (Get-Date -Format "yyyyMMddHHmmss")),
    [string]$Output = ".artifacts\runs",
    [string]$PublicationOutputRoot = ".artifacts\publication",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("process", "docker", "external")]
    [string]$TargetMode,
    [ValidateSet("published-port", "shared-docker-network")]
    [string]$TargetNetworkMode,
    [ValidateSet("Debug", "Release")]
    [string]$TargetConfiguration,
    [string]$ExecutionProfile,
    [int]$DurationSeconds,
    [int]$WarmupSeconds,
    [int]$Repetitions,
    [int]$Connections,
    [int]$StreamsPerConnection,
    [string]$BaseUrl,
    [switch]$DryRun,
    [switch]$FailOnError
)

$ErrorActionPreference = "Stop"
$script:AnyFailures = $false
$script:Results = New-Object System.Collections.Generic.List[object]

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$CliProject = Join-Path $RepoRoot "src\Incursa.ProtocolLab.Cli"
$OutputRoot = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $RepoRoot $Output }
$PublicationRoot = if ([System.IO.Path]::IsPathRooted($PublicationOutputRoot)) { $PublicationOutputRoot } else { Join-Path $RepoRoot $PublicationOutputRoot }
$script:TargetModeOverrideSpecified = $PSBoundParameters.ContainsKey("TargetMode")
$script:TargetNetworkModeOverrideSpecified = $PSBoundParameters.ContainsKey("TargetNetworkMode")
$script:TargetConfigurationOverrideSpecified = $PSBoundParameters.ContainsKey("TargetConfiguration")
$script:ExecutionProfileSpecified = $PSBoundParameters.ContainsKey("ExecutionProfile")
$script:DurationOverrideSpecified = $PSBoundParameters.ContainsKey("DurationSeconds")
$script:WarmupOverrideSpecified = $PSBoundParameters.ContainsKey("WarmupSeconds")
$script:RepetitionsOverrideSpecified = $PSBoundParameters.ContainsKey("Repetitions")
$script:ConnectionsOverrideSpecified = $PSBoundParameters.ContainsKey("Connections")
$script:StreamsOverrideSpecified = $PSBoundParameters.ContainsKey("StreamsPerConnection")
$script:BaseUrlSpecified = $PSBoundParameters.ContainsKey("BaseUrl")

$BenchmarkProfiles = @{
    "ci-public-report" = [pscustomobject]@{
        RunIdSuffix = "ci-public-report"
        Implementations = "kestrel-http3"
        Scenarios = "http.core.plaintext,http.core.json"
        Protocol = "h3"
        LoadTool = "managed-httpclient-h3-load"
        LoadToolMode = "managed"
        LoadProfile = "local-regression"
        TargetMode = "process"
        TargetNetworkMode = "published-port"
        TargetConfiguration = "Release"
    }
    "h3-local-v1-comparison" = [pscustomobject]@{
        RunIdSuffix = "h3-local-v1-comparison"
        Implementations = "kestrel-http3,incursa-http3,quic-go-http3"
        Scenarios = "http.core.plaintext,http.core.json,http.core.status,http.payload.bytes.1kb,http.payload.bytes.64kb,http.payload.bytes.1mb,http.payload.stream.100x16kb,http.headers.inspect-request,http.headers.response.50x32,http.upload.echo.64kb,http.upload.hash.1mb,http.upload.sink.1mb"
        Protocol = "h3"
        LoadTool = "managed-httpclient-h3-load"
        LoadToolMode = "managed"
        LoadProfile = "local-comparison"
        TargetMode = "process"
        TargetNetworkMode = "published-port"
        TargetConfiguration = "Release"
    }
    "quic-transport-v1-comparison" = [pscustomobject]@{
        RunIdSuffix = "quic-transport-v1-comparison"
        Implementations = "incursa-raw-quic-adapter-v1,msquic-dotnet-raw-adapter-v1"
        Scenarios = "quic.transport.handshake-cold,quic.transport.stream-throughput.1mb,quic.transport.multiplex.100x64kb,quic.transport.connection-churn,quic.transport.duplex-streams"
        Protocol = "quic"
        LoadTool = "quic-go-raw-load"
        LoadToolMode = "process"
        LoadProfile = "local-comparison"
        TargetMode = "process"
        TargetNetworkMode = "published-port"
        TargetConfiguration = "Release"
    }
}

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

function Write-StageHeader {
    param([Parameter(Mandatory = $true)][string]$Name)

    Write-Host ""
    Write-Host "==> $Name"
}

function Escape-MdCell {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    return ([string]$Value).Replace("|", "\|").Replace("`r`n", "<br>").Replace("`n", "<br>")
}

function Add-StageResult {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Status,
        [AllowNull()][object]$ExitCode,
        [AllowNull()][object]$ArtifactPath,
        [AllowNull()][object]$PublicationPath,
        [AllowNull()][object]$CommandLine
    )

    $script:Results.Add([pscustomobject]@{
        Name = $Name
        Status = $Status
        ExitCode = $ExitCode
        ArtifactPath = $ArtifactPath
        PublicationPath = $PublicationPath
        CommandLine = $CommandLine
    }) | Out-Null
}

function Invoke-Stage {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [AllowNull()][object]$ArtifactPath,
        [AllowNull()][object]$PublicationPath
    )

    Write-StageHeader -Name $Name
    $commandLine = "$FilePath " + ($Arguments -join " ")
    Write-Host $commandLine

    if ($DryRun) {
        Add-StageResult -Name $Name -Status "planned" -ExitCode $null -ArtifactPath $ArtifactPath -PublicationPath $PublicationPath -CommandLine $commandLine
        return
    }

    & $FilePath @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) {
        Add-StageResult -Name $Name -Status "passed" -ExitCode $exitCode -ArtifactPath $ArtifactPath -PublicationPath $PublicationPath -CommandLine $commandLine
        if ($ArtifactPath) {
            Write-Host "Artifacts: $ArtifactPath"
        }
        if ($PublicationPath) {
            Write-Host "Publication bundle: $PublicationPath"
        }
    }
    else {
        $script:AnyFailures = $true
        Add-StageResult -Name $Name -Status "failed" -ExitCode $exitCode -ArtifactPath $ArtifactPath -PublicationPath $PublicationPath -CommandLine $commandLine
        Write-Warning "$Name failed with exit code $exitCode."
    }
}

function Write-WorkflowSummary {
    param([Parameter(Mandatory = $true)][string]$Path)

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# ProtocolLab Local Workflow Summary")
    $lines.Add("")
    $lines.Add("Generated: $([DateTimeOffset]::UtcNow.ToString('u'))")
    $lines.Add("Run ID prefix: ``$RunIdPrefix``")
    $lines.Add("Configuration: ``$Configuration``")
    $lines.Add("")
    $lines.Add("| Stage | Status | Exit Code | Artifact Path | Publication Path |")
    $lines.Add("| --- | --- | --- | --- | --- |")

    foreach ($result in $script:Results) {
        $lines.Add("| $(Escape-MdCell $result.Name) | $(Escape-MdCell $result.Status) | $(Escape-MdCell $result.ExitCode) | $(Escape-MdCell $result.ArtifactPath) | $(Escape-MdCell $result.PublicationPath) |")
    }

    New-Item -ItemType Directory -Force (Split-Path -Parent $Path) | Out-Null
    Set-Content -LiteralPath $Path -Value $lines -Encoding utf8
}

function New-BenchmarkRunArguments {
    param(
        [Parameter(Mandatory = $true)][pscustomobject]$Profile,
        [Parameter(Mandatory = $true)][string]$RunId
    )

    $targetModeValue = if ($script:TargetModeOverrideSpecified -and -not [string]::IsNullOrWhiteSpace($TargetMode)) { $TargetMode } else { $Profile.TargetMode }
    $targetNetworkModeValue = if ($script:TargetNetworkModeOverrideSpecified -and -not [string]::IsNullOrWhiteSpace($TargetNetworkMode)) { $TargetNetworkMode } else { $Profile.TargetNetworkMode }
    $targetConfigurationValue = if ($script:TargetConfigurationOverrideSpecified -and -not [string]::IsNullOrWhiteSpace($TargetConfiguration)) { $TargetConfiguration } elseif (-not [string]::IsNullOrWhiteSpace($Profile.TargetConfiguration)) { $Profile.TargetConfiguration } else { $Configuration }

    $arguments = @(
        "run",
        "--project", $CliProject,
        "-c", $Configuration,
        "--",
        "run",
        "--implementations", $Profile.Implementations,
        "--scenarios", $Profile.Scenarios,
        "--protocol", $Profile.Protocol,
        "--load-tool", $Profile.LoadTool,
        "--load-tool-mode", $Profile.LoadToolMode,
        "--load-profile", $Profile.LoadProfile,
        "--target-configuration", $targetConfigurationValue,
        "--run-id", $RunId,
        "--output", $OutputRoot,
        "--publication-output", (Join-Path $PublicationRoot $RunId)
    )

    if (-not [string]::IsNullOrWhiteSpace($targetModeValue)) {
        $arguments += @("--target-mode", $targetModeValue)
    }

    if (-not [string]::IsNullOrWhiteSpace($targetNetworkModeValue)) {
        $arguments += @("--target-network-mode", $targetNetworkModeValue)
    }

    if ($script:ExecutionProfileSpecified -and -not [string]::IsNullOrWhiteSpace($ExecutionProfile)) {
        $arguments += @("--execution-profile", $ExecutionProfile)
    }

    if ($script:BaseUrlSpecified -and -not [string]::IsNullOrWhiteSpace($BaseUrl)) {
        $arguments += @("--base-url", $BaseUrl)
    }

    if ($script:DurationOverrideSpecified) {
        $arguments += @("--duration", "$DurationSeconds")
    }

    if ($script:WarmupOverrideSpecified) {
        $arguments += @("--warmup", "$WarmupSeconds")
    }

    if ($script:RepetitionsOverrideSpecified) {
        $arguments += @("--repetitions", "$Repetitions")
    }

    if ($script:ConnectionsOverrideSpecified) {
        $arguments += @("--connections", "$Connections")
    }

    if ($script:StreamsOverrideSpecified) {
        $arguments += @("--streams-per-connection", "$StreamsPerConnection")
    }

    return $arguments
}

Set-Location $RepoRoot
New-Item -ItemType Directory -Force $OutputRoot | Out-Null
New-Item -ItemType Directory -Force $PublicationRoot | Out-Null

if (-not $RunBuild -and -not $RunTests -and -not $RunCheck -and (-not $Suite -or $Suite.Count -lt 1)) {
    throw "Specify at least one stage switch or one benchmark suite."
}

$selectedSuites = New-Object System.Collections.Generic.List[string]
foreach ($suiteValue in @($Suite)) {
    foreach ($suiteName in @($suiteValue -split ',')) {
        $trimmed = $suiteName.Trim()
        if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
            $selectedSuites.Add($trimmed) | Out-Null
        }
    }
}

if ($RunBuild) {
    Invoke-Stage -Name "Build solution" -FilePath "dotnet" -Arguments @("build", "Incursa.ProtocolLab.sln", "-c", $Configuration) -ArtifactPath $null -PublicationPath $null
}

if ($RunTests) {
    Invoke-Stage -Name "Test solution" -FilePath "dotnet" -Arguments @("test", "Incursa.ProtocolLab.sln", "-c", $Configuration) -ArtifactPath $null -PublicationPath $null
}

if ($RunCheck) {
    Invoke-Stage -Name "Run repository check" -FilePath "dotnet" -Arguments @("run", "--project", "src\Incursa.ProtocolLab.Cli", "-c", $Configuration, "--", "check") -ArtifactPath $null -PublicationPath $null
}

foreach ($suiteName in @($selectedSuites)) {
    if (-not $BenchmarkProfiles.ContainsKey($suiteName)) {
        $script:AnyFailures = $true
        Add-StageResult -Name "Benchmark suite $suiteName" -Status "failed" -ExitCode 1 -ArtifactPath "Unknown suite '$suiteName'." -PublicationPath $null -CommandLine $null
        Write-Warning "Unknown benchmark suite '$suiteName'. Supported values: $($BenchmarkProfiles.Keys -join ', ')."
        continue
    }

    $profile = $BenchmarkProfiles[$suiteName]
    $runId = "$RunIdPrefix-$($profile.RunIdSuffix)"
    $runArguments = New-BenchmarkRunArguments -Profile $profile -RunId $runId
    Invoke-Stage -Name "Benchmark suite $suiteName" -FilePath "dotnet" -Arguments $runArguments -ArtifactPath (Join-Path $OutputRoot $runId) -PublicationPath (Join-Path $PublicationRoot $runId)
}

$summaryPath = Join-Path $OutputRoot "workflow-summary.md"
Write-WorkflowSummary -Path $summaryPath
Write-Host ""
Write-Host "Workflow summary: $summaryPath"

if (-not $DryRun -and $Suite -and $Suite.Count -gt 0) {
    $indexScript = Join-Path $RepoRoot "scripts\analysis\New-ProtocolLabRunIndex.ps1"
    if (Test-Path -LiteralPath $indexScript) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $indexScript -RunsRoot $OutputRoot | Out-Host
    }
}

Write-Host ""
Write-Host "Workflow results:"
$script:Results | Format-Table -AutoSize | Out-String | Write-Host

if ($FailOnError -and $script:AnyFailures) {
    throw "One or more requested stages failed. Review the workflow summary and run artifacts."
}
