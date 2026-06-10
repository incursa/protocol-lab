# Copyright (c) 2026 Incursa LLC.
# Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

[CmdletBinding(DefaultParameterSetName = "UploadPackage")]
param(
    [Parameter(Mandatory = $true)]
    [string] $ControllerUri,

    [Parameter(Mandatory = $true, ParameterSetName = "UploadPackage")]
    [string] $PackagePath,

    [Parameter(Mandatory = $true, ParameterSetName = "ExistingPackage")]
    [string] $PackageId,

    [Parameter(Mandatory = $true, ParameterSetName = "ExistingPackage")]
    [string] $PackageVersion,

    [Parameter(Mandatory = $true, ParameterSetName = "ExistingPackage")]
    [string] $Sha256,

    [Parameter(Mandatory = $true)]
    [string] $ImplementationId,

    [Parameter(Mandatory = $true)]
    [string] $TestExecutorId,

    [string[]] $AdditionalPackagePath = @(),

    [string[]] $PackageReference = @(),

    [string] $SuiteId = "h3-local-v1",

    [string[]] $ScenarioId = @("http.core.plaintext"),

    [string] $Protocol = "h3",

    [string] $LoadProfileId = "smoke",

    [string] $ExecutionMode = "process",

    [string[]] $RequiredCapability = @(),

    [int] $TimeoutSeconds = 1800,

    [int] $PollSeconds = 5,

    [string] $ArtifactOutputPath,

    [switch] $NoWait
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Join-ControllerUri {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BaseUri,

        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    return $BaseUri.TrimEnd("/") + "/" + $Path.TrimStart("/")
}

function ConvertTo-AbsoluteControllerUri {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BaseUri,

        [Parameter(Mandatory = $true)]
        [string] $MaybeRelativeUri
    )

    $parsed = $null
    if ([System.Uri]::TryCreate($MaybeRelativeUri, [System.UriKind]::Absolute, [ref]$parsed)) {
        return $MaybeRelativeUri
    }

    return Join-ControllerUri -BaseUri $BaseUri -Path $MaybeRelativeUri
}

function Get-JobId {
    param([Parameter(Mandatory = $true)][object] $Response)

    foreach ($name in @("jobId", "id")) {
        if ($Response.PSObject.Properties.Name -contains $name -and -not [string]::IsNullOrWhiteSpace([string]$Response.$name)) {
            return [string]$Response.$name
        }
    }

    throw "Job submission response did not include jobId or id."
}

function Get-JobStatus {
    param([Parameter(Mandatory = $true)][object] $Job)

    foreach ($name in @("status", "state")) {
        if ($Job.PSObject.Properties.Name -contains $name -and -not [string]::IsNullOrWhiteSpace([string]$Job.$name)) {
            return [string]$Job.$name
        }
    }

    return ""
}

function Get-ArtifactDownloadUri {
    param([Parameter(Mandatory = $true)][object] $Job)

    foreach ($name in @("artifactArchiveUrl", "artifactsDownloadUrl", "downloadUrl")) {
        if ($Job.PSObject.Properties.Name -contains $name -and -not [string]::IsNullOrWhiteSpace([string]$Job.$name)) {
            return [string]$Job.$name
        }
    }

    if ($Job.PSObject.Properties.Name -contains "artifacts" -and $null -ne $Job.artifacts) {
        foreach ($name in @("archiveUrl", "downloadUrl")) {
            if ($Job.artifacts.PSObject.Properties.Name -contains $name -and -not [string]::IsNullOrWhiteSpace([string]$Job.artifacts.$name)) {
                return [string]$Job.artifacts.$name
            }
        }
    }

    return $null
}

function Send-PackageArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Controller
    )

    $packageFullPath = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path)
    if ([System.IO.Path]::GetExtension($packageFullPath) -ne ".plabpkg") {
        throw "PackagePath must end with .plabpkg: $packageFullPath"
    }

    $uploadUri = Join-ControllerUri -BaseUri $Controller -Path "/api/lab/packages"
    $uploadResponse = Invoke-RestMethod -Method Post -Uri $uploadUri -Form @{
        file = Get-Item -LiteralPath $packageFullPath
    }

    foreach ($name in @("packageId", "packageVersion", "sha256")) {
        if (-not ($uploadResponse.PSObject.Properties.Name -contains $name)) {
            throw "Package upload response did not include '$name'."
        }
    }

    return [ordered]@{
        packageId = [string]$uploadResponse.packageId
        packageVersion = [string]$uploadResponse.packageVersion
        sha256 = [string]$uploadResponse.sha256
    }
}

$controller = $ControllerUri.TrimEnd("/")

if ($PSCmdlet.ParameterSetName -eq "UploadPackage") {
    $primaryPackage = Send-PackageArchive -Path $PackagePath -Controller $controller
    $PackageId = $primaryPackage.packageId
    $PackageVersion = $primaryPackage.packageVersion
    $Sha256 = $primaryPackage.sha256
}

$packages = @(
    [ordered]@{
        packageId = $PackageId
        packageVersion = $PackageVersion
        sha256 = $Sha256
    }
)

foreach ($additionalPath in $AdditionalPackagePath) {
    $packages += Send-PackageArchive -Path $additionalPath -Controller $controller
}

foreach ($reference in $PackageReference) {
    $parts = $reference -split ":", 3
    if ($parts.Count -ne 3 -or [string]::IsNullOrWhiteSpace($parts[0]) -or [string]::IsNullOrWhiteSpace($parts[1]) -or [string]::IsNullOrWhiteSpace($parts[2])) {
        throw "PackageReference values must use packageId:packageVersion:sha256 form. Invalid value: '$reference'."
    }

    $packages += [ordered]@{
        packageId = $parts[0]
        packageVersion = $parts[1]
        sha256 = $parts[2]
    }
}

$jobBody = [ordered]@{
    kind = "single-node-benchmark"
    suiteIds = @($SuiteId)
    implementationIds = @($ImplementationId)
    testExecutorIds = @($TestExecutorId)
    scenarioIds = @($ScenarioId)
    protocols = @($Protocol)
    loadProfileId = $LoadProfileId
    targetMode = $ExecutionMode
    requiredCapabilities = @($RequiredCapability)
    packages = $packages
}

$jobUri = Join-ControllerUri -BaseUri $controller -Path "/api/lab/jobs"
$jobResponse = Invoke-RestMethod -Method Post -Uri $jobUri -ContentType "application/json" -Body ($jobBody | ConvertTo-Json -Depth 16)
$jobId = Get-JobId -Response $jobResponse

if ($NoWait) {
    [ordered]@{
        jobId = $jobId
        packageId = $PackageId
        packageVersion = $PackageVersion
        sha256 = $Sha256
        packages = $packages
        status = Get-JobStatus -Job $jobResponse
    } | ConvertTo-Json -Depth 8
    return
}

$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
$lastJob = $jobResponse
do {
    Start-Sleep -Seconds $PollSeconds
    $lastJob = Invoke-RestMethod -Method Get -Uri (Join-ControllerUri -BaseUri $controller -Path "/api/lab/jobs/$jobId")
    $status = Get-JobStatus -Job $lastJob

    if ($status -in @("Completed", "Failed", "Canceled", "Cancelled", "TimedOut")) {
        break
    }
} while ([DateTimeOffset]::UtcNow -lt $deadline)

$finalStatus = Get-JobStatus -Job $lastJob
if ($finalStatus -notin @("Completed", "Failed", "Canceled", "Cancelled", "TimedOut")) {
    throw "Timed out waiting for job '$jobId'. Last status: '$finalStatus'."
}

$downloadedArtifactPath = $null
$artifactDownloadUri = Get-ArtifactDownloadUri -Job $lastJob
if (-not [string]::IsNullOrWhiteSpace($ArtifactOutputPath) -and -not [string]::IsNullOrWhiteSpace($artifactDownloadUri)) {
    $artifactFullPath = [System.IO.Path]::GetFullPath($ArtifactOutputPath)
    $artifactDirectory = [System.IO.Path]::GetDirectoryName($artifactFullPath)
    if (-not [string]::IsNullOrWhiteSpace($artifactDirectory)) {
        New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
    }

    Invoke-WebRequest -Uri (ConvertTo-AbsoluteControllerUri -BaseUri $controller -MaybeRelativeUri $artifactDownloadUri) -OutFile $artifactFullPath
    $downloadedArtifactPath = $artifactFullPath
}

[ordered]@{
    jobId = $jobId
    status = $finalStatus
    packageId = $PackageId
    packageVersion = $PackageVersion
    sha256 = $Sha256
    packages = $packages
    artifactDownloadUri = $artifactDownloadUri
    artifactPath = $downloadedArtifactPath
    job = $lastJob
} | ConvertTo-Json -Depth 32
