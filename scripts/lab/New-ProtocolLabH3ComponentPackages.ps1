# Copyright (c) 2026 Incursa LLC.
# Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

[CmdletBinding()]
param(
    [string] $PackageVersion,

    [string] $OutputRoot = ".artifacts/lab-packages/h3-components",

    [ValidateSet("h3-large-body-v1")]
    [string] $SuiteId = "h3-large-body-v1",

    [string[]] $ScenarioId = @("http.payload.bytes.64kb", "http.payload.bytes.1mb"),

    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$H3LargeBodyScenarioSources = [ordered]@{
    "http.payload.bytes.64kb" = [ordered]@{
        Source = "scenarios/http/payload/bytes-64kb.yaml"
        Entry = "scenarios/http/payload/bytes-64kb.yaml"
        DisplayName = "HTTP Bytes 64KB"
    }
    "http.payload.bytes.1mb" = [ordered]@{
        Source = "scenarios/http/payload/bytes-1mb.yaml"
        Entry = "scenarios/http/payload/bytes-1mb.yaml"
        DisplayName = "HTTP Bytes 1MB"
    }
}

function Resolve-RepoRoot {
    $startPath = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) { (Get-Location).Path } else { $PSScriptRoot }
    $directory = Get-Item -LiteralPath $startPath
    while ($null -ne $directory) {
        if ((Test-Path -LiteralPath (Join-Path $directory.FullName "Incursa.ProtocolLab.sln")) -and
            (Test-Path -LiteralPath (Join-Path $directory.FullName "load-tools/managed-httpclient-h3-load.yaml"))) {
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

function Assert-LargeBodyScenarioSelection {
    param([Parameter(Mandatory = $true)][string[]] $SelectedScenarioIds)

    $selected = @($SelectedScenarioIds | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $expected = @($H3LargeBodyScenarioSources.Keys)
    $unsupported = @($selected | Where-Object { $expected -notcontains $_ })
    if ($unsupported.Count -gt 0) {
        throw "H3 component package builder only supports explicit large-body scenarios: $($expected -join ', '). Unsupported scenario(s): $($unsupported -join ', ')."
    }

    $missing = @($expected | Where-Object { $selected -notcontains $_ })
    if ($missing.Count -gt 0) {
        throw "H3 component package builder requires both large-body scenarios for '$SuiteId': $($missing -join ', ')."
    }
}

function Normalize-ScenarioSelection {
    param([Parameter(Mandatory = $true)][string[]] $SelectedScenarioIds)

    return @($SelectedScenarioIds |
        ForEach-Object { ([string]$_) -split "," } |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function New-TestExecutorManifest {
    param(
        [Parameter(Mandatory = $true)][string] $Version,
        [Parameter(Mandatory = $true)][string[]] $SelectedScenarioIds
    )

    $testSelectors = ($SelectedScenarioIds | ForEach-Object { "  - selectorType: test-id`n    expression: $_" }) -join "`n"
    $scenarioSelectors = ($SelectedScenarioIds | ForEach-Object { "  - selectorType: scenario-id`n    expression: $_" }) -join "`n"

    return @"
# Test Executor Contract v1 manifest
executorIdentity:
  id: managed-httpclient-h3-load
  name: Managed HttpClient HTTP/3 Load
  version: $Version
  vendor: ProtocolLab
versionCompatibility:
  contractVersion: test-executor-v1
  compatibleContractVersions:
    - test-executor-v1
claimedCapabilities:
  - id: http.application
    status: supported
    description: Managed local-lab HTTP/3 validation for deterministic payload download scenarios.
supportedTestSelectors:
$testSelectors
supportedScenarioSelectors:
$scenarioSelectors
supportedProtocolFamilies:
  - h3
supportedExecutionModes:
  - managed
requiredTargetEndpointBindings:
  - bindingId: target
    purpose: h3-server
    endpointType: h3
    protocols:
      - h3
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
      executable: managed-httpclient-h3-load
      workingDirectory: .
      defaultArguments: []
      versionCommand: []
      availabilityCheck: managed
      parser:
        type: managed-httpclient-h3-json
        id: managed-httpclient-h3-json
        preservesRawOutput: true
    limitations:
      - Managed local-lab evidence only
      - Not an external-reference benchmark load generator
    notes: Package declaration for the worker-managed HTTP/3 load generator. The worker must not route raw QUIC requests through this executor.
"@
}

function New-TestExecutorPackageSource {
    param(
        [Parameter(Mandatory = $true)][string] $StageRoot,
        [Parameter(Mandatory = $true)][string] $Version,
        [Parameter(Mandatory = $true)][string[]] $SelectedScenarioIds
    )

    $sourceRoot = Join-Path $StageRoot "protocol-lab-managed-h3-test-executor"
    Remove-Item -LiteralPath $sourceRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path (Join-Path $sourceRoot "test-executors") | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $sourceRoot "bin") | Out-Null

    New-TestExecutorManifest -Version $Version -SelectedScenarioIds $SelectedScenarioIds |
        Set-Content -LiteralPath (Join-Path $sourceRoot "test-executors/managed-httpclient-h3-load.yaml") -NoNewline
    "ProtocolLab managed HTTP/3 package placeholder." |
        Set-Content -LiteralPath (Join-Path $sourceRoot "bin/managed-httpclient-h3-load-placeholder") -NoNewline

    Write-PackageManifest -Path (Join-Path $sourceRoot "protocol-lab-package.json") -Manifest ([ordered]@{
        schemaVersion = "protocol-lab-package-v2"
        packageId = "protocol-lab-managed-h3-test-executor"
        packageVersion = $Version
        kind = "test-executor"
        displayName = "ProtocolLab managed HTTP/3 test executor"
        entryManifests = @("test-executors/managed-httpclient-h3-load.yaml")
        providedTestExecutors = @(
            [ordered]@{
                testExecutorId = "managed-httpclient-h3-load"
                displayName = "Managed HttpClient HTTP/3 Load"
                protocols = @("h3")
                scenarios = @($SelectedScenarioIds)
                requiredCapabilities = @()
            }
        )
        environments = @(
            [ordered]@{
                os = "linux"
                arch = "x64"
                entrypoint = [ordered]@{ kind = "process"; path = "bin/managed-httpclient-h3-load-placeholder"; arguments = @(); workingDirectory = "." }
            },
            [ordered]@{
                os = "windows"
                arch = "x64"
                entrypoint = [ordered]@{ kind = "process"; path = "bin/managed-httpclient-h3-load-placeholder"; arguments = @(); workingDirectory = "." }
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

function New-ScenarioPackageSource {
    param(
        [Parameter(Mandatory = $true)][string] $RepoRoot,
        [Parameter(Mandatory = $true)][string] $StageRoot,
        [Parameter(Mandatory = $true)][string] $Version,
        [Parameter(Mandatory = $true)][string] $SelectedSuiteId,
        [Parameter(Mandatory = $true)][string[]] $SelectedScenarioIds
    )

    $sourceRoot = Join-Path $StageRoot "protocol-lab-h3-large-body-scenarios"
    Remove-Item -LiteralPath $sourceRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $sourceRoot | Out-Null

    Copy-RequiredFile -Source (Join-Path $RepoRoot "suites/$SelectedSuiteId.yaml") -Destination (Join-Path $sourceRoot "suites/$SelectedSuiteId.yaml")
    foreach ($scenario in $SelectedScenarioIds) {
        $sourceInfo = $H3LargeBodyScenarioSources[$scenario]
        Copy-RequiredFile -Source (Join-Path $RepoRoot ([string]$sourceInfo.Source)) -Destination (Join-Path $sourceRoot ([string]$sourceInfo.Entry))
    }

    $providedScenarios = @($SelectedScenarioIds | ForEach-Object {
        $sourceInfo = $H3LargeBodyScenarioSources[$_]
        [ordered]@{ scenarioId = $_; displayName = [string]$sourceInfo.DisplayName; protocols = @("h3") }
    })
    $entryManifests = @("suites/$SelectedSuiteId.yaml") + @($SelectedScenarioIds | ForEach-Object { [string]$H3LargeBodyScenarioSources[$_].Entry })

    Write-PackageManifest -Path (Join-Path $sourceRoot "protocol-lab-package.json") -Manifest ([ordered]@{
        schemaVersion = "protocol-lab-package-v2"
        packageId = "protocol-lab-h3-large-body-scenarios"
        packageVersion = $Version
        kind = "scenario-pack"
        displayName = "ProtocolLab HTTP/3 large-body scenarios"
        entryManifests = $entryManifests
        providedSuites = @(
            [ordered]@{
                suiteId = $SelectedSuiteId
                displayName = "ProtocolLab package-backed HTTP/3 large body suite"
                protocols = @("h3")
                testExecutors = @("managed-httpclient-h3-load")
            }
        )
        providedScenarios = @($providedScenarios)
        environments = @(
            [ordered]@{
                os = "linux"
                arch = "x64"
                entrypoint = [ordered]@{ kind = "process"; path = "suites/$SelectedSuiteId.yaml"; arguments = @(); workingDirectory = "." }
            },
            [ordered]@{
                os = "windows"
                arch = "x64"
                entrypoint = [ordered]@{ kind = "process"; path = "suites/$SelectedSuiteId.yaml"; arguments = @(); workingDirectory = "." }
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

$normalizedScenarioIds = Normalize-ScenarioSelection -SelectedScenarioIds $ScenarioId
Assert-LargeBodyScenarioSelection -SelectedScenarioIds $normalizedScenarioIds
$selectedScenarioIds = @($H3LargeBodyScenarioSources.Keys)

$outputRootFullPath = [System.IO.Path]::GetFullPath($OutputRoot)
$stageRoot = Join-Path $outputRootFullPath "source/$PackageVersion"
$packageRoot = Join-Path $outputRootFullPath "packages"
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null

$builder = Join-Path $repoRoot "scripts/lab/New-ProtocolLabPackage.ps1"
$testExecutorSource = New-TestExecutorPackageSource -StageRoot $stageRoot -Version $PackageVersion -SelectedScenarioIds $selectedScenarioIds
$scenarioSource = New-ScenarioPackageSource -RepoRoot $repoRoot -StageRoot $stageRoot -Version $PackageVersion -SelectedSuiteId $SuiteId -SelectedScenarioIds $selectedScenarioIds

$testExecutorPackageJson = & pwsh -NoLogo -NoProfile -File $builder `
    -SourcePath $testExecutorSource `
    -OutputPath (Join-Path $packageRoot "protocol-lab-managed-h3-test-executor.$PackageVersion.plabpkg") `
    -Force:$Force
if ($LASTEXITCODE -ne 0) {
    throw "Managed H3 test-executor package creation failed."
}

$scenarioPackageJson = & pwsh -NoLogo -NoProfile -File $builder `
    -SourcePath $scenarioSource `
    -OutputPath (Join-Path $packageRoot "protocol-lab-h3-large-body-scenarios.$PackageVersion.plabpkg") `
    -Force:$Force
if ($LASTEXITCODE -ne 0) {
    throw "H3 large-body scenario package creation failed."
}

$testExecutorPackage = $testExecutorPackageJson | ConvertFrom-Json
$scenarioPackage = $scenarioPackageJson | ConvertFrom-Json

[ordered]@{
    packageVersion = $PackageVersion
    suiteId = $SuiteId
    scenarioIds = @($selectedScenarioIds)
    testExecutorPackage = $testExecutorPackage
    scenarioPackage = $scenarioPackage
    packageReferences = @(
        "$($testExecutorPackage.packageId):$($testExecutorPackage.packageVersion):$($testExecutorPackage.sha256)",
        "$($scenarioPackage.packageId):$($scenarioPackage.packageVersion):$($scenarioPackage.sha256)"
    )
} | ConvertTo-Json -Depth 32
