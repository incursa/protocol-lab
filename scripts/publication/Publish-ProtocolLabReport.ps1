<#
.SYNOPSIS
Compatibility wrapper for uploading a ProtocolLab public report bundle to R2.

.DESCRIPTION
This wrapper preserves the historical script name while delegating to the
R2-only Upload-ProtocolLabReportBundle.ps1 path. It stages the public-safe
bundle when RunRoot is supplied, then uploads only public/runs/{runId}/ objects
to R2.

This script uploads only run-prefix R2 objects. The downstream site owns
processing and indexing after R2 has the run-prefix objects.
#>
[CmdletBinding()]
param(
    [string]$RunId,
    [string]$RunRoot,
    [string]$BundleRoot,
    [string]$BucketName = "protocol-lab-reports",
    [string]$Prefix,
    [ValidateRange(1, 64)]
    [int]$UploadConcurrency = 8,
    [string]$PythonPath = "python",
    [string]$PowerShellPath = "pwsh",
    [string]$R2CredentialsPath,
    [string]$R2CredentialsPathEnvironmentVariable = "PROTOCOL_LAB_R2_CREDENTIALS_PATH",
    [string]$R2SecretVault,
    [string]$R2AccessKeyIdSecretName = "ProtocolLab-R2-AccessKeyId",
    [string]$R2SecretAccessKeySecretName = "ProtocolLab-R2-SecretAccessKey",
    [string]$CloudflareAccountIdSecretName = "ProtocolLab-CloudflareAccountId",
    [string]$R2EndpointSecretName = "ProtocolLab-R2-Endpoint",
    [string]$R2SessionTokenSecretName = "ProtocolLab-R2-SessionToken",
    [switch]$AllowDiagnosticPublication,
    [switch]$VerifyUploadedObjects,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$uploadScript = Join-Path $PSScriptRoot "Upload-ProtocolLabReportBundle.ps1"
if (-not (Test-Path -LiteralPath $uploadScript)) {
    throw "R2 upload script not found: $uploadScript"
}

$arguments = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $uploadScript,
    "-BucketName", $BucketName,
    "-UploadConcurrency", "$UploadConcurrency",
    "-PythonPath", $PythonPath,
    "-R2CredentialsPathEnvironmentVariable", $R2CredentialsPathEnvironmentVariable,
    "-R2AccessKeyIdSecretName", $R2AccessKeyIdSecretName,
    "-R2SecretAccessKeySecretName", $R2SecretAccessKeySecretName,
    "-CloudflareAccountIdSecretName", $CloudflareAccountIdSecretName,
    "-R2EndpointSecretName", $R2EndpointSecretName,
    "-R2SessionTokenSecretName", $R2SessionTokenSecretName
)

if (-not [string]::IsNullOrWhiteSpace($RunId)) {
    $arguments += @("-RunId", $RunId)
}

if (-not [string]::IsNullOrWhiteSpace($RunRoot)) {
    $arguments += @("-RunRoot", $RunRoot)
}

if (-not [string]::IsNullOrWhiteSpace($BundleRoot)) {
    $arguments += @("-BundleRoot", $BundleRoot)
}

if (-not [string]::IsNullOrWhiteSpace($Prefix)) {
    $arguments += @("-Prefix", $Prefix)
}

if (-not [string]::IsNullOrWhiteSpace($R2CredentialsPath)) {
    $arguments += @("-R2CredentialsPath", $R2CredentialsPath)
}

if (-not [string]::IsNullOrWhiteSpace($R2SecretVault)) {
    $arguments += @("-R2SecretVault", $R2SecretVault)
}

if ($AllowDiagnosticPublication) {
    $arguments += "-AllowDiagnosticPublication"
}

if ($VerifyUploadedObjects) {
    $arguments += "-VerifyUploadedObjects"
}

if ($DryRun) {
    $arguments += "-DryRun"
}

& $PowerShellPath @arguments
if ($LASTEXITCODE -ne 0) {
    throw "R2 publication failed with exit code $LASTEXITCODE."
}
