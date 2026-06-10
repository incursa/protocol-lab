# Copyright (c) 2026 Incursa LLC.
# Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

[CmdletBinding()]
param(
    [string] $PackageVersion,

    [string] $OutputRoot = ".artifacts/lab-packages/raw-quic-components",

    [ValidateSet("linux-x64")]
    [string] $TestExecutorRuntimeIdentifier = "linux-x64",

    [switch] $SourceBackedTestExecutor,

    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
    $directory = Get-Item -LiteralPath (Get-Location).Path
    while ($null -ne $directory) {
        if ((Test-Path -LiteralPath (Join-Path $directory.FullName "Incursa.ProtocolLab.sln")) -and
            (Test-Path -LiteralPath (Join-Path $directory.FullName "load-tools/quic-go-raw-load.yaml"))) {
            return $directory.FullName
        }

        $directory = $directory.Parent
    }

    throw "Could not locate ProtocolLab repository root."
}

function Copy-RequiredFile {
    param(
        [Parameter(Mandatory = $true)][string] $Source,
        [Parameter(Mandatory = $true)][string] $Destination
    )

    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        throw "Required package source file was not found: $Source"
    }

    New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($Destination)) | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

function Write-PackageManifest {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][object] $Manifest
    )

    New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($Path)) | Out-Null
    $Manifest | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $Path -NoNewline
}

function Get-GoBuildEnvironment {
    param(
        [Parameter(Mandatory = $true)][string] $RuntimeIdentifier
    )

    switch ($RuntimeIdentifier) {
        "linux-x64" {
            return [ordered]@{
                GOOS = "linux"
                GOARCH = "amd64"
                ExecutableName = "quic-go-raw-load"
            }
        }
        default {
            throw "Unsupported raw QUIC test-executor runtime identifier '$RuntimeIdentifier'."
        }
    }
}

function Invoke-GoBuild {
    param(
        [Parameter(Mandatory = $true)][string] $RepoRoot,
        [Parameter(Mandatory = $true)][string] $RuntimeIdentifier,
        [Parameter(Mandatory = $true)][string] $OutputPath
    )

    $buildEnvironment = Get-GoBuildEnvironment -RuntimeIdentifier $RuntimeIdentifier
    $testExecutorRoot = Join-Path $RepoRoot "tools/test-executors/quic-go-raw-load"
    New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($OutputPath)) | Out-Null

    $previousGoos = [Environment]::GetEnvironmentVariable("GOOS")
    $previousGoarch = [Environment]::GetEnvironmentVariable("GOARCH")
    try {
        [Environment]::SetEnvironmentVariable("GOOS", [string]$buildEnvironment.GOOS)
        [Environment]::SetEnvironmentVariable("GOARCH", [string]$buildEnvironment.GOARCH)

        $buildOutput = & go -C $testExecutorRoot build -trimpath -o $OutputPath ./cmd/quic-go-raw-load 2>&1
        if ($LASTEXITCODE -ne 0) {
            $message = ($buildOutput | Out-String).Trim()
            throw "Raw QUIC test-executor binary build failed for '$RuntimeIdentifier'. Ensure Go is installed on the packaging machine. $message"
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable("GOOS", $previousGoos)
        [Environment]::SetEnvironmentVariable("GOARCH", $previousGoarch)
    }
}

function Convert-TestExecutorManifestForPackage {
    param(
        [Parameter(Mandatory = $true)][string] $ManifestText,
        [Parameter(Mandatory = $true)][string] $ExecutablePath,
        [Parameter(Mandatory = $true)][string] $PackageVersion,
        [string] $WorkingDirectory = ".",
        [Parameter(Mandatory = $true)][bool] $BinaryBacked
    )

    $defaultArguments = if ($BinaryBacked) {
        "      defaultArguments: []"
    }
    else {
        @"
      defaultArguments:
        - run
        - ./cmd/quic-go-raw-load
"@
    }
    if ($BinaryBacked) {
        $limitation = "Self-contained test-executor binary; Go is required only when building the package"
        $notes = "Go-backed raw QUIC test executor packaged as a self-contained Linux x64 process payload. The worker launches the package-local executable $ExecutablePath through the selected test-executor package; missing package-local executables must be reported as unavailable rather than rerouted through HTTP/3 tooling."
        $versionCommand = "--version"
    }
    else {
        $limitation = "Requires Go 1.26+ on PATH"
        $notes = "Go-backed raw QUIC test executor for ProtocolLab local benchmarks. The worker launches it from package-local workingDirectory source with go run ./cmd/quic-go-raw-load so the repo can benchmark real raw QUIC transport clients without a separate compiled binary."
        $versionCommand = "version"
    }

    return @"
# Test Executor Contract v1 manifest
executorIdentity:
  id: quic-go-raw-load
  name: quic-go Raw QUIC Load
  version: $PackageVersion
  vendor: ProtocolLab
versionCompatibility:
  contractVersion: test-executor-v1
  compatibleContractVersions:
    - test-executor-v1
claimedCapabilities:
  - id: quic.transport
    status: supported
    description: Raw QUIC transport validation for the currently enabled multiplex and duplex scenarios.
supportedTestSelectors:
  - selectorType: test-id
    expression: quic.transport.multiplex.100x64kb
  - selectorType: test-id
    expression: quic.transport.duplex-streams
supportedScenarioSelectors:
  - selectorType: scenario-id
    expression: quic.transport.multiplex.100x64kb
  - selectorType: scenario-id
    expression: quic.transport.duplex-streams
supportedProtocolFamilies:
  - quic
supportedExecutionModes:
  - process
requiredTargetEndpointBindings:
  - bindingId: target
    purpose: raw-quic-server
    endpointType: quic
    protocols:
      - quic
supportedArtifactTypes:
  - type: load.stdout.log
  - type: load.stderr.log
  - type: load.metrics.json
metricsAvailability:
  available: true
  availableKinds:
    - summary
extensions:
  protocolLabPackage:
    process:
      executable: $ExecutablePath
      workingDirectory: $WorkingDirectory
$defaultArguments
      versionCommand:
        - $versionCommand
      availabilityCheck: path
      parser:
        type: raw-quic-json
        id: raw-quic-json
        preservesRawOutput: true
    limitations:
      - $limitation
      - Benchmark evidence stays local-lab
      - qlog capture is not enabled yet
    notes: $notes
"@
}

function New-TestExecutorPackageSource {
    param(
        [Parameter(Mandatory = $true)][string] $RepoRoot,
        [Parameter(Mandatory = $true)][string] $StageRoot,
        [Parameter(Mandatory = $true)][string] $Version,
        [Parameter(Mandatory = $true)][string] $RuntimeIdentifier,
        [Parameter(Mandatory = $true)][bool] $BinaryBacked
    )

    $sourceRoot = Join-Path $StageRoot "protocol-lab-raw-quic-test-executor"
    Remove-Item -LiteralPath $sourceRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $sourceRoot | Out-Null

    $executablePath = "bin/$RuntimeIdentifier/quic-go-raw-load"
    $workingDirectory = "."
    if ($BinaryBacked) {
        Invoke-GoBuild -RepoRoot $RepoRoot -RuntimeIdentifier $RuntimeIdentifier -OutputPath (Join-Path $sourceRoot $executablePath)
    }
    else {
        $testExecutorSource = Join-Path $RepoRoot "tools/test-executors/quic-go-raw-load"
        $testExecutorDestination = Join-Path $sourceRoot "source"
        Copy-Item -LiteralPath $testExecutorSource -Destination $testExecutorDestination -Recurse -Force
        $executablePath = "go"
        $workingDirectory = "source"
    }

    $testExecutorText = Get-Content -LiteralPath (Join-Path $RepoRoot "load-tools/quic-go-raw-load.yaml") -Raw
    $testExecutorText = Convert-TestExecutorManifestForPackage -ManifestText $testExecutorText -ExecutablePath $executablePath -PackageVersion $Version -WorkingDirectory $workingDirectory -BinaryBacked:$BinaryBacked
    New-Item -ItemType Directory -Force -Path (Join-Path $sourceRoot "test-executors") | Out-Null
    $testExecutorText | Set-Content -LiteralPath (Join-Path $sourceRoot "test-executors/quic-go-raw-load.yaml") -NoNewline

    [object[]]$testExecutorRequiredCapabilities = @()
    if (-not $BinaryBacked) {
        $testExecutorRequiredCapabilities = @([ordered]@{ name = "go"; value = "true" })
    }

    Write-PackageManifest -Path (Join-Path $sourceRoot "protocol-lab-package.json") -Manifest ([ordered]@{
        schemaVersion = "protocol-lab-package-v2"
        packageId = "protocol-lab-raw-quic-test-executor"
        packageVersion = $Version
        kind = "test-executor"
        displayName = "ProtocolLab raw QUIC test executor"
        entryManifests = @("test-executors/quic-go-raw-load.yaml")
        providedTestExecutors = @(
            [ordered]@{
                testExecutorId = "quic-go-raw-load"
                displayName = "quic-go Raw QUIC Load"
                protocols = @("quic")
                scenarios = @(
                    "quic.transport.multiplex.100x64kb",
                    "quic.transport.duplex-streams"
                )
                requiredCapabilities = @($testExecutorRequiredCapabilities)
            }
        )
        environments = @(
            [ordered]@{
                os = "linux"
                arch = "x64"
                entrypoint = [ordered]@{
                    kind = "process"
                    path = if ($BinaryBacked) { $executablePath } else { "source/go.mod" }
                    arguments = @()
                    workingDirectory = "."
                }
            }
        )
        dependencies = [ordered]@{
            requiresDotNet = $false
            requiresDocker = $false
            requiresPwsh = $false
            requiresBash = $false
            requiresGo = -not $BinaryBacked
            requiredCapabilities = @($testExecutorRequiredCapabilities)
        }
    })

    return $sourceRoot
}

function New-ScenarioPackageSource {
    param(
        [Parameter(Mandatory = $true)][string] $RepoRoot,
        [Parameter(Mandatory = $true)][string] $StageRoot,
        [Parameter(Mandatory = $true)][string] $Version
    )

    $sourceRoot = Join-Path $StageRoot "protocol-lab-raw-quic-scenarios"
    Remove-Item -LiteralPath $sourceRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $sourceRoot | Out-Null

    New-Item -ItemType Directory -Force -Path (Join-Path $sourceRoot "suites") | Out-Null
    @"
id: quic-transport-v1-comparison
name: ProtocolLab raw QUIC transport comparison suite
description: Package-local raw QUIC transport suite for the currently enabled multiplex and duplex validation cells.
loadProfileId: local-comparison
targetMode: process
targetNetworkMode: published-port
implementations:
  []
scenarios:
  - quic.transport.multiplex.100x64kb
  - quic.transport.duplex-streams
protocol: quic
loadTools:
  - id: quic-go-raw-load
    mode: process
    category: managed-lab
defaults:
  durationSeconds: 30
  warmupSeconds: 10
  repetitions: 3
  connections: 32
  streamsPerConnection: 16
counterCapture:
  enabledByDefault: false
  tool: dotnet-counters
notes: Package-backed raw QUIC proof is intentionally narrow until additional validators and artifact gates exist. Implementation selection is resolved from submitted implementation packages or catalog overlays.
"@ | Set-Content -LiteralPath (Join-Path $sourceRoot "suites/quic-transport-v1-comparison.yaml") -NoNewline

    foreach ($scenario in @("duplex-streams.yaml", "multiplex-100-streams.yaml")) {
        Copy-RequiredFile -Source (Join-Path $RepoRoot "scenarios/quic/transport/$scenario") -Destination (Join-Path $sourceRoot "scenarios/quic/transport/$scenario")
    }

    Write-PackageManifest -Path (Join-Path $sourceRoot "protocol-lab-package.json") -Manifest ([ordered]@{
        schemaVersion = "protocol-lab-package-v2"
        packageId = "protocol-lab-raw-quic-scenarios"
        packageVersion = $Version
        kind = "scenario-pack"
        displayName = "ProtocolLab raw QUIC transport scenarios"
        entryManifests = @(
            "suites/quic-transport-v1-comparison.yaml",
            "scenarios/quic/transport/duplex-streams.yaml",
            "scenarios/quic/transport/multiplex-100-streams.yaml"
        )
        providedSuites = @(
            [ordered]@{
                suiteId = "quic-transport-v1-comparison"
                displayName = "ProtocolLab raw QUIC transport comparison suite"
                protocols = @("quic")
                testExecutors = @("quic-go-raw-load")
            }
        )
        providedScenarios = @(
            [ordered]@{ scenarioId = "quic.transport.multiplex.100x64kb"; displayName = "Raw QUIC multiplex"; protocols = @("quic") },
            [ordered]@{ scenarioId = "quic.transport.duplex-streams"; displayName = "Raw QUIC duplex streams"; protocols = @("quic") }
        )
        environments = @(
            [ordered]@{
                os = "linux"
                arch = "x64"
                entrypoint = [ordered]@{ kind = "process"; path = "suites/quic-transport-v1-comparison.yaml"; arguments = @(); workingDirectory = "." }
            },
            [ordered]@{
                os = "windows"
                arch = "x64"
                entrypoint = [ordered]@{ kind = "process"; path = "suites/quic-transport-v1-comparison.yaml"; arguments = @(); workingDirectory = "." }
            }
        )
        dependencies = [ordered]@{
            requiresDotNet = $false
            requiresDocker = $false
            requiresPwsh = $false
            requiresBash = $false
            requiresGo = $false
            requiredCapabilities = @()
        }
    })

    return $sourceRoot
}

$repoRoot = Resolve-RepoRoot
if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $PackageVersion = "dev-" + (Get-Date -AsUTC -Format "yyyyMMddTHHmmssZ")
}

$outputRootFullPath = [System.IO.Path]::GetFullPath($OutputRoot)
$stageRoot = Join-Path $outputRootFullPath "source/$PackageVersion"
$packageRoot = Join-Path $outputRootFullPath "packages"
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null

$builder = Join-Path $repoRoot "scripts/lab/New-ProtocolLabPackage.ps1"
$testExecutorSource = New-TestExecutorPackageSource -RepoRoot $repoRoot -StageRoot $stageRoot -Version $PackageVersion -RuntimeIdentifier $TestExecutorRuntimeIdentifier -BinaryBacked:(-not $SourceBackedTestExecutor)
$scenarioSource = New-ScenarioPackageSource -RepoRoot $repoRoot -StageRoot $stageRoot -Version $PackageVersion

$testExecutorPackageJson = & pwsh -NoLogo -NoProfile -File $builder `
    -SourcePath $testExecutorSource `
    -OutputPath (Join-Path $packageRoot "protocol-lab-raw-quic-test-executor.$PackageVersion.plabpkg") `
    -Force:$Force `
    -SkipEntrypointFileCheck
if ($LASTEXITCODE -ne 0) {
    throw "Raw QUIC test-executor package creation failed."
}

$scenarioPackageJson = & pwsh -NoLogo -NoProfile -File $builder `
    -SourcePath $scenarioSource `
    -OutputPath (Join-Path $packageRoot "protocol-lab-raw-quic-scenarios.$PackageVersion.plabpkg") `
    -Force:$Force
if ($LASTEXITCODE -ne 0) {
    throw "Raw QUIC scenario package creation failed."
}

$testExecutorPackage = $testExecutorPackageJson | ConvertFrom-Json
$scenarioPackage = $scenarioPackageJson | ConvertFrom-Json

[ordered]@{
    packageVersion = $PackageVersion
    testExecutorRuntimeIdentifier = $TestExecutorRuntimeIdentifier
    testExecutorPackagingMode = if ($SourceBackedTestExecutor) { "source-backed" } else { "binary-backed" }
    testExecutorPackage = $testExecutorPackage
    scenarioPackage = $scenarioPackage
    packageReferences = @(
        "$($testExecutorPackage.packageId):$($testExecutorPackage.packageVersion):$($testExecutorPackage.sha256)",
        "$($scenarioPackage.packageId):$($scenarioPackage.packageVersion):$($scenarioPackage.sha256)"
    )
} | ConvertTo-Json -Depth 32
