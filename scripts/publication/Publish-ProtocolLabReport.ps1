<#
.SYNOPSIS
Promotes a completed ProtocolLab run to the public Cloudflare layout.

.DESCRIPTION
Stages the public-safe bundle with the existing publish-report flow, uploads
the bundle under public/runs/{runId}/ in the protocol-lab-reports R2 bucket,
refreshes the public registry objects, and indexes searchable metadata into
D1 through the PROTOCOL_LAB_DB binding.

The script fails closed on malformed bundle metadata, mismatched run IDs,
private-path leaks, or artifact paths that escape the completed run root.

.PARAMETER RunId
Completed run identifier. When omitted, the script derives the run ID from
the supplied run root path.

.PARAMETER RunRoot
Completed run root under .artifacts/runs/{runId}. When omitted, the script
derives it from the run ID.

.PARAMETER BundleRoot
Staged publication bundle root. Defaults to .artifacts/publication/{runId}.

.PARAMETER BucketName
Cloudflare R2 bucket that stores the public bundle.

.PARAMETER D1Binding
D1 binding name used by the site. The default is PROTOCOL_LAB_DB.

.PARAMETER D1DatabaseId
Remote D1 database identifier used by Wrangler.

.PARAMETER D1DatabaseName
Wrangler database name. Defaults to protocol-lab-reports.

.PARAMETER WranglerPath
Path to wrangler or an equivalent executable.

.PARAMETER AllowDiagnosticPublication
Allows DiagnosticOnly bundles to be promoted when the source bundle remains
explicitly labeled diagnostic-only.

.PARAMETER DryRun
Validate the handoff and print the planned object layout without uploading to
R2 or indexing D1.
#>
[CmdletBinding()]
param(
    [string]$RunId,
    [string]$RunRoot,
    [string]$BundleRoot,
    [string]$BucketName = "protocol-lab-reports",
    [string]$D1Binding = "PROTOCOL_LAB_DB",
    [string]$D1DatabaseId,
    [string]$D1DatabaseName = "protocol-lab-reports",
    [string]$WranglerPath = "wrangler",
    [switch]$AllowDiagnosticPublication,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$CliProject = Join-Path $RepoRoot "src\Incursa.ProtocolLab.Cli"
$PublicationBundleRoot = Join-Path $RepoRoot ".artifacts\publication"
$RunArtifactsRoot = Join-Path $RepoRoot ".artifacts\runs"
$SchemaPath = Join-Path $PSScriptRoot "protocol-lab-reports-d1-schema.sql"

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

function Test-IsUnderRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $candidateFull = [System.IO.Path]::GetFullPath($Path)
    return $candidateFull.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)
}

function Invoke-Tool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
    )

    Push-Location $WorkingDirectory
    try {
        Write-Host "$FilePath $($Arguments -join ' ')"
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$FilePath failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function ConvertTo-SqlLiteral {
    param(
        [AllowNull()]
        [object]$Value
    )

    if ($null -eq $Value) {
        return "NULL"
    }

    if ($Value -is [bool]) {
        return $(if ($Value) { "1" } else { "0" })
    }

    if ($Value -is [datetime]) {
        $Value = [DateTimeOffset]$Value
    }

    if ($Value -is [datetimeoffset]) {
        return "'" + $Value.ToString("o") + "'"
    }

    if ($Value -is [byte] -or
        $Value -is [short] -or
        $Value -is [int] -or
        $Value -is [long] -or
        $Value -is [float] -or
        $Value -is [double] -or
        $Value -is [decimal]) {
        return [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0}", $Value)
    }

    return "'" + $Value.ToString().Replace("'", "''") + "'"
}

function New-SqlTuple {
    param([object[]]$Values)

    return "(" + (($Values | ForEach-Object { ConvertTo-SqlLiteral $_ }) -join ", ") + ")"
}

function Read-JsonFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required file not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-ObjectContentType {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $normalized = $RelativePath.ToLowerInvariant()
    if ($normalized.EndsWith(".json")) {
        return "application/json; charset=utf-8"
    }

    if ($normalized.EndsWith(".md")) {
        return "text/markdown; charset=utf-8"
    }

    if ($normalized.EndsWith(".jsonl") -or $normalized.EndsWith(".txt") -or $normalized.EndsWith(".log")) {
        return "text/plain; charset=utf-8"
    }

    return "application/octet-stream"
}

function Get-ObjectCacheControl {
    param([Parameter(Mandatory = $true)][string]$ObjectKey)

    if ($ObjectKey.StartsWith("public/registry/", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "public, max-age=60, must-revalidate"
    }

    return "public, max-age=31536000, immutable"
}

function Read-R2JsonObject {
    param(
        [Parameter(Mandatory = $true)][string]$Bucket,
        [Parameter(Mandatory = $true)][string]$ObjectKey,
        [Parameter(Mandatory = $true)][string]$TempDirectory
    )

    $tempFile = Join-Path $TempDirectory ([System.IO.Path]::GetRandomFileName() + ".json")
    try {
        Push-Location $RepoRoot
        try {
            $commandOutput = & $WranglerPath @("r2", "object", "get", "$Bucket/$ObjectKey", "--file", $tempFile, "--remote") 2>&1
            $exitCode = $LASTEXITCODE
        }
        finally {
            Pop-Location
        }

        if ($exitCode -ne 0) {
            $message = (($commandOutput | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine).Trim()
            if ($message -match '(?i)\b404\b' -or
                $message -match '(?i)\bnot found\b' -or
                $message -match '(?i)\bno such object\b') {
                return $null
            }

            throw "Failed to read R2 object '$Bucket/$ObjectKey': $message"
        }

        if (-not (Test-Path -LiteralPath $tempFile)) {
            throw "R2 object '$Bucket/$ObjectKey' was downloaded but no file was written to '$tempFile'."
        }

        return Get-Content -LiteralPath $tempFile -Raw | ConvertFrom-Json
    }
    catch {
        throw
    }
    finally {
        if (Test-Path -LiteralPath $tempFile) {
            Remove-Item -LiteralPath $tempFile -Force
        }
    }
}

function Write-WranglerConfig {
    param(
        [Parameter(Mandatory = $true)][string]$ConfigPath,
        [Parameter(Mandatory = $true)][string]$AccountId,
        [Parameter(Mandatory = $true)][string]$DatabaseId,
        [Parameter(Mandatory = $true)][string]$DatabaseName,
        [Parameter(Mandatory = $true)][string]$Binding
    )

    $date = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")
    @"
name = "protocol-lab-publication"
compatibility_date = "$date"
account_id = "$AccountId"

[[d1_databases]]
binding = "$Binding"
database_name = "$DatabaseName"
database_id = "$DatabaseId"
"@ | Set-Content -LiteralPath $ConfigPath -Encoding utf8NoBOM
}

function Parse-PublicationWarnings {
    param([Parameter(Mandatory = $true)][string]$WarningsPath)

    if (-not (Test-Path -LiteralPath $WarningsPath)) {
        throw "Required file not found: $WarningsPath"
    }

    $warnings = @()
    $capturing = $false
    foreach ($line in Get-Content -LiteralPath $WarningsPath) {
        if ($line -eq "## Warnings") {
            $capturing = $true
            continue
        }

        if (-not $capturing) {
            continue
        }

        if ([string]::IsNullOrWhiteSpace([string]$line)) {
            continue
        }

        if ($line -match '^\- \[(?<code>[^\]]+)\]\s+(?<message>.*)$') {
            $code = [string]$Matches.code
            $message = [string]$Matches.message
            $source = if ($code -like "report:*") { "report" } else { "plan" }
            $warnings += [pscustomobject]@{
                warningSource = $source
                warningCode = $code
                warningMessage = $message
            }
        }
    }

    return $warnings
}

function Build-RegistryMerge {
    param(
        [Parameter(Mandatory = $true)]$ExistingRegistry,
        [Parameter(Mandatory = $true)]$CurrentEntry
    )

    $entries = New-Object System.Collections.Generic.List[object]
    if ($ExistingRegistry -and $ExistingRegistry.entries) {
        foreach ($entry in @($ExistingRegistry.entries)) {
            if ($entry.runId -and -not [string]::Equals($entry.runId.ToString(), $CurrentEntry.runId.ToString(), [System.StringComparison]::OrdinalIgnoreCase)) {
                $entries.Add($entry) | Out-Null
            }
        }
    }

    $entries.Add($CurrentEntry) | Out-Null
    $sorted = @($entries | Sort-Object `
        @{ Expression = { $_.generatedAt }; Descending = $true }, `
        @{ Expression = { $_.runId }; Descending = $false })

    return [ordered]@{
        schemaVersion = "protocol-lab.public-report-index.v1"
        entries = $sorted
    }
}

function Build-LatestObject {
    param(
        [Parameter(Mandatory = $true)]$Entry,
        [Parameter(Mandatory = $true)]$Manifest,
        [Parameter(Mandatory = $true)]$PublicationWarnings,
        [Parameter(Mandatory = $true)][datetimeoffset]$PublishedAt
    )

    return [ordered]@{
        schemaVersion = "protocol-lab.public-report-latest.v1"
        runId = $Entry.runId
        generatedAt = $Entry.generatedAt
        publishedAt = $PublishedAt.ToString("o")
        claimLevel = $Entry.claimLevel
        publishable = [bool]$Entry.publishable
        diagnosticOnly = [bool]$Entry.diagnosticOnly
        executionProfile = $Entry.executionProfile
        visibility = $Entry.visibility
        sourceKind = $Entry.sourceKind
        evidenceWarningCount = [int]$Entry.warningCount
        publicationWarningCount = [int]$PublicationWarnings.Count
        copiedArtifactCount = [int]$Manifest.copiedArtifactCount
        skippedArtifactCount = [int]$Manifest.skippedArtifactCount
        implementationCount = [int]$Entry.implementations.Count
        scenarioCount = [int]$Entry.scenarios.Count
        protocolCount = [int]$Entry.protocols.Count
        validationPassed = [int]$Manifest.validationCounts.passed
        validationFailed = [int]$Manifest.validationCounts.failed
        validationUnsupported = [int]$Manifest.validationCounts.unsupported
        validationNotApplicable = [int]$Manifest.validationCounts.notApplicable
        validationInconclusive = [int]$Manifest.validationCounts.inconclusive
        validationInfrastructureFailure = [int]$Manifest.validationCounts.infrastructureFailure
        benchmarkAccepted = [int]$Manifest.benchmarkCounts.accepted
        benchmarkRejected = [int]$Manifest.benchmarkCounts.rejected
        benchmarkNotRunValidationFailed = [int]$Manifest.benchmarkCounts.notRunValidationFailed
        benchmarkNotRunUnsupported = [int]$Manifest.benchmarkCounts.notRunUnsupported
        benchmarkNotRunLoadToolFailed = [int]$Manifest.benchmarkCounts.notRunLoadToolFailed
        benchmarkNotRunParserFailed = [int]$Manifest.benchmarkCounts.notRunParserFailed
        artifactRootKey = $Entry.artifactRootKey
        bundlePrefix = $Entry.bundlePrefix
        evidenceReportJsonKey = $Entry.evidenceReportJsonKey
        evidenceReportMarkdownKey = $Entry.evidenceReportMarkdownKey
        artifactsIndexKey = $Entry.artifactsIndexKey
        publicationManifestKey = $Entry.publicationManifestKey
        publicationWarningsKey = $Entry.publicationWarningsKey
        publicationSkippedKey = $Entry.publicationSkippedKey
        reportIndexEntryKey = $Entry.reportIndexEntryKey
        reportIndexKey = $Entry.reportIndexKey
        registryKey = "public/registry/report-index.json"
        latestKey = "public/registry/latest.json"
        implementations = @($Entry.implementations)
        scenarios = @($Entry.scenarios)
        protocols = @($Entry.protocols)
    }
}

function Write-R2Object {
    param(
        [Parameter(Mandatory = $true)][string]$ObjectKey,
        [Parameter(Mandatory = $true)][string]$FilePath
    )

    $contentType = Get-ObjectContentType -RelativePath ([System.IO.Path]::GetFileName($FilePath))
    $cacheControl = Get-ObjectCacheControl -ObjectKey $ObjectKey
    Invoke-Tool -FilePath $WranglerPath -Arguments @(
        "r2", "object", "put", "$BucketName/$ObjectKey",
        "--file", $FilePath,
        "--remote",
        "--force",
        "--content-type", $contentType,
        "--cache-control", $cacheControl
    ) -WorkingDirectory $RepoRoot
}

function Write-D1SqlFile {
    param(
        [Parameter(Mandatory = $true)]$Entry,
        [Parameter(Mandatory = $true)]$Manifest,
        [Parameter(Mandatory = $true)]$Warnings,
        [Parameter(Mandatory = $true)][string]$SqlPath,
        [Parameter(Mandatory = $true)][datetimeoffset]$PublishedAt,
        [Parameter(Mandatory = $true)][bool]$UpdateLatest
    )

    $runColumns = @(
        "run_id",
        "generated_at",
        "published_at",
        "claim_level",
        "publishable",
        "diagnostic_only",
        "execution_profile",
        "visibility",
        "source_kind",
        "evidence_warning_count",
        "publication_warning_count",
        "copied_artifact_count",
        "skipped_artifact_count",
        "implementation_count",
        "scenario_count",
        "protocol_count",
        "validation_passed",
        "validation_failed",
        "validation_unsupported",
        "validation_not_applicable",
        "validation_inconclusive",
        "validation_infrastructure_failure",
        "benchmark_accepted",
        "benchmark_rejected",
        "benchmark_not_run_validation_failed",
        "benchmark_not_run_unsupported",
        "benchmark_not_run_load_tool_failed",
        "benchmark_not_run_parser_failed",
        "artifact_root_key",
        "bundle_prefix",
        "evidence_report_json_key",
        "evidence_report_markdown_key",
        "artifacts_index_key",
        "publication_manifest_key",
        "publication_warnings_key",
        "publication_skipped_key",
        "report_index_entry_key",
        "report_index_key"
    )

    $runValues = @(
        $Entry.runId,
        $Entry.generatedAt,
        $PublishedAt,
        $Entry.claimLevel,
        [bool]$Entry.publishable,
        [bool]$Entry.diagnosticOnly,
        $Entry.executionProfile,
        $Entry.visibility,
        $Entry.sourceKind,
        [int]$Entry.warningCount,
        [int]$Warnings.Count,
        [int]$Manifest.copiedArtifactCount,
        [int]$Manifest.skippedArtifactCount,
        [int]$Entry.implementations.Count,
        [int]$Entry.scenarios.Count,
        [int]$Entry.protocols.Count,
        [int]$Manifest.validationCounts.passed,
        [int]$Manifest.validationCounts.failed,
        [int]$Manifest.validationCounts.unsupported,
        [int]$Manifest.validationCounts.notApplicable,
        [int]$Manifest.validationCounts.inconclusive,
        [int]$Manifest.validationCounts.infrastructureFailure,
        [int]$Manifest.benchmarkCounts.accepted,
        [int]$Manifest.benchmarkCounts.rejected,
        [int]$Manifest.benchmarkCounts.notRunValidationFailed,
        [int]$Manifest.benchmarkCounts.notRunUnsupported,
        [int]$Manifest.benchmarkCounts.notRunLoadToolFailed,
        [int]$Manifest.benchmarkCounts.notRunParserFailed,
        $Entry.artifactRootKey,
        $Entry.bundlePrefix,
        $Entry.evidenceReportJsonKey,
        $Entry.evidenceReportMarkdownKey,
        $Entry.artifactsIndexKey,
        $Entry.publicationManifestKey,
        $Entry.publicationWarningsKey,
        $Entry.publicationSkippedKey,
        $Entry.reportIndexEntryKey,
        $Entry.reportIndexKey
    )

    $runAssignments = @()
    foreach ($column in $runColumns) {
        if ($column -ne "run_id") {
            $runAssignments += "$column = excluded.$column"
        }
    }

    $objectKeys = @(
        [ordered]@{ objectKind = "evidence-report-v1.json"; objectKey = $Entry.evidenceReportJsonKey },
        [ordered]@{ objectKind = "evidence-report-v1.md"; objectKey = $Entry.evidenceReportMarkdownKey },
        [ordered]@{ objectKind = "artifacts-index.json"; objectKey = $Entry.artifactsIndexKey },
        [ordered]@{ objectKind = "publication-manifest.json"; objectKey = $Entry.publicationManifestKey },
        [ordered]@{ objectKind = "publication-warnings.md"; objectKey = $Entry.publicationWarningsKey },
        [ordered]@{ objectKind = "publication-skipped.md"; objectKey = $Entry.publicationSkippedKey },
        [ordered]@{ objectKind = "report-index-entry.json"; objectKey = $Entry.reportIndexEntryKey },
        [ordered]@{ objectKind = "report-index.json"; objectKey = $Entry.reportIndexKey }
    )

    $implementationRows = @($Entry.implementations | ForEach-Object { New-SqlTuple @($Entry.runId, $_) })
    $scenarioRows = @($Entry.scenarios | ForEach-Object { New-SqlTuple @($Entry.runId, $_) })
    $protocolRows = @($Entry.protocols | ForEach-Object { New-SqlTuple @($Entry.runId, $_) })

    $warningRows = New-Object System.Collections.Generic.List[string]
    $warningIndex = 1
    foreach ($warning in $Warnings) {
        $warningRows.Add((New-SqlTuple @($Entry.runId, $warningIndex, $warning.warningSource, $warning.warningCode, $warning.warningMessage))) | Out-Null
        $warningIndex++
    }

    $objectKeyRows = @($objectKeys | ForEach-Object { New-SqlTuple @($Entry.runId, $_.objectKind, $_.objectKey) })

    $statements = New-Object System.Collections.Generic.List[string]
    $statements.Add("BEGIN;") | Out-Null
    $statements.Add(("INSERT INTO public_report_runs (" + ($runColumns -join ", ") + ") VALUES " + (New-SqlTuple $runValues) + " ON CONFLICT(run_id) DO UPDATE SET " + ($runAssignments -join ", ") + ";")) | Out-Null

    if ($implementationRows.Count -gt 0) {
        $statements.Add("DELETE FROM public_report_run_implementations WHERE run_id = " + (ConvertTo-SqlLiteral $Entry.runId) + ";") | Out-Null
        $statements.Add("INSERT INTO public_report_run_implementations (run_id, implementation_id) VALUES " + ($implementationRows -join ", ") + ";") | Out-Null
    }

    if ($scenarioRows.Count -gt 0) {
        $statements.Add("DELETE FROM public_report_run_scenarios WHERE run_id = " + (ConvertTo-SqlLiteral $Entry.runId) + ";") | Out-Null
        $statements.Add("INSERT INTO public_report_run_scenarios (run_id, scenario_id) VALUES " + ($scenarioRows -join ", ") + ";") | Out-Null
    }

    if ($protocolRows.Count -gt 0) {
        $statements.Add("DELETE FROM public_report_run_protocols WHERE run_id = " + (ConvertTo-SqlLiteral $Entry.runId) + ";") | Out-Null
        $statements.Add("INSERT INTO public_report_run_protocols (run_id, protocol_id) VALUES " + ($protocolRows -join ", ") + ";") | Out-Null
    }

    $statements.Add("DELETE FROM public_report_run_warnings WHERE run_id = " + (ConvertTo-SqlLiteral $Entry.runId) + ";") | Out-Null
    if ($warningRows.Count -gt 0) {
        $statements.Add("INSERT INTO public_report_run_warnings (run_id, warning_index, warning_source, warning_code, warning_message) VALUES " + ($warningRows -join ", ") + ";") | Out-Null
    }

    $statements.Add("DELETE FROM public_report_run_object_keys WHERE run_id = " + (ConvertTo-SqlLiteral $Entry.runId) + ";") | Out-Null
    $statements.Add("INSERT INTO public_report_run_object_keys (run_id, object_kind, object_key) VALUES " + ($objectKeyRows -join ", ") + ";") | Out-Null

    if ($UpdateLatest) {
        $latestColumns = @(
            "singleton",
            "run_id",
            "generated_at",
            "published_at",
            "claim_level",
            "publishable",
            "diagnostic_only",
            "execution_profile",
            "visibility",
            "source_kind",
            "evidence_warning_count",
            "publication_warning_count",
            "copied_artifact_count",
            "skipped_artifact_count",
            "implementation_count",
            "scenario_count",
            "protocol_count",
            "validation_passed",
            "validation_failed",
            "validation_unsupported",
            "validation_not_applicable",
            "validation_inconclusive",
            "validation_infrastructure_failure",
            "benchmark_accepted",
            "benchmark_rejected",
            "benchmark_not_run_validation_failed",
            "benchmark_not_run_unsupported",
            "benchmark_not_run_load_tool_failed",
            "benchmark_not_run_parser_failed",
            "artifact_root_key",
            "bundle_prefix",
            "evidence_report_json_key",
            "evidence_report_markdown_key",
            "artifacts_index_key",
            "publication_manifest_key",
            "publication_warnings_key",
            "publication_skipped_key",
            "report_index_entry_key",
            "report_index_key",
            "registry_key",
            "latest_key"
        )

        $latestValues = @(
            1,
            $Entry.runId,
            $Entry.generatedAt,
            $PublishedAt,
            $Entry.claimLevel,
            [bool]$Entry.publishable,
            [bool]$Entry.diagnosticOnly,
            $Entry.executionProfile,
            $Entry.visibility,
            $Entry.sourceKind,
            [int]$Entry.warningCount,
            [int]$Warnings.Count,
            [int]$Manifest.copiedArtifactCount,
            [int]$Manifest.skippedArtifactCount,
            [int]$Entry.implementations.Count,
            [int]$Entry.scenarios.Count,
            [int]$Entry.protocols.Count,
            [int]$Manifest.validationCounts.passed,
            [int]$Manifest.validationCounts.failed,
            [int]$Manifest.validationCounts.unsupported,
            [int]$Manifest.validationCounts.notApplicable,
            [int]$Manifest.validationCounts.inconclusive,
            [int]$Manifest.validationCounts.infrastructureFailure,
            [int]$Manifest.benchmarkCounts.accepted,
            [int]$Manifest.benchmarkCounts.rejected,
            [int]$Manifest.benchmarkCounts.notRunValidationFailed,
            [int]$Manifest.benchmarkCounts.notRunUnsupported,
            [int]$Manifest.benchmarkCounts.notRunLoadToolFailed,
            [int]$Manifest.benchmarkCounts.notRunParserFailed,
            $Entry.artifactRootKey,
            $Entry.bundlePrefix,
            $Entry.evidenceReportJsonKey,
            $Entry.evidenceReportMarkdownKey,
            $Entry.artifactsIndexKey,
            $Entry.publicationManifestKey,
            $Entry.publicationWarningsKey,
            $Entry.publicationSkippedKey,
            $Entry.reportIndexEntryKey,
            $Entry.reportIndexKey,
            "public/registry/report-index.json",
            "public/registry/latest.json"
        )

        $statements.Add("DELETE FROM public_report_latest;") | Out-Null
        $statements.Add(("INSERT INTO public_report_latest (" + ($latestColumns -join ", ") + ") VALUES " + (New-SqlTuple $latestValues) + ";")) | Out-Null
    }

    $statements.Add("COMMIT;") | Out-Null
    Set-Content -LiteralPath $SqlPath -Value ($statements -join "`n") -Encoding utf8NoBOM
}

if ([string]::IsNullOrWhiteSpace($RunRoot) -and [string]::IsNullOrWhiteSpace($RunId)) {
    throw "Specify --run-root or --run-id."
}

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$RunArtifactsRoot = Resolve-AbsolutePath -Path $RunArtifactsRoot -BasePath $RepoRoot
$PublicationBundleRoot = Resolve-AbsolutePath -Path $PublicationBundleRoot -BasePath $RepoRoot
$RunRoot = if ([string]::IsNullOrWhiteSpace($RunRoot)) { Join-Path $RunArtifactsRoot $RunId } else { $RunRoot }
$RunRoot = Resolve-AbsolutePath -Path $RunRoot -BasePath $RepoRoot

if ([string]::IsNullOrWhiteSpace($RunId)) {
    $RunId = [System.IO.Path]::GetFileName($RunRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar))
}

if ([string]::IsNullOrWhiteSpace($BundleRoot)) {
    $BundleRoot = Join-Path $PublicationBundleRoot $RunId
}

$BundleRoot = Resolve-AbsolutePath -Path $BundleRoot -BasePath $RepoRoot

if (-not (Test-IsUnderRoot -Root $RunArtifactsRoot -Path $RunRoot)) {
    throw "Run root must be inside .artifacts\runs: $RunRoot"
}

if (-not (Test-IsUnderRoot -Root $PublicationBundleRoot -Path $BundleRoot)) {
    throw "Publication bundle root must be inside .artifacts\publication: $BundleRoot"
}

$runRootFolderName = [System.IO.Path]::GetFileName($RunRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar))
if (-not [string]::Equals($runRootFolderName, $RunId, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Run root '$RunRoot' does not match run id '$RunId'."
}

$bundleRootFolderName = [System.IO.Path]::GetFileName($BundleRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar))
if (-not [string]::Equals($bundleRootFolderName, $RunId, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Publication bundle root '$BundleRoot' does not match run id '$RunId'."
}

if (-not (Test-Path -LiteralPath $RunRoot)) {
    throw "Run directory not found: $RunRoot"
}

if ([System.IO.Path]::GetFullPath($RunRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) -eq
    [System.IO.Path]::GetFullPath($BundleRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)) {
    throw "Publication bundle root must be separate from the run root."
}

if (-not (Test-Path -LiteralPath $SchemaPath)) {
    throw "D1 schema file not found: $SchemaPath"
}

Write-Host "ProtocolLab publication handoff"
Write-Host "  run id: $RunId"
Write-Host "  run root: $RunRoot"
Write-Host "  bundle root: $BundleRoot"
Write-Host "  R2 bucket: $BucketName"
Write-Host "  D1 binding: $D1Binding"

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

if ($DryRun) {
    $publishArgs += "--dry-run"
}

Invoke-Tool -FilePath "dotnet" -Arguments $publishArgs -WorkingDirectory $RepoRoot

if ($DryRun) {
    $bundlePrefix = "public/runs/$RunId/"
    Write-Host "Dry run completed."
    Write-Host "  bundle prefix: $bundlePrefix"
    Write-Host "  registry key: public/registry/report-index.json"
    Write-Host "  latest key: public/registry/latest.json"
    return
}

if (-not (Get-Command $WranglerPath -ErrorAction SilentlyContinue)) {
    throw "wrangler was not found on PATH. Install Wrangler or pass -WranglerPath."
}

if (-not (Test-Path -LiteralPath $BundleRoot)) {
    throw "Publication bundle was not created: $BundleRoot"
}

$D1DatabaseId = if ([string]::IsNullOrWhiteSpace($D1DatabaseId)) { $env:PROTOCOL_LAB_DB_ID } else { $D1DatabaseId }
if ([string]::IsNullOrWhiteSpace($D1DatabaseId)) {
    throw "D1 database id is required for the searchable metadata index."
}

$cloudflareApiToken = $env:CLOUDFLARE_API_TOKEN
if ([string]::IsNullOrWhiteSpace($cloudflareApiToken)) {
    $cloudflareApiToken = $env:CF_API_TOKEN
}

if ([string]::IsNullOrWhiteSpace($cloudflareApiToken)) {
    throw "CLOUDFLARE_API_TOKEN or CF_API_TOKEN is required for Cloudflare uploads."
}

$cloudflareAccountId = $env:CLOUDFLARE_ACCOUNT_ID
if ([string]::IsNullOrWhiteSpace($cloudflareAccountId)) {
    $cloudflareAccountId = $env:CF_ACCOUNT_ID
}

if ([string]::IsNullOrWhiteSpace($cloudflareAccountId)) {
    throw "CLOUDFLARE_ACCOUNT_ID is required for Wrangler authentication."
}

$manifest = Read-JsonFile (Join-Path $BundleRoot "publication-manifest.json")
$entry = Read-JsonFile (Join-Path $BundleRoot "report-index-entry.json")
$bundleRegistry = Read-JsonFile (Join-Path $BundleRoot "report-index.json")
$artifactsIndex = Read-JsonFile (Join-Path $BundleRoot "artifacts-index.json")
$evidenceReport = Read-JsonFile (Join-Path $BundleRoot "evidence-report-v1.json")
$warningsPath = Join-Path $BundleRoot "publication-warnings.md"
$warnings = Parse-PublicationWarnings -WarningsPath $warningsPath

if ($entry.runId -ne $RunId -or $manifest.runId -ne $RunId -or $artifactsIndex.runId -ne $RunId -or $evidenceReport.runId -ne $RunId) {
    throw "Staged bundle contains mismatched run identifiers."
}

if ($entry.bundlePrefix -ne "public/runs/$RunId/") {
    throw "Registry entry bundle prefix is incorrect: $($entry.bundlePrefix)"
}

if ($entry.evidenceReportJsonKey -ne "public/runs/$RunId/evidence-report-v1.json" -or
    $entry.evidenceReportMarkdownKey -ne "public/runs/$RunId/evidence-report-v1.md" -or
    $entry.artifactsIndexKey -ne "public/runs/$RunId/artifacts-index.json" -or
    $entry.publicationManifestKey -ne "public/runs/$RunId/publication-manifest.json" -or
    $entry.publicationWarningsKey -ne "public/runs/$RunId/publication-warnings.md" -or
    $entry.publicationSkippedKey -ne "public/runs/$RunId/publication-skipped.md" -or
    $entry.reportIndexEntryKey -ne "public/runs/$RunId/report-index-entry.json" -or
    $entry.reportIndexKey -ne "public/runs/$RunId/report-index.json") {
    throw "Registry entry object keys do not match the expected public layout."
}

if ($manifest.artifactRootKey -ne "artifacts" -or $artifactsIndex.artifactRootKey -ne "artifacts" -or $entry.artifactRootKey -ne "artifacts") {
    throw "Publication bundle artifact root key is invalid."
}

if ($bundleRegistry.entries.Count -lt 1) {
    throw "Publication registry does not contain any entries."
}

$currentRegistryEntry = $bundleRegistry.entries | Where-Object { $_.runId -eq $RunId } | Select-Object -First 1
if (-not $currentRegistryEntry) {
    throw "Publication registry does not include the current run entry."
}

$tempRoot = Join-Path $env:TEMP ("protocol-lab-publication-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force $tempRoot | Out-Null

$publishedAt = [DateTimeOffset]::UtcNow

try {
    $publishedFiles = @(Get-ChildItem -LiteralPath $BundleRoot -File -Recurse | Sort-Object FullName)
    foreach ($file in $publishedFiles) {
        $relativePath = [System.IO.Path]::GetRelativePath($BundleRoot, $file.FullName)
        $objectKey = "public/runs/$RunId/" + ($relativePath -replace '\\', '/')
        Write-R2Object -ObjectKey $objectKey -FilePath $file.FullName
    }

    $registryObject = Read-R2JsonObject -Bucket $BucketName -ObjectKey "public/registry/report-index.json" -TempDirectory $tempRoot
    $mergedRegistry = Build-RegistryMerge -ExistingRegistry $registryObject -CurrentEntry $entry
    $registryPath = Join-Path $tempRoot "report-index.json"
    $mergedRegistry | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $registryPath -Encoding utf8NoBOM
    Write-R2Object -ObjectKey "public/registry/report-index.json" -FilePath $registryPath

    $latestObject = Read-R2JsonObject -Bucket $BucketName -ObjectKey "public/registry/latest.json" -TempDirectory $tempRoot
    $shouldUpdateLatest = $true
    if ($latestObject -and $latestObject.generatedAt) {
        try {
            $currentGeneratedAt = [DateTimeOffset]$entry.generatedAt
            $existingGeneratedAt = [DateTimeOffset]$latestObject.generatedAt
            $shouldUpdateLatest = $currentGeneratedAt -ge $existingGeneratedAt
        }
        catch {
            $shouldUpdateLatest = $true
        }
    }

    if ($shouldUpdateLatest) {
        $latestJson = Build-LatestObject -Entry $entry -Manifest $manifest -PublicationWarnings $warnings -PublishedAt $publishedAt
        $latestPath = Join-Path $tempRoot "latest.json"
        $latestJson | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $latestPath -Encoding utf8NoBOM
        Write-R2Object -ObjectKey "public/registry/latest.json" -FilePath $latestPath
    }

    $configPath = Join-Path $tempRoot "wrangler.toml"
    Write-WranglerConfig -ConfigPath $configPath -AccountId $cloudflareAccountId -DatabaseId $D1DatabaseId -DatabaseName $D1DatabaseName -Binding $D1Binding

    $sqlPath = Join-Path $tempRoot "public-report-index.sql"
    Write-D1SqlFile -Entry $entry -Manifest $manifest -Warnings $warnings -SqlPath $sqlPath -PublishedAt $publishedAt -UpdateLatest $shouldUpdateLatest

    Invoke-Tool -FilePath $WranglerPath -Arguments @("d1", "execute", $D1Binding, "--remote", "--yes", "--config", $configPath, "--file", $SchemaPath) -WorkingDirectory $RepoRoot
    Invoke-Tool -FilePath $WranglerPath -Arguments @("d1", "execute", $D1Binding, "--remote", "--yes", "--config", $configPath, "--file", $sqlPath) -WorkingDirectory $RepoRoot
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

Write-Host "Publication complete."
Write-Host "  R2 prefix: public/runs/$RunId/"
Write-Host "  registry key: public/registry/report-index.json"
Write-Host "  latest key: public/registry/latest.json"
