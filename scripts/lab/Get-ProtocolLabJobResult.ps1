# Copyright (c) 2026 Incursa LLC.
# Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ControllerUri,

    [Parameter(Mandatory = $true)]
    [string] $JobId,

    [string] $ArtifactOutputPath
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

$controller = $ControllerUri.TrimEnd("/")
$job = Invoke-RestMethod -Method Get -Uri (Join-ControllerUri -BaseUri $controller -Path "/api/lab/jobs/$JobId")
$downloadedArtifactPath = $null
$artifactDownloadUri = Get-ArtifactDownloadUri -Job $job

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
    jobId = $JobId
    artifactDownloadUri = $artifactDownloadUri
    artifactPath = $downloadedArtifactPath
    job = $job
} | ConvertTo-Json -Depth 32
