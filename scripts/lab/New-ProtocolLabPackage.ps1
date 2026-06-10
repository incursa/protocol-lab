# Copyright (c) 2026 Incursa LLC.
# Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $SourcePath,

    [string] $OutputPath,

    [switch] $Force,

    [switch] $SkipEntrypointFileCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string] $Path)

    return [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path)
}

function Test-PackageRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $FieldName
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "$FieldName must not be empty."
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        throw "$FieldName must be package-relative: $Path"
    }

    $segments = $Path -split "[/\\]+"
    if ($segments -contains "..") {
        throw "$FieldName must not contain path traversal: $Path"
    }

    if ($Path -match "^[a-zA-Z]:") {
        throw "$FieldName must not contain a drive-qualified path: $Path"
    }

    if ($Path -match "^[a-zA-Z][a-zA-Z0-9+.-]*:") {
        throw "$FieldName must not be a URI path: $Path"
    }
}

function Assert-PackageFileExists {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath,

        [Parameter(Mandatory = $true)]
        [string] $FieldName
    )

    Test-PackageRelativePath -Path $RelativePath -FieldName $FieldName
    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    $rootWithSeparator = $Root.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not $fullPath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$FieldName escapes the package root: $RelativePath"
    }

    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "$FieldName file does not exist: $RelativePath"
    }
}

function Assert-PackageDirectoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath,

        [Parameter(Mandatory = $true)]
        [string] $FieldName
    )

    Test-PackageRelativePath -Path $RelativePath -FieldName $FieldName
    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    $rootWithSeparator = $Root.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not $fullPath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $fullPath.Equals($Root, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$FieldName escapes the package root: $RelativePath"
    }
}

function Assert-RequiredProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Object,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    if (-not ($Object.PSObject.Properties.Name -contains $Name)) {
        throw "protocol-lab-package.json is missing required property '$Name'."
    }

    $value = $Object.$Name
    if ($null -eq $value) {
        throw "protocol-lab-package.json property '$Name' must not be null."
    }

    if ($value -is [string] -and [string]::IsNullOrWhiteSpace($value)) {
        throw "protocol-lab-package.json property '$Name' must not be empty."
    }
}

function Get-OptionalProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Object,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    if ($Object.PSObject.Properties.Name -contains $Name) {
        return $Object.$Name
    }

    return $null
}

function Assert-StringArrayProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Object,

        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [string] $Context
    )

    $value = Get-OptionalProperty -Object $Object -Name $Name
    if ($null -eq $value) {
        throw "$Context must declare '$Name'."
    }

    $items = @($value)
    if ($items.Count -lt 1) {
        throw "$Context.$Name must contain at least one value."
    }

    foreach ($item in $items) {
        if ([string]::IsNullOrWhiteSpace([string]$item)) {
            throw "$Context.$Name must not contain empty values."
        }
    }
}

function Assert-StringProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Object,

        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [string] $Context
    )

    $value = Get-OptionalProperty -Object $Object -Name $Name
    if ([string]::IsNullOrWhiteSpace([string]$value)) {
        throw "$Context must declare non-empty '$Name'."
    }
}

function Assert-V2ProvidedComponents {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Manifest
    )

    switch ([string]$Manifest.kind) {
        "implementation" {
            $rawComponents = Get-OptionalProperty -Object $Manifest -Name "providedImplementations"
            $components = if ($null -eq $rawComponents) { @() } else { @($rawComponents) }
            if ($components.Count -lt 1) {
                throw "V2 implementation packages must declare providedImplementations so schedulers can match implementation compatibility."
            }

            foreach ($component in $components) {
                Assert-StringProperty -Object $component -Name "implementationId" -Context "providedImplementations[]"
                Assert-StringArrayProperty -Object $component -Name "protocols" -Context "providedImplementations[]"
            }
        }
        "test-executor" {
            $rawComponents = Get-OptionalProperty -Object $Manifest -Name "providedTestExecutors"
            $components = if ($null -eq $rawComponents) { @() } else { @($rawComponents) }
            if ($components.Count -lt 1) {
                throw "V2 test-executor packages must declare providedTestExecutors so schedulers can match executor compatibility."
            }

            foreach ($component in $components) {
                Assert-StringProperty -Object $component -Name "testExecutorId" -Context "providedTestExecutors[]"
                Assert-StringArrayProperty -Object $component -Name "protocols" -Context "providedTestExecutors[]"
            }
        }
        "scenario-pack" {
            $rawSuites = Get-OptionalProperty -Object $Manifest -Name "providedSuites"
            $rawScenarios = Get-OptionalProperty -Object $Manifest -Name "providedScenarios"
            $suites = if ($null -eq $rawSuites) { @() } else { @($rawSuites) }
            $scenarios = if ($null -eq $rawScenarios) { @() } else { @($rawScenarios) }
            if ($suites.Count -lt 1 -and $scenarios.Count -lt 1) {
                throw "V2 scenario-pack packages must declare providedSuites or providedScenarios so schedulers can match scenario compatibility."
            }

            foreach ($suite in $suites) {
                Assert-StringProperty -Object $suite -Name "suiteId" -Context "providedSuites[]"
                Assert-StringArrayProperty -Object $suite -Name "protocols" -Context "providedSuites[]"
                $testExecutors = Get-OptionalProperty -Object $suite -Name "testExecutors"
                if ($null -ne $testExecutors) {
                    Assert-StringArrayProperty -Object $suite -Name "testExecutors" -Context "providedSuites[]"
                }
            }

            foreach ($scenario in $scenarios) {
                Assert-StringProperty -Object $scenario -Name "scenarioId" -Context "providedScenarios[]"
                Assert-StringArrayProperty -Object $scenario -Name "protocols" -Context "providedScenarios[]"
            }
        }
        "toolchain" {
            $capabilities = Get-OptionalProperty -Object $Manifest.dependencies -Name "requiredCapabilities"
            if ($null -eq $capabilities) {
                throw "V2 toolchain packages must declare dependencies.requiredCapabilities, even when empty."
            }
        }
    }
}

function Assert-V2EntryManifestLayout {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Manifest
    )

    $expectedPrefixes = @(switch ([string]$Manifest.kind) {
        "implementation" { @("implementations/") }
        "test-executor" { @("test-executors/") }
        "scenario-pack" { @("scenarios/", "suites/") }
        default { @() }
    })

    if ($expectedPrefixes.Count -eq 0) {
        return
    }

    foreach ($entryManifest in @($Manifest.entryManifests)) {
        $normalizedEntryManifest = ([string]$entryManifest) -replace "\\", "/"
        $matchesExpectedPrefix = $false
        foreach ($prefix in $expectedPrefixes) {
            if ($normalizedEntryManifest.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
                $matchesExpectedPrefix = $true
                break
            }
        }

        if (-not $matchesExpectedPrefix) {
            throw "V2 $($Manifest.kind) entryManifests must live under $($expectedPrefixes -join ' or '): $entryManifest"
        }
    }
}

$sourceRoot = Resolve-FullPath -Path $SourcePath
if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
    throw "SourcePath must be a directory: $SourcePath"
}

$manifestPath = Join-Path $sourceRoot "protocol-lab-package.json"
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Package source must contain protocol-lab-package.json at the root."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
foreach ($property in @("schemaVersion", "packageId", "packageVersion", "kind", "entryManifests", "environments", "dependencies")) {
    Assert-RequiredProperty -Object $manifest -Name $property
}

$supportedSchemaVersions = @("protocol-lab-package-v1", "protocol-lab-package-v2")
if ($supportedSchemaVersions -notcontains $manifest.schemaVersion) {
    throw "Unsupported schemaVersion '$($manifest.schemaVersion)'. Expected one of: $($supportedSchemaVersions -join ', ')."
}

if ($manifest.schemaVersion -eq "protocol-lab-package-v1" -and $manifest.kind -ne "implementation") {
    throw "Unsupported package kind '$($manifest.kind)'. V1 supports only 'implementation'."
}

$supportedV2Kinds = @("implementation", "test-executor", "scenario-pack", "toolchain")
if ($manifest.schemaVersion -eq "protocol-lab-package-v2" -and $supportedV2Kinds -notcontains $manifest.kind) {
    throw "Unsupported package kind '$($manifest.kind)'. V2 supports: $($supportedV2Kinds -join ', ')."
}

if ($manifest.schemaVersion -eq "protocol-lab-package-v2") {
    Assert-V2ProvidedComponents -Manifest $manifest
    Assert-V2EntryManifestLayout -Manifest $manifest
}

if ($manifest.entryManifests.Count -lt 1 -and $manifest.kind -ne "toolchain") {
    throw "entryManifests must contain at least one component manifest."
}

foreach ($entryManifest in $manifest.entryManifests) {
    Assert-PackageFileExists -Root $sourceRoot -RelativePath ([string]$entryManifest) -FieldName "entryManifests"
}

if ($manifest.environments.Count -lt 1) {
    throw "environments must contain at least one supported environment."
}

$supportedOs = @("linux", "windows", "macos")
$supportedArch = @("x64", "arm64")
$supportedEntrypoints = @("process", "bash", "pwsh")

foreach ($environment in $manifest.environments) {
    foreach ($property in @("os", "arch", "entrypoint")) {
        Assert-RequiredProperty -Object $environment -Name $property
    }

    if ($supportedOs -notcontains $environment.os) {
        throw "Unsupported environment os '$($environment.os)'. Expected one of: $($supportedOs -join ', ')."
    }

    if ($supportedArch -notcontains $environment.arch) {
        throw "Unsupported environment arch '$($environment.arch)'. Expected one of: $($supportedArch -join ', ')."
    }

    foreach ($property in @("kind", "path", "arguments", "workingDirectory")) {
        Assert-RequiredProperty -Object $environment.entrypoint -Name $property
    }

    if ($supportedEntrypoints -notcontains $environment.entrypoint.kind) {
        throw "Unsupported entrypoint kind '$($environment.entrypoint.kind)'. Expected one of: $($supportedEntrypoints -join ', ')."
    }

    if (-not $SkipEntrypointFileCheck) {
        Assert-PackageFileExists -Root $sourceRoot -RelativePath ([string]$environment.entrypoint.path) -FieldName "entrypoint.path"
    }
    else {
        Test-PackageRelativePath -Path ([string]$environment.entrypoint.path) -FieldName "entrypoint.path"
    }

    Assert-PackageDirectoryPath -Root $sourceRoot -RelativePath ([string]$environment.entrypoint.workingDirectory) -FieldName "entrypoint.workingDirectory"
}

foreach ($property in @("requiresDotNet", "requiresDocker", "requiresPwsh", "requiresBash")) {
    Assert-RequiredProperty -Object $manifest.dependencies -Name $property
    if ($manifest.dependencies.$property -isnot [bool]) {
        throw "dependencies.$property must be a boolean."
    }
}

if ($manifest.schemaVersion -eq "protocol-lab-package-v2" -and
    ($manifest.dependencies.PSObject.Properties.Name -contains "requiresGo") -and
    $manifest.dependencies.requiresGo -isnot [bool]) {
    throw "dependencies.requiresGo must be a boolean."
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $safeVersion = ([string]$manifest.packageVersion) -replace "[^A-Za-z0-9_.-]", "-"
    $OutputPath = Join-Path (Get-Location) "$($manifest.packageId).$safeVersion.plabpkg"
}

if ([System.IO.Path]::GetExtension($OutputPath) -ne ".plabpkg") {
    throw "OutputPath must end with .plabpkg: $OutputPath"
}

$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($outputFullPath)
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

if ((Test-Path -LiteralPath $outputFullPath) -and -not $Force) {
    throw "Output package already exists. Use -Force to overwrite: $outputFullPath"
}

$tempZipPath = [System.IO.Path]::ChangeExtension([System.IO.Path]::GetTempFileName(), ".zip")
Remove-Item -LiteralPath $tempZipPath -Force -ErrorAction SilentlyContinue

try {
    $sourceChildren = @(Get-ChildItem -LiteralPath $sourceRoot -Force | ForEach-Object { $_.FullName })
    if ($sourceChildren.Count -eq 0) {
        throw "Package source directory is empty: $sourceRoot"
    }

    Compress-Archive -LiteralPath $sourceChildren -DestinationPath $tempZipPath -CompressionLevel Optimal -Force
    Move-Item -LiteralPath $tempZipPath -Destination $outputFullPath -Force
}
finally {
    Remove-Item -LiteralPath $tempZipPath -Force -ErrorAction SilentlyContinue
}

$hash = (Get-FileHash -LiteralPath $outputFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
$result = [ordered]@{
    schemaVersion = [string]$manifest.schemaVersion
    packageId = [string]$manifest.packageId
    packageVersion = [string]$manifest.packageVersion
    kind = [string]$manifest.kind
    sha256 = $hash
    path = $outputFullPath
    entryManifests = @($manifest.entryManifests)
}

$result | ConvertTo-Json -Depth 8
