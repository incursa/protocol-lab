<#
.SYNOPSIS
Uploads a staged ProtocolLab public report bundle to Cloudflare R2.

.DESCRIPTION
Uploads a public-safe bundle under public/runs/{runId}/ in the
protocol-lab-reports R2 bucket. The script can restage the bundle from a
completed run by invoking the existing publish-report command first.

This script uploads only run-prefix R2 objects. The downstream site owns
processing and indexing after R2 has the run-prefix objects.

.PARAMETER BundleRoot
Staged publication bundle root. Defaults to .artifacts/publication/{runId} when
RunRoot or RunId is supplied.

.PARAMETER RunRoot
Optional completed run root under .artifacts/runs/{runId}. When supplied, the
script restages the public-safe bundle before upload.

.PARAMETER RunId
Completed run identifier. When omitted, the script derives the run ID from
RunRoot or BundleRoot.

.PARAMETER BucketName
Cloudflare R2 bucket that stores public report bundles.

.PARAMETER Prefix
R2 object prefix. Defaults to public/runs/{runId}/ and must remain under that
run prefix.

.PARAMETER UploadConcurrency
Maximum number of concurrent object uploads used by the Python helper.

.PARAMETER VerifyUploadedObjects
After upload, verify each object exists and parse key JSON objects.

.PARAMETER DryRun
Validate the bundle and print the planned object layout without requiring R2
credentials or uploading objects.

.PARAMETER R2CredentialsPath
Optional path to a local credentials file outside source control. When omitted,
the script checks the path named by PROTOCOL_LAB_R2_CREDENTIALS_PATH.

.PARAMETER R2SecretVault
Optional PowerShell SecretManagement vault name for local credentials.
#>
[CmdletBinding()]
param(
    [string]$BundleRoot,
    [string]$RunRoot,
    [string]$RunId,
    [string]$BucketName = "protocol-lab-reports",
    [string]$Prefix,
    [ValidateRange(1, 64)]
    [int]$UploadConcurrency = 8,
    [string]$PythonPath = "python",
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

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$CliProject = Join-Path $RepoRoot "src\Incursa.ProtocolLab.Cli"
$PublicationBundleRoot = Join-Path $RepoRoot ".artifacts\publication"

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

function Invoke-Tool {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory
    )

    Push-Location $WorkingDirectory
    try {
        Write-Host "$FilePath $($Arguments -join ' ')"
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$FilePath exited with code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Get-ObjectContentType {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $extension = [System.IO.Path]::GetExtension($RelativePath).ToLowerInvariant()
    switch ($extension) {
        ".json" { return "application/json" }
        ".md" { return "text/markdown; charset=utf-8" }
        ".txt" { return "text/plain; charset=utf-8" }
        ".log" { return "text/plain; charset=utf-8" }
        ".jsonl" { return "application/x-ndjson" }
        default { return "application/octet-stream" }
    }
}

function Get-ObjectCacheControl {
    param([Parameter(Mandatory = $true)][string]$ObjectKey)

    if ($ObjectKey.EndsWith(".json", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "public, max-age=60"
    }

    return "public, max-age=31536000, immutable"
}

function Get-BundleRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    $rootUri = [System.Uri]::new($rootFull)
    $pathUri = [System.Uri]::new($pathFull)
    $relativeUri = $rootUri.MakeRelativeUri($pathUri)
    return [System.Uri]::UnescapeDataString($relativeUri.ToString()) -replace '/', [System.IO.Path]::DirectorySeparatorChar
}

function Read-JsonFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required JSON file not found: $Path"
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Failed to parse JSON file '$Path': $($_.Exception.Message)"
    }
}

function Normalize-R2Prefix {
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$RunId
    )

    $normalized = ($Value -replace '\\', '/').Trim('/')
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        $normalized = "public/runs/$RunId"
    }

    $normalized = "$normalized/"
    $expected = "public/runs/$RunId/"
    if (-not [string]::Equals($normalized, $expected, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "R2 upload prefix must be '$expected'. The downstream site owns registry and latest processing."
    }

    return $normalized
}

function Assert-BundleShape {
    param(
        [Parameter(Mandatory = $true)][string]$BundleRoot,
        [Parameter(Mandatory = $true)][string]$RunId,
        [Parameter(Mandatory = $true)][string]$Prefix
    )

    $manifest = Read-JsonFile (Join-Path $BundleRoot "publication-manifest.json")
    $entry = Read-JsonFile (Join-Path $BundleRoot "report-index-entry.json")
    $artifactsIndex = Read-JsonFile (Join-Path $BundleRoot "artifacts-index.json")
    $evidenceReport = Read-JsonFile (Join-Path $BundleRoot "evidence-report-v1.json")

    if ($manifest.runId -ne $RunId -or $entry.runId -ne $RunId -or $artifactsIndex.runId -ne $RunId -or $evidenceReport.runId -ne $RunId) {
        throw "Staged bundle contains mismatched run identifiers."
    }

    if ($entry.bundlePrefix -ne $Prefix) {
        throw "Registry entry bundle prefix is '$($entry.bundlePrefix)', expected '$Prefix'."
    }

    $requiredRootObjects = @(
        "evidence-report-v1.json",
        "evidence-report-v1.md",
        "artifacts-index.json",
        "publication-manifest.json",
        "publication-warnings.md",
        "publication-skipped.md",
        "report-index-entry.json",
        "report-index.json"
    )

    foreach ($objectName in $requiredRootObjects) {
        $path = Join-Path $BundleRoot $objectName
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Publication bundle is missing required file: $path"
        }
    }

    if ($artifactsIndex.artifactRootKey -ne "artifacts" -or $entry.artifactRootKey -ne "artifacts") {
        throw "Publication bundle artifact root key is invalid."
    }

    return [pscustomobject]@{
        Manifest = $manifest
        Entry = $entry
        ArtifactsIndex = $artifactsIndex
        EvidenceReport = $evidenceReport
    }
}

function New-UploadEntries {
    param(
        [Parameter(Mandatory = $true)][string]$BundleRoot,
        [Parameter(Mandatory = $true)][string]$RunId,
        [Parameter(Mandatory = $true)][string]$Prefix,
        [Parameter(Mandatory = $true)]$ArtifactsIndex
    )

    $files = @(Get-ChildItem -LiteralPath $BundleRoot -File -Recurse | Sort-Object FullName)
    if ($files.Count -lt 1) {
        throw "Publication bundle contains no files: $BundleRoot"
    }

    $copiedArtifactKeys = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($cell in @($ArtifactsIndex.Cells)) {
        foreach ($file in @($cell.Files)) {
            if ($file.Exists -and -not [string]::IsNullOrWhiteSpace([string]$file.Path)) {
                $artifactRelativePath = ([string]$file.Path) -replace '\\', '/'
                if ($artifactRelativePath.StartsWith("/", [System.StringComparison]::Ordinal) -or
                    $artifactRelativePath.IndexOf("../", [System.StringComparison]::Ordinal) -ge 0 -or
                    $artifactRelativePath.IndexOf("/..", [System.StringComparison]::Ordinal) -ge 0) {
                    throw "Copied artifact path is not a bundle-relative path: $artifactRelativePath"
                }

                $artifactPath = Join-Path $BundleRoot ($artifactRelativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
                if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
                    throw "Copied artifact referenced by artifacts-index.json is missing from the bundle: $artifactRelativePath"
                }

                $copiedArtifactKeys.Add($Prefix + $artifactRelativePath) | Out-Null
            }
        }
    }

    $entries = New-Object System.Collections.Generic.List[object]
    foreach ($file in $files) {
        $relativePath = (Get-BundleRelativePath -Root $BundleRoot -Path $file.FullName) -replace '\\', '/'
        $objectKey = $Prefix + $relativePath
        if (-not $objectKey.StartsWith($Prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Object key escaped the run prefix: $objectKey"
        }

        $verifyJson = $relativePath -in @(
            "evidence-report-v1.json",
            "artifacts-index.json",
            "publication-manifest.json",
            "report-index-entry.json",
            "report-index.json"
        )

        $required = $verifyJson -or $copiedArtifactKeys.Contains($objectKey) -or
            $relativePath -in @("evidence-report-v1.md", "publication-warnings.md", "publication-skipped.md")

        $entries.Add([ordered]@{
            source = $file.FullName
            relativePath = $relativePath
            key = $objectKey
            contentType = Get-ObjectContentType -RelativePath $relativePath
            cacheControl = Get-ObjectCacheControl -ObjectKey $objectKey
            verifyJson = $verifyJson
            expectedRunId = if ($verifyJson) { $RunId } else { $null }
            required = $required
        }) | Out-Null
    }

    $plannedKeys = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $entries) {
        $plannedKeys.Add([string]$entry.key) | Out-Null
    }

    foreach ($copiedArtifactKey in $copiedArtifactKeys) {
        if (-not $plannedKeys.Contains($copiedArtifactKey)) {
            throw "Copied artifact referenced by artifacts-index.json was not planned for upload: $copiedArtifactKey"
        }
    }

    return $entries
}

function Get-EnvironmentValue {
    param([Parameter(Mandatory = $true)][string]$Name)

    return [Environment]::GetEnvironmentVariable($Name)
}

function Set-R2EnvironmentValue {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$Value
    )

    if (-not [string]::IsNullOrWhiteSpace($Value)) {
        [Environment]::SetEnvironmentVariable($Name, $Value)
    }
}

function Test-R2UploadEnvironment {
    $hasAccessKey = -not [string]::IsNullOrWhiteSpace((Get-EnvironmentValue -Name "AWS_ACCESS_KEY_ID"))
    $hasSecretKey = -not [string]::IsNullOrWhiteSpace((Get-EnvironmentValue -Name "AWS_SECRET_ACCESS_KEY"))
    $hasEndpoint = -not [string]::IsNullOrWhiteSpace((Get-EnvironmentValue -Name "R2_ENDPOINT"))
    $hasAccountId = -not [string]::IsNullOrWhiteSpace((Get-EnvironmentValue -Name "CLOUDFLARE_ACCOUNT_ID"))

    return $hasAccessKey -and $hasSecretKey -and ($hasEndpoint -or $hasAccountId)
}

function Set-DefaultR2Region {
    if ([string]::IsNullOrWhiteSpace((Get-EnvironmentValue -Name "AWS_DEFAULT_REGION"))) {
        Set-R2EnvironmentValue -Name "AWS_DEFAULT_REGION" -Value "auto"
    }
}

function ConvertTo-SecretText {
    param($Value)

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [System.Security.SecureString]) {
        $credential = [pscredential]::new("secret", $Value)
        return $credential.GetNetworkCredential().Password
    }

    return [string]$Value
}

function Get-SecretManagementValue {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$VaultName
    )

    $getSecret = Get-Command -Name Get-Secret -ErrorAction SilentlyContinue
    if ($null -eq $getSecret) {
        return $null
    }

    $arguments = @{
        Name = $Name
        ErrorAction = "SilentlyContinue"
    }

    if (-not [string]::IsNullOrWhiteSpace($VaultName)) {
        $arguments["Vault"] = $VaultName
    }

    try {
        if ($getSecret.Parameters.ContainsKey("AsPlainText")) {
            $arguments["AsPlainText"] = $true
        }

        return ConvertTo-SecretText -Value (Get-Secret @arguments)
    }
    catch {
        return $null
    }
}

function Set-R2EnvironmentFromValues {
    param([hashtable]$Values)

    if ($Values.ContainsKey("R2_ACCESS_KEY_ID") -and -not [string]::IsNullOrWhiteSpace($Values["R2_ACCESS_KEY_ID"])) {
        Set-R2EnvironmentValue -Name "AWS_ACCESS_KEY_ID" -Value $Values["R2_ACCESS_KEY_ID"]
    }
    elseif ($Values.ContainsKey("AWS_ACCESS_KEY_ID")) {
        Set-R2EnvironmentValue -Name "AWS_ACCESS_KEY_ID" -Value $Values["AWS_ACCESS_KEY_ID"]
    }

    if ($Values.ContainsKey("R2_SECRET_ACCESS_KEY") -and -not [string]::IsNullOrWhiteSpace($Values["R2_SECRET_ACCESS_KEY"])) {
        Set-R2EnvironmentValue -Name "AWS_SECRET_ACCESS_KEY" -Value $Values["R2_SECRET_ACCESS_KEY"]
    }
    elseif ($Values.ContainsKey("AWS_SECRET_ACCESS_KEY")) {
        Set-R2EnvironmentValue -Name "AWS_SECRET_ACCESS_KEY" -Value $Values["AWS_SECRET_ACCESS_KEY"]
    }

    if ($Values.ContainsKey("AWS_SESSION_TOKEN")) {
        Set-R2EnvironmentValue -Name "AWS_SESSION_TOKEN" -Value $Values["AWS_SESSION_TOKEN"]
    }
    elseif ($Values.ContainsKey("R2_SESSION_TOKEN")) {
        Set-R2EnvironmentValue -Name "AWS_SESSION_TOKEN" -Value $Values["R2_SESSION_TOKEN"]
    }

    if ($Values.ContainsKey("CLOUDFLARE_ACCOUNT_ID")) {
        Set-R2EnvironmentValue -Name "CLOUDFLARE_ACCOUNT_ID" -Value $Values["CLOUDFLARE_ACCOUNT_ID"]
    }

    if ($Values.ContainsKey("R2_ENDPOINT")) {
        Set-R2EnvironmentValue -Name "R2_ENDPOINT" -Value $Values["R2_ENDPOINT"]
    }

    Set-DefaultR2Region
}

function Assert-R2UploadEnvironment {
    if (-not (Test-R2UploadEnvironment)) {
        throw "R2 upload credentials are incomplete. Set AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY, and either CLOUDFLARE_ACCOUNT_ID or R2_ENDPOINT; or provide a credentials file with -R2CredentialsPath / $R2CredentialsPathEnvironmentVariable; or store them with PowerShell SecretManagement."
    }
}

function Set-R2EnvironmentFromFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "R2 credentials file was not found: $Path"
    }

    $values = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
            continue
        }

        if ($trimmed -match '^([^=:\s]+)\s*[=:]\s*(.+)$') {
            $values[$matches[1]] = $matches[2].Trim().Trim('"')
        }
    }

    Set-R2EnvironmentFromValues -Values $values
    Assert-R2UploadEnvironment
}

function Set-R2EnvironmentFromSecretManagement {
    $values = @{}
    $secretMappings = @(
        @{ SecretName = $R2AccessKeyIdSecretName; EnvironmentName = "AWS_ACCESS_KEY_ID" },
        @{ SecretName = $R2SecretAccessKeySecretName; EnvironmentName = "AWS_SECRET_ACCESS_KEY" },
        @{ SecretName = $CloudflareAccountIdSecretName; EnvironmentName = "CLOUDFLARE_ACCOUNT_ID" },
        @{ SecretName = $R2EndpointSecretName; EnvironmentName = "R2_ENDPOINT" },
        @{ SecretName = $R2SessionTokenSecretName; EnvironmentName = "AWS_SESSION_TOKEN" }
    )

    foreach ($mapping in $secretMappings) {
        if ([string]::IsNullOrWhiteSpace($mapping.SecretName)) {
            continue
        }

        $secretValue = Get-SecretManagementValue -Name $mapping.SecretName -VaultName $R2SecretVault
        if (-not [string]::IsNullOrWhiteSpace($secretValue)) {
            $values[$mapping.EnvironmentName] = $secretValue
        }
    }

    if ($values.Count -eq 0) {
        return $false
    }

    Set-R2EnvironmentFromValues -Values $values
    return (Test-R2UploadEnvironment)
}

function Initialize-R2UploadEnvironment {
    if (Test-R2UploadEnvironment) {
        Set-DefaultR2Region
        Write-Host "  R2 credentials: environment variables"
        return
    }

    $credentialPath = $null
    if (-not [string]::IsNullOrWhiteSpace($R2CredentialsPath)) {
        $credentialPath = $R2CredentialsPath
    }
    elseif (-not [string]::IsNullOrWhiteSpace($R2CredentialsPathEnvironmentVariable)) {
        $credentialPath = Get-EnvironmentValue -Name $R2CredentialsPathEnvironmentVariable
    }

    if (-not [string]::IsNullOrWhiteSpace($credentialPath)) {
        $resolvedCredentialPath = Resolve-AbsolutePath -Path $credentialPath -BasePath $RepoRoot
        Set-R2EnvironmentFromFile -Path $resolvedCredentialPath
        Write-Host "  R2 credentials: $resolvedCredentialPath"
        return
    }

    if (Set-R2EnvironmentFromSecretManagement) {
        Set-DefaultR2Region
        Write-Host "  R2 credentials: PowerShell SecretManagement"
        return
    }

    Assert-R2UploadEnvironment
}

if ([string]::IsNullOrWhiteSpace($RunRoot) -and [string]::IsNullOrWhiteSpace($BundleRoot) -and [string]::IsNullOrWhiteSpace($RunId)) {
    throw "Specify -BundleRoot, -RunRoot, or -RunId."
}

$PublicationBundleRoot = Resolve-AbsolutePath -Path $PublicationBundleRoot -BasePath $RepoRoot

if (-not [string]::IsNullOrWhiteSpace($RunRoot)) {
    $RunRoot = Resolve-AbsolutePath -Path $RunRoot -BasePath $RepoRoot
    if (-not (Test-Path -LiteralPath $RunRoot -PathType Container)) {
        throw "Run root not found: $RunRoot"
    }

    if ([string]::IsNullOrWhiteSpace($RunId)) {
        $RunId = [System.IO.Path]::GetFileName($RunRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar))
    }
}

if ([string]::IsNullOrWhiteSpace($BundleRoot)) {
    if ([string]::IsNullOrWhiteSpace($RunId)) {
        throw "Unable to infer RunId before defaulting BundleRoot."
    }

    $BundleRoot = Join-Path $PublicationBundleRoot $RunId
}

$BundleRoot = Resolve-AbsolutePath -Path $BundleRoot -BasePath $RepoRoot

if ([string]::IsNullOrWhiteSpace($RunId)) {
    $RunId = [System.IO.Path]::GetFileName($BundleRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar))
}

$bundleFolderName = [System.IO.Path]::GetFileName($BundleRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar))
if (-not [string]::Equals($bundleFolderName, $RunId, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Bundle root '$BundleRoot' does not match run id '$RunId'."
}

if (-not [string]::IsNullOrWhiteSpace($RunRoot)) {
    $publishArgs = @(
        "run", "--project", $CliProject, "--",
        "publish-report",
        "--run", $RunRoot,
        "--output", $BundleRoot,
        "--visibility", "public"
    )
    if ($AllowDiagnosticPublication) {
        $publishArgs += "--allow-diagnostic-publication"
    }

    Invoke-Tool -FilePath "dotnet" -Arguments $publishArgs -WorkingDirectory $RepoRoot
}

if (-not (Test-Path -LiteralPath $BundleRoot -PathType Container)) {
    throw "Publication bundle not found: $BundleRoot"
}

$Prefix = if ([string]::IsNullOrWhiteSpace($Prefix)) { "public/runs/$RunId/" } else { $Prefix }
$Prefix = Normalize-R2Prefix -Value $Prefix -RunId $RunId
$bundleShape = Assert-BundleShape -BundleRoot $BundleRoot -RunId $RunId -Prefix $Prefix
$entries = @(New-UploadEntries -BundleRoot $BundleRoot -RunId $RunId -Prefix $Prefix -ArtifactsIndex $bundleShape.ArtifactsIndex)

Write-Host "ProtocolLab R2 bundle upload"
Write-Host "  run id: $RunId"
Write-Host "  bundle root: $BundleRoot"
Write-Host "  R2 bucket: $BucketName"
Write-Host "  R2 prefix: $Prefix"
Write-Host "  object count: $($entries.Count)"

if ($DryRun) {
    Write-Host "Dry run completed. Planned objects:"
    foreach ($entry in $entries) {
        Write-Host "  $($entry.key)"
    }
    return
}

Initialize-R2UploadEnvironment

if (-not (Get-Command $PythonPath -ErrorAction SilentlyContinue)) {
    throw "python was not found on PATH. Install Python or pass -PythonPath."
}

Invoke-Tool -FilePath $PythonPath -Arguments @("-c", "import boto3") -WorkingDirectory $RepoRoot

$tempRoot = Join-Path $env:TEMP ("protocol-lab-r2-upload-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force $tempRoot | Out-Null
try {
    $uploadManifestPath = Join-Path $tempRoot "upload-manifest.json"
    [ordered]@{
        bucket = $BucketName
        prefix = $Prefix
        runId = $RunId
        concurrency = $UploadConcurrency
        verify = [bool]$VerifyUploadedObjects
        entries = $entries
    } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $uploadManifestPath -Encoding utf8NoBOM

    $helperScript = Join-Path $PSScriptRoot "r2_s3_helper.py"
    if (-not (Test-Path -LiteralPath $helperScript)) {
        throw "R2 helper script not found: $helperScript"
    }

    Invoke-Tool -FilePath $PythonPath -Arguments @($helperScript, "upload-manifest", $uploadManifestPath) -WorkingDirectory $RepoRoot
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

Write-Host "R2 upload complete."
Write-Host "  R2 prefix: $Prefix"
