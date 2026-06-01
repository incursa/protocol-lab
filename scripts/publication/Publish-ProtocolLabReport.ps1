<#
.SYNOPSIS
Promotes a completed ProtocolLab run to the public Cloudflare layout.

.DESCRIPTION
Stages the public-safe bundle with the existing publish-report flow, uploads
the bundle under public/runs/{runId}/ in the protocol-lab-reports R2 bucket
through the S3-compatible API, verifies the uploaded payload before advancing
the public registry objects, and indexes searchable metadata into D1 through
the Cloudflare D1 REST API.

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

.PARAMETER D1DatabaseId
Remote D1 database identifier used by the Cloudflare API.

.PARAMETER PythonPath
Path to python or an equivalent executable that can run boto3.

.PARAMETER AllowDiagnosticPublication
Allows DiagnosticOnly bundles to be promoted when the source bundle remains
explicitly labeled diagnostic-only.

.PARAMETER DryRun
Validate the handoff and print the planned object layout without uploading to
R2 or indexing D1.

.PARAMETER VerifyPublishedRuns
After the metadata write completes, scan the published D1 rows and confirm that
each published run still has its full R2 payload set.
#>
[CmdletBinding()]
param(
    [string]$RunId,
    [string]$RunRoot,
    [string]$BundleRoot,
    [string]$BucketName = "protocol-lab-reports",
    [string]$D1DatabaseId,
    [string]$PythonPath = "python",
    [switch]$AllowDiagnosticPublication,
    [switch]$DryRun,
    [switch]$VerifyPublishedRuns
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

function ConvertFrom-D1Boolean {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $false
    }

    if ($Value -is [bool]) {
        return [bool]$Value
    }

    $text = [string]$Value
    if ([string]::Equals($text, "true", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    if ([string]::Equals($text, "false", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    return ([int]$Value) -ne 0
}

function ConvertFrom-D1Integer {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return 0
    }

    return [int]$Value
}

function Read-JsonFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required file not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-HttpErrorBody {
    param(
        [Parameter(Mandatory = $true)]
        [System.Management.Automation.ErrorRecord]$ErrorRecord
    )

    $response = $ErrorRecord.Exception.Response
    if ($null -eq $response) {
        return $null
    }

    try {
        if ($response -is [System.Net.Http.HttpResponseMessage]) {
            return $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }

        if ($response -is [System.Net.WebResponse]) {
            $stream = $response.GetResponseStream()
            if ($null -eq $stream) {
                return $null
            }

            $reader = [System.IO.StreamReader]::new($stream)
            try {
                return $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
                $stream.Dispose()
            }
        }

        return $null
    }
    catch {
        return $null
    }
}

function Get-CloudflareErrorMessage {
    param(
        [AllowNull()]
        [object]$ResponseBody
    )

    if ($null -eq $ResponseBody) {
        return $null
    }

    $text = [string]$ResponseBody
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    try {
        $json = $text | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        return $text.Trim()
    }

    $messages = New-Object System.Collections.Generic.List[string]
    foreach ($propertyName in @("errors", "messages")) {
        $propertyValue = $json.$propertyName
        if ($null -eq $propertyValue) {
            continue
        }

        foreach ($item in @($propertyValue)) {
            if ($null -ne $item.message -and -not [string]::IsNullOrWhiteSpace([string]$item.message)) {
                $messages.Add([string]$item.message) | Out-Null
            }
        }
    }

    if ($messages.Count -gt 0) {
        return ($messages -join "; ")
    }

    if ($null -ne $json.message -and -not [string]::IsNullOrWhiteSpace([string]$json.message)) {
        return [string]$json.message
    }

    return $text.Trim()
}

function Invoke-CloudflareD1Sql {
    param(
        [Parameter(Mandatory = $true)][string]$AccountId,
        [Parameter(Mandatory = $true)][string]$DatabaseId,
        [Parameter(Mandatory = $true)][string]$ApiToken,
        [Parameter(Mandatory = $true)][string]$Sql,
        [string[]]$Params,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $uri = "https://api.cloudflare.com/client/v4/accounts/$AccountId/d1/database/$DatabaseId/query"
    $headers = @{
        Authorization = "Bearer $ApiToken"
    }
    $body = @{
        sql = $Sql
    }
    if ($null -ne $Params -and $Params.Count -gt 0) {
        $body.params = @($Params)
    }

    $body = $body | ConvertTo-Json -Depth 20 -Compress

    try {
        $response = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -ContentType "application/json" -Body $body
    }
    catch {
        $bodyText = $null
        if ($_.ErrorDetails -and -not [string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) {
            $bodyText = $_.ErrorDetails.Message
        }
        if ([string]::IsNullOrWhiteSpace([string]$bodyText)) {
            $bodyText = Get-HttpErrorBody -ErrorRecord $_
        }

        $message = Get-CloudflareErrorMessage -ResponseBody $bodyText
        if ([string]::IsNullOrWhiteSpace([string]$message)) {
            $message = $_.Exception.Message
        }

        throw "Failed to execute D1 $Description query: $message"
    }

    if ($null -eq $response) {
        throw "D1 $Description query returned no response."
    }

    if ($response.success -ne $true) {
        $message = Get-CloudflareErrorMessage -ResponseBody ($response | ConvertTo-Json -Depth 20)
        if ([string]::IsNullOrWhiteSpace([string]$message)) {
            $message = "Cloudflare returned success=false for the D1 $Description query."
        }

        throw $message
    }

    return $response
}

function Invoke-CloudflareD1QueryRows {
    param(
        [Parameter(Mandatory = $true)][string]$AccountId,
        [Parameter(Mandatory = $true)][string]$DatabaseId,
        [Parameter(Mandatory = $true)][string]$ApiToken,
        [Parameter(Mandatory = $true)][string]$Sql,
        [string[]]$Params,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $response = Invoke-CloudflareD1Sql -AccountId $AccountId -DatabaseId $DatabaseId -ApiToken $ApiToken -Sql $Sql -Params $Params -Description $Description
    if ($null -eq $response.result -or $response.result.Count -lt 1) {
        return @()
    }

    $rows = $response.result[0].results
    if ($null -eq $rows) {
        return @()
    }

    return @($rows)
}

function Invoke-D1SqlFile {
    param(
        [Parameter(Mandatory = $true)][string]$AccountId,
        [Parameter(Mandatory = $true)][string]$DatabaseId,
        [Parameter(Mandatory = $true)][string]$ApiToken,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "D1 SQL file not found: $Path"
    }

    $sql = Get-Content -LiteralPath $Path -Raw
    if ([string]::IsNullOrWhiteSpace($sql)) {
        throw "D1 SQL file is empty: $Path"
    }

    Invoke-CloudflareD1Sql -AccountId $AccountId -DatabaseId $DatabaseId -ApiToken $ApiToken -Sql $sql -Description $Description | Out-Null
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

function Normalize-R2Endpoint {
    param(
        [Parameter(Mandatory = $true)][string]$Endpoint
    )

    try {
        $uri = [Uri]$Endpoint
    }
    catch {
        throw "Invalid R2 endpoint URI: $Endpoint"
    }

    if (-not [string]::Equals($uri.Scheme, "https", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "R2 endpoint must use https: $Endpoint"
    }

    $builder = [System.UriBuilder]::new($uri)
    $builder.Path = ""
    $builder.Query = ""
    $builder.Fragment = ""
    return $builder.Uri.AbsoluteUri.TrimEnd("/")
}

function Read-R2JsonObject {
    param(
        [Parameter(Mandatory = $true)][string]$Bucket,
        [Parameter(Mandatory = $true)][string]$ObjectKey,
        [Parameter(Mandatory = $true)][string]$TempDirectory
    )

    $tempFile = Join-Path $TempDirectory ([System.IO.Path]::GetRandomFileName() + ".json")
    try {
        $helperScript = Join-Path $PSScriptRoot "r2_s3_helper.py"
        if (-not (Test-Path -LiteralPath $helperScript)) {
            throw "R2 helper script not found: $helperScript"
        }

        $commandOutput = & $PythonPath $helperScript "get" $Bucket $ObjectKey $tempFile 2>&1
        $exitCode = $LASTEXITCODE

        if ($exitCode -ne 0) {
            $message = (($commandOutput | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine).Trim()
            if ($exitCode -eq 2) {
                return $null
            }

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

function Test-R2ObjectExists {
    param(
        [Parameter(Mandatory = $true)][string]$Bucket,
        [Parameter(Mandatory = $true)][string]$ObjectKey
    )

    $tempFile = Join-Path $env:TEMP ([System.IO.Path]::GetRandomFileName())
    try {
        $helperScript = Join-Path $PSScriptRoot "r2_s3_helper.py"
        if (-not (Test-Path -LiteralPath $helperScript)) {
            throw "R2 helper script not found: $helperScript"
        }

        $commandOutput = & $PythonPath $helperScript "head" $Bucket $ObjectKey $tempFile 2>&1
        $exitCode = $LASTEXITCODE
        if ($exitCode -eq 0) {
            return $true
        }

        $message = (($commandOutput | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine).Trim()
        if ($exitCode -eq 2 -or
            $message -match '(?i)\b404\b' -or
            $message -match '(?i)\bnot found\b' -or
            $message -match '(?i)\bno such object\b') {
            return $false
        }

        throw "Failed to check R2 object '$Bucket/$ObjectKey': $message"
    }
    finally {
        if (Test-Path -LiteralPath $tempFile) {
            Remove-Item -LiteralPath $tempFile -Force
        }
    }
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

function Build-RegistryFromEntries {
    param(
        [Parameter(Mandatory = $true)][object[]]$Entries
    )

    $entries = New-Object System.Collections.ArrayList
    $seenRunIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in @($Entries)) {
        $runId = [string]$entry.runId
        if ([string]::IsNullOrWhiteSpace($runId)) {
            throw "Registry entry is missing a run id."
        }

        if (-not $seenRunIds.Add($runId)) {
            throw "Registry contains duplicate run id '$runId'."
        }

        $entries.Add($entry) | Out-Null
    }

    $sorted = @($entries | Sort-Object `
        @{ Expression = { $_.generatedAt }; Descending = $true }, `
        @{ Expression = { $_.runId }; Descending = $false })

    return [ordered]@{
        schemaVersion = "protocol-lab.public-report-index.v1"
        entries = $sorted
    }
}

function Assert-RegistryIncludesRun {
    param(
        [Parameter(Mandatory = $true)]$Registry,
        [Parameter(Mandatory = $true)][string]$RunId,
        [Parameter(Mandatory = $true)][string]$Description
    )

    if ($null -eq $Registry -or $null -eq $Registry.entries) {
        throw "$Description is missing an entries collection."
    }

    $currentEntry = @($Registry.entries | Where-Object { $_.runId -and [string]::Equals([string]$_.runId, $RunId, [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1)
    if ($currentEntry.Count -lt 1) {
        throw "$Description does not include run '$RunId'."
    }
}

function Get-D1PublishedRunRows {
    param(
        [Parameter(Mandatory = $true)][string]$AccountId,
        [Parameter(Mandatory = $true)][string]$DatabaseId,
        [Parameter(Mandatory = $true)][string]$ApiToken
    )

    return @(Invoke-CloudflareD1QueryRows -AccountId $AccountId -DatabaseId $DatabaseId -ApiToken $ApiToken -Sql @"
SELECT
  run_id,
  generated_at,
  published_at,
  claim_level,
  publishable,
  diagnostic_only,
  execution_profile,
  visibility,
  source_kind,
  evidence_warning_count,
  publication_warning_count,
  copied_artifact_count,
  skipped_artifact_count,
  implementation_count,
  scenario_count,
  protocol_count,
  validation_passed,
  validation_failed,
  validation_unsupported,
  validation_not_applicable,
  validation_inconclusive,
  validation_infrastructure_failure,
  benchmark_accepted,
  benchmark_rejected,
  benchmark_not_run_validation_failed,
  benchmark_not_run_unsupported,
  benchmark_not_run_load_tool_failed,
  benchmark_not_run_parser_failed,
  artifact_root_key,
  bundle_prefix,
  evidence_report_json_key,
  evidence_report_markdown_key,
  artifacts_index_key,
  publication_manifest_key,
  publication_warnings_key,
  publication_skipped_key,
  report_index_entry_key,
  report_index_key
FROM public_report_runs
ORDER BY generated_at DESC, run_id ASC
"@ -Description "published runs")
}

function Get-D1PublishedRunRow {
    param(
        [Parameter(Mandatory = $true)][string]$AccountId,
        [Parameter(Mandatory = $true)][string]$DatabaseId,
        [Parameter(Mandatory = $true)][string]$ApiToken,
        [Parameter(Mandatory = $true)][string]$RunId
    )

    $runIdLiteral = ConvertTo-SqlLiteral $RunId
    $rows = @(Invoke-CloudflareD1QueryRows -AccountId $AccountId -DatabaseId $DatabaseId -ApiToken $ApiToken -Sql @"
SELECT
  run_id,
  generated_at,
  published_at,
  claim_level,
  publishable,
  diagnostic_only,
  execution_profile,
  visibility,
  source_kind,
  evidence_warning_count,
  publication_warning_count,
  copied_artifact_count,
  skipped_artifact_count,
  implementation_count,
  scenario_count,
  protocol_count,
  validation_passed,
  validation_failed,
  validation_unsupported,
  validation_not_applicable,
  validation_inconclusive,
  validation_infrastructure_failure,
  benchmark_accepted,
  benchmark_rejected,
  benchmark_not_run_validation_failed,
  benchmark_not_run_unsupported,
  benchmark_not_run_load_tool_failed,
  benchmark_not_run_parser_failed,
  artifact_root_key,
  bundle_prefix,
  evidence_report_json_key,
  evidence_report_markdown_key,
  artifacts_index_key,
  publication_manifest_key,
  publication_warnings_key,
  publication_skipped_key,
  report_index_entry_key,
  report_index_key
FROM public_report_runs
WHERE run_id = $runIdLiteral
LIMIT 1
"@ -Description "published run '$RunId'")

    if ($rows.Count -lt 1) {
        return $null
    }

    return $rows[0]
}

function Get-D1LatestPublicationRow {
    param(
        [Parameter(Mandatory = $true)][string]$AccountId,
        [Parameter(Mandatory = $true)][string]$DatabaseId,
        [Parameter(Mandatory = $true)][string]$ApiToken
    )

    $rows = @(Invoke-CloudflareD1QueryRows -AccountId $AccountId -DatabaseId $DatabaseId -ApiToken $ApiToken -Sql @"
SELECT
  run_id,
  generated_at,
  published_at,
  claim_level,
  publishable,
  diagnostic_only,
  execution_profile,
  visibility,
  source_kind,
  evidence_warning_count,
  publication_warning_count,
  copied_artifact_count,
  skipped_artifact_count,
  implementation_count,
  scenario_count,
  protocol_count,
  validation_passed,
  validation_failed,
  validation_unsupported,
  validation_not_applicable,
  validation_inconclusive,
  validation_infrastructure_failure,
  benchmark_accepted,
  benchmark_rejected,
  benchmark_not_run_validation_failed,
  benchmark_not_run_unsupported,
  benchmark_not_run_load_tool_failed,
  benchmark_not_run_parser_failed,
  artifact_root_key,
  bundle_prefix,
  evidence_report_json_key,
  evidence_report_markdown_key,
  artifacts_index_key,
  publication_manifest_key,
  publication_warnings_key,
  publication_skipped_key,
  report_index_entry_key,
  report_index_key,
  registry_key,
  latest_key
FROM public_report_latest
LIMIT 1
"@ -Description "latest publication")

    if ($rows.Count -lt 1) {
        return $null
    }

    return $rows[0]
}

function Test-D1RunShouldUpdateLatest {
    param(
        [AllowNull()]$LatestRow,
        [Parameter(Mandatory = $true)]$Entry
    )

    if ($null -eq $LatestRow -or [string]::IsNullOrWhiteSpace([string]$LatestRow.generated_at)) {
        return $true
    }

    try {
        $currentGeneratedAt = [DateTimeOffset]$Entry.generatedAt
        $existingGeneratedAt = [DateTimeOffset]$LatestRow.generated_at
        return $currentGeneratedAt -ge $existingGeneratedAt
    }
    catch {
        return $true
    }
}

function Assert-RegistryEntryMatchesD1Run {
    param(
        [Parameter(Mandatory = $true)]$Entry,
        [Parameter(Mandatory = $true)]$Row
    )

    $runId = [string]$Row.run_id
    if ([string]::IsNullOrWhiteSpace($runId)) {
        throw "D1 published run row is missing a run id."
    }

    if (-not [string]::Equals([string]$Entry.runId, $runId, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "D1 published run '$runId' references registry entry for run '$($Entry.runId)'."
    }

    $stringFields = @(
        [ordered]@{ Json = "bundlePrefix"; Sql = "bundle_prefix" },
        [ordered]@{ Json = "evidenceReportJsonKey"; Sql = "evidence_report_json_key" },
        [ordered]@{ Json = "evidenceReportMarkdownKey"; Sql = "evidence_report_markdown_key" },
        [ordered]@{ Json = "artifactsIndexKey"; Sql = "artifacts_index_key" },
        [ordered]@{ Json = "publicationManifestKey"; Sql = "publication_manifest_key" },
        [ordered]@{ Json = "publicationWarningsKey"; Sql = "publication_warnings_key" },
        [ordered]@{ Json = "publicationSkippedKey"; Sql = "publication_skipped_key" },
        [ordered]@{ Json = "reportIndexEntryKey"; Sql = "report_index_entry_key" },
        [ordered]@{ Json = "reportIndexKey"; Sql = "report_index_key" },
        [ordered]@{ Json = "visibility"; Sql = "visibility" },
        [ordered]@{ Json = "sourceKind"; Sql = "source_kind" },
        [ordered]@{ Json = "claimLevel"; Sql = "claim_level" },
        [ordered]@{ Json = "executionProfile"; Sql = "execution_profile" },
        [ordered]@{ Json = "artifactRootKey"; Sql = "artifact_root_key" }
    )

    foreach ($field in $stringFields) {
        $jsonValue = [string]$Entry.($field.Json)
        $sqlValue = [string]$Row.($field.Sql)
        if (-not [string]::Equals($jsonValue, $sqlValue, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "D1 published run '$runId' has mismatched registry field '$($field.Json)' ('$jsonValue' vs '$sqlValue')."
        }
    }

    if ([bool]$Entry.publishable -ne (ConvertFrom-D1Boolean $Row.publishable)) {
        throw "D1 published run '$runId' has mismatched publishable metadata."
    }

    if ([bool]$Entry.diagnosticOnly -ne (ConvertFrom-D1Boolean $Row.diagnostic_only)) {
        throw "D1 published run '$runId' has mismatched diagnosticOnly metadata."
    }

    if ([int]$Entry.warningCount -ne (ConvertFrom-D1Integer $Row.evidence_warning_count)) {
        throw "D1 published run '$runId' has mismatched evidence warning count."
    }

    if (@($Entry.implementations).Count -ne (ConvertFrom-D1Integer $Row.implementation_count)) {
        throw "D1 published run '$runId' has mismatched implementation count."
    }

    if (@($Entry.scenarios).Count -ne (ConvertFrom-D1Integer $Row.scenario_count)) {
        throw "D1 published run '$runId' has mismatched scenario count."
    }

    if (@($Entry.protocols).Count -ne (ConvertFrom-D1Integer $Row.protocol_count)) {
        throw "D1 published run '$runId' has mismatched protocol count."
    }
}

function Wait-D1PublishedRunRows {
    param(
        [Parameter(Mandatory = $true)][string]$AccountId,
        [Parameter(Mandatory = $true)][string]$DatabaseId,
        [Parameter(Mandatory = $true)][string]$ApiToken,
        [Parameter(Mandatory = $true)][string]$RunId,
        [Parameter(Mandatory = $true)]$CurrentEntry,
        [int]$MaxAttempts = 12,
        [int]$DelaySeconds = 5
    )

    $lastRowCount = 0
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $publishedRunRows = @(Get-D1PublishedRunRows -AccountId $AccountId -DatabaseId $DatabaseId -ApiToken $ApiToken)
        $lastRowCount = $publishedRunRows.Count
        $currentRow = @($publishedRunRows | Where-Object { [string]::Equals([string]$_.run_id, $RunId, [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1)

        if ($currentRow.Count -lt 1) {
            $directRow = Get-D1PublishedRunRow -AccountId $AccountId -DatabaseId $DatabaseId -ApiToken $ApiToken -RunId $RunId
            if ($null -ne $directRow) {
                Assert-RegistryEntryMatchesD1Run -Entry $CurrentEntry -Row $directRow
                Write-Host "D1 current run '$RunId' was visible by direct lookup on attempt $attempt; merging it into the registry scan."
                return @($publishedRunRows + $directRow)
            }
        }
        else {
            Assert-RegistryEntryMatchesD1Run -Entry $CurrentEntry -Row $currentRow[0]
            Write-Host "D1 published run scan returned $($publishedRunRows.Count) run(s); current run '$RunId' visible on attempt $attempt."
            return $publishedRunRows
        }

        if ($attempt -lt $MaxAttempts) {
            Write-Host "D1 published run scan returned $lastRowCount run(s) but not '$RunId'; retrying in $DelaySeconds second(s)."
            Start-Sleep -Seconds $DelaySeconds
        }
    }

    throw "D1 published run scan did not expose current run '$RunId' after $MaxAttempts attempt(s); refusing to refresh public registry objects."
}

function Get-D1BackedRegistryEntries {
    param(
        [Parameter(Mandatory = $true)][object[]]$RunRows,
        [Parameter(Mandatory = $true)]$CurrentEntry,
        [Parameter(Mandatory = $true)][string]$Bucket,
        [Parameter(Mandatory = $true)][string]$TempDirectory
    )

    $entries = New-Object System.Collections.ArrayList
    foreach ($row in @($RunRows)) {
        $runId = [string]$row.run_id
        if ([string]::IsNullOrWhiteSpace($runId)) {
            throw "D1 published run row is missing a run id."
        }

        if ([string]::Equals($runId, [string]$CurrentEntry.runId, [System.StringComparison]::OrdinalIgnoreCase)) {
            $entry = $CurrentEntry
        }
        else {
            $entryKey = [string]$row.report_index_entry_key
            if ([string]::IsNullOrWhiteSpace($entryKey)) {
                throw "D1 published run '$runId' has a blank report-index-entry key."
            }

            $entry = Read-R2JsonObject -Bucket $Bucket -ObjectKey $entryKey -TempDirectory $TempDirectory
            if ($null -eq $entry) {
                throw "D1 published run '$runId' references missing or malformed registry entry '$entryKey'."
            }
        }

        Assert-RegistryEntryMatchesD1Run -Entry $entry -Row $row
        $entries.Add($entry) | Out-Null
    }

    return @($entries)
}

function Build-LatestObjectFromD1Run {
    param(
        [Parameter(Mandatory = $true)]$Entry,
        [Parameter(Mandatory = $true)]$Row
    )

    return [ordered]@{
        schemaVersion = "protocol-lab.public-report-latest.v1"
        runId = $Entry.runId
        generatedAt = $Entry.generatedAt
        publishedAt = [string]$Row.published_at
        claimLevel = $Entry.claimLevel
        publishable = [bool]$Entry.publishable
        diagnosticOnly = [bool]$Entry.diagnosticOnly
        executionProfile = $Entry.executionProfile
        visibility = $Entry.visibility
        sourceKind = $Entry.sourceKind
        evidenceWarningCount = ConvertFrom-D1Integer $Row.evidence_warning_count
        publicationWarningCount = ConvertFrom-D1Integer $Row.publication_warning_count
        copiedArtifactCount = ConvertFrom-D1Integer $Row.copied_artifact_count
        skippedArtifactCount = ConvertFrom-D1Integer $Row.skipped_artifact_count
        implementationCount = ConvertFrom-D1Integer $Row.implementation_count
        scenarioCount = ConvertFrom-D1Integer $Row.scenario_count
        protocolCount = ConvertFrom-D1Integer $Row.protocol_count
        validationPassed = ConvertFrom-D1Integer $Row.validation_passed
        validationFailed = ConvertFrom-D1Integer $Row.validation_failed
        validationUnsupported = ConvertFrom-D1Integer $Row.validation_unsupported
        validationNotApplicable = ConvertFrom-D1Integer $Row.validation_not_applicable
        validationInconclusive = ConvertFrom-D1Integer $Row.validation_inconclusive
        validationInfrastructureFailure = ConvertFrom-D1Integer $Row.validation_infrastructure_failure
        benchmarkAccepted = ConvertFrom-D1Integer $Row.benchmark_accepted
        benchmarkRejected = ConvertFrom-D1Integer $Row.benchmark_rejected
        benchmarkNotRunValidationFailed = ConvertFrom-D1Integer $Row.benchmark_not_run_validation_failed
        benchmarkNotRunUnsupported = ConvertFrom-D1Integer $Row.benchmark_not_run_unsupported
        benchmarkNotRunLoadToolFailed = ConvertFrom-D1Integer $Row.benchmark_not_run_load_tool_failed
        benchmarkNotRunParserFailed = ConvertFrom-D1Integer $Row.benchmark_not_run_parser_failed
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

function Assert-UploadedRunBundleComplete {
    param(
        [Parameter(Mandatory = $true)][string]$Bucket,
        [Parameter(Mandatory = $true)][string]$RunId,
        [Parameter(Mandatory = $true)][string]$BundleRoot,
        [Parameter(Mandatory = $true)][string]$TempDirectory
    )

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
        $requiredObjectKey = "public/runs/$RunId/$objectName"
        if (-not (Test-R2ObjectExists -Bucket $Bucket -ObjectKey $requiredObjectKey)) {
            throw "Required R2 object '$requiredObjectKey' was not found."
        }
    }

    $artifactsIndex = Read-R2JsonObject -Bucket $Bucket -ObjectKey "public/runs/$RunId/artifacts-index.json" -TempDirectory $TempDirectory
    if ($null -eq $artifactsIndex) {
        throw "Required R2 object 'public/runs/$RunId/artifacts-index.json' is missing or malformed JSON."
    }

    if ($artifactsIndex.PSObject.Properties.Name -contains "runId" -and
        -not [string]::IsNullOrWhiteSpace([string]$artifactsIndex.runId) -and
        -not [string]::Equals([string]$artifactsIndex.runId, $RunId, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Uploaded artifacts index 'public/runs/$RunId/artifacts-index.json' has mismatched run id '$($artifactsIndex.runId)'."
    }

    foreach ($cell in @($artifactsIndex.cells)) {
        foreach ($file in @($cell.files)) {
            if ($file.exists -ne $true) {
                continue
            }

            $artifactPath = [string]$file.path
            if ([string]::IsNullOrWhiteSpace($artifactPath)) {
                throw "Uploaded artifacts index 'public/runs/$RunId/artifacts-index.json' includes a copied artifact with a blank path."
            }

            $artifactKey = "public/runs/$RunId/$artifactPath"
            if (-not (Test-R2ObjectExists -Bucket $Bucket -ObjectKey $artifactKey)) {
                throw "Uploaded artifacts index 'public/runs/$RunId/artifacts-index.json' references missing copied artifact '$artifactKey'."
            }
        }
    }

    $bundleFiles = @(Get-ChildItem -LiteralPath $BundleRoot -File -Recurse | Sort-Object FullName)
    foreach ($file in $bundleFiles) {
        $relativePath = [System.IO.Path]::GetRelativePath($BundleRoot, $file.FullName)
        $objectKey = "public/runs/$RunId/" + ($relativePath -replace '\\', '/')

        if ($file.Extension -ieq ".json") {
            $remoteJson = Read-R2JsonObject -Bucket $Bucket -ObjectKey $objectKey -TempDirectory $TempDirectory
            if ($null -eq $remoteJson) {
                throw "Uploaded R2 object '$objectKey' is missing or malformed JSON."
            }

            if ($file.Name -eq "report-index.json") {
                $currentEntry = @($remoteJson.entries | Where-Object { $_.runId -and [string]::Equals([string]$_.runId, $RunId, [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1)
                if ($currentEntry.Count -lt 1) {
                    throw "Uploaded registry bundle '$objectKey' does not include the current run entry."
                }
            }
            elseif ($remoteJson.PSObject.Properties.Name -contains "runId" -and
                -not [string]::IsNullOrWhiteSpace([string]$remoteJson.runId) -and
                -not [string]::Equals([string]$remoteJson.runId, $RunId, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Uploaded R2 object '$objectKey' has mismatched run id '$($remoteJson.runId)'."
            }

            if ($file.Name -eq "report-index-entry.json") {
                $expectedPrefix = "public/runs/$RunId/"
                if ($remoteJson.PSObject.Properties.Name -contains "bundlePrefix" -and
                    -not [string]::Equals([string]$remoteJson.bundlePrefix, $expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "Uploaded registry entry '$objectKey' has mismatched bundle prefix '$($remoteJson.bundlePrefix)'."
                }
            }
        }
        else {
            if (-not (Test-R2ObjectExists -Bucket $Bucket -ObjectKey $objectKey)) {
                throw "Uploaded R2 object '$objectKey' was not found."
            }
        }
    }
}

function Assert-UploadedRegistryObjectsComplete {
    param(
        [Parameter(Mandatory = $true)][string]$Bucket,
        [Parameter(Mandatory = $true)][string]$RunId,
        [Parameter(Mandatory = $true)][string]$TempDirectory,
        [Parameter(Mandatory = $true)][string]$ExpectedLatestRunId
    )

    $registryObject = Read-R2JsonObject -Bucket $Bucket -ObjectKey "public/registry/report-index.json" -TempDirectory $TempDirectory
    if ($null -eq $registryObject) {
        throw "Uploaded registry object 'public/registry/report-index.json' is missing or malformed JSON."
    }

    Assert-RegistryIncludesRun -Registry $registryObject -RunId $RunId -Description "Uploaded registry object 'public/registry/report-index.json'"

    $latestJson = Read-R2JsonObject -Bucket $Bucket -ObjectKey "public/registry/latest.json" -TempDirectory $TempDirectory
    if ($null -eq $latestJson) {
        throw "Uploaded registry object 'public/registry/latest.json' is missing or malformed JSON."
    }

    if ($latestJson.PSObject.Properties.Name -contains "runId" -and
        -not [string]::Equals([string]$latestJson.runId, $ExpectedLatestRunId, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Uploaded registry object 'public/registry/latest.json' points at run '$($latestJson.runId)' instead of '$ExpectedLatestRunId'."
    }
}

function Assert-PublishedRunsComplete {
    param(
        [Parameter(Mandatory = $true)][string]$Bucket,
        [Parameter(Mandatory = $true)][string]$AccountId,
        [Parameter(Mandatory = $true)][string]$DatabaseId,
        [Parameter(Mandatory = $true)][string]$ApiToken,
        [Parameter(Mandatory = $true)][string]$TempDirectory
    )

    $runRows = @(Invoke-CloudflareD1QueryRows -AccountId $AccountId -DatabaseId $DatabaseId -ApiToken $ApiToken -Sql @"
SELECT
  run_id,
  generated_at,
  published_at,
  claim_level,
  publishable,
  diagnostic_only,
  execution_profile,
  visibility,
  source_kind,
  artifact_root_key,
  bundle_prefix,
  evidence_report_json_key,
  evidence_report_markdown_key,
  artifacts_index_key,
  publication_manifest_key,
  publication_warnings_key,
  publication_skipped_key,
  report_index_entry_key,
  report_index_key
FROM public_report_runs
ORDER BY generated_at DESC
"@ -Description "published runs")

    if ($runRows.Count -lt 1) {
        throw "D1 published run scan returned no runs."
    }

    $objectKeyRows = @(Invoke-CloudflareD1QueryRows -AccountId $AccountId -DatabaseId $DatabaseId -ApiToken $ApiToken -Sql @"
SELECT
  run_id,
  object_kind,
  object_key
FROM public_report_run_object_keys
ORDER BY run_id, object_kind
"@ -Description "run object keys")

    $objectKeysByRun = @{}
    foreach ($row in $objectKeyRows) {
        $runId = [string]$row.run_id
        if ([string]::IsNullOrWhiteSpace($runId)) {
            continue
        }

        if (-not $objectKeysByRun.ContainsKey($runId)) {
            $objectKeysByRun[$runId] = @{}
        }

        $objectKeysByRun[$runId][[string]$row.object_kind] = [string]$row.object_key
    }

    $requiredObjectKinds = @(
        "evidence-report-v1.json",
        "evidence-report-v1.md",
        "artifacts-index.json",
        "publication-manifest.json",
        "publication-warnings.md",
        "publication-skipped.md",
        "report-index-entry.json",
        "report-index.json"
    )

    $objectKindColumns = @{
        "evidence-report-v1.json" = "evidence_report_json_key"
        "evidence-report-v1.md" = "evidence_report_markdown_key"
        "artifacts-index.json" = "artifacts_index_key"
        "publication-manifest.json" = "publication_manifest_key"
        "publication-warnings.md" = "publication_warnings_key"
        "publication-skipped.md" = "publication_skipped_key"
        "report-index-entry.json" = "report_index_entry_key"
        "report-index.json" = "report_index_key"
    }

    foreach ($run in $runRows) {
        $runId = [string]$run.run_id
        if ([string]::IsNullOrWhiteSpace($runId)) {
            throw "D1 published run row is missing a run id."
        }

        if ($run.PSObject.Properties.Name -contains "bundle_prefix" -and
            -not [string]::Equals([string]$run.bundle_prefix, "public/runs/$runId/", [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "D1 published run '$runId' has a mismatched bundle prefix '$($run.bundle_prefix)'."
        }

        if ($run.PSObject.Properties.Name -contains "artifact_root_key" -and
            -not [string]::Equals([string]$run.artifact_root_key, "artifacts", [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "D1 published run '$runId' has a mismatched artifact root key '$($run.artifact_root_key)'."
        }

        if (-not $objectKeysByRun.ContainsKey($runId)) {
            throw "D1 published run '$runId' has no object key rows."
        }

        $expectedKeys = $objectKeysByRun[$runId]
        foreach ($objectKind in $requiredObjectKinds) {
            if (-not $expectedKeys.ContainsKey($objectKind)) {
                throw "D1 published run '$runId' is missing object kind '$objectKind'."
            }

            $objectKey = [string]$expectedKeys[$objectKind]
            if ([string]::IsNullOrWhiteSpace($objectKey)) {
                throw "D1 published run '$runId' has an empty object key for '$objectKind'."
            }

            $expectedColumn = $objectKindColumns[$objectKind]
            if ($run.PSObject.Properties.Name -notcontains $expectedColumn) {
                throw "D1 published run '$runId' does not expose the '$expectedColumn' column."
            }

            $runColumnValue = [string]$run.$expectedColumn
            if (-not [string]::Equals($runColumnValue, $objectKey, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "D1 published run '$runId' has mismatched object key '$objectKey' for '$objectKind' (expected '$runColumnValue')."
            }

            if ($objectKind -like "*.json") {
                $remoteJson = Read-R2JsonObject -Bucket $Bucket -ObjectKey $objectKey -TempDirectory $TempDirectory
                if ($null -eq $remoteJson) {
                    throw "D1 published run '$runId' references missing or malformed JSON object '$objectKey'."
                }

                if ($remoteJson.PSObject.Properties.Name -contains "runId" -and
                    -not [string]::IsNullOrWhiteSpace([string]$remoteJson.runId) -and
                    -not [string]::Equals([string]$remoteJson.runId, $runId, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "D1 published run '$runId' references JSON object '$objectKey' with mismatched run id '$($remoteJson.runId)'."
                }

                if ($objectKind -eq "report-index-entry.json") {
                    $expectedPrefix = [string]$run.bundle_prefix
                    if ($remoteJson.PSObject.Properties.Name -contains "bundlePrefix" -and
                        -not [string]::Equals([string]$remoteJson.bundlePrefix, $expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                        throw "D1 published run '$runId' has a mismatched bundle prefix in '$objectKey'."
                    }
                }
            }
            elseif (-not (Test-R2ObjectExists -Bucket $Bucket -ObjectKey $objectKey)) {
                throw "D1 published run '$runId' references missing R2 object '$objectKey'."
            }
        }

        $artifactsIndexKey = [string]$run.artifacts_index_key
        $artifactsIndex = Read-R2JsonObject -Bucket $Bucket -ObjectKey $artifactsIndexKey -TempDirectory $TempDirectory
        if ($null -eq $artifactsIndex) {
            throw "D1 published run '$runId' references missing or malformed artifacts index '$artifactsIndexKey'."
        }

        if ($artifactsIndex.PSObject.Properties.Name -contains "runId" -and
            -not [string]::IsNullOrWhiteSpace([string]$artifactsIndex.runId) -and
            -not [string]::Equals([string]$artifactsIndex.runId, $runId, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "D1 published run '$runId' has an artifacts index with mismatched run id '$($artifactsIndex.runId)'."
        }

        foreach ($cell in @($artifactsIndex.cells)) {
            foreach ($file in @($cell.files)) {
                if ($file.exists -ne $true) {
                    continue
                }

                $artifactPath = [string]$file.path
                if ([string]::IsNullOrWhiteSpace($artifactPath)) {
                    throw "D1 published run '$runId' includes a copied artifact with a blank path."
                }

                $artifactKey = "public/runs/$runId/$artifactPath"
                if (-not (Test-R2ObjectExists -Bucket $Bucket -ObjectKey $artifactKey)) {
                    throw "D1 published run '$runId' references missing copied artifact '$artifactKey'."
                }
            }
        }
    }

    $latestRows = @(Invoke-CloudflareD1QueryRows -AccountId $AccountId -DatabaseId $DatabaseId -ApiToken $ApiToken -Sql @"
SELECT
  run_id,
  generated_at,
  published_at,
  claim_level,
  publishable,
  diagnostic_only,
  execution_profile,
  visibility,
  source_kind,
  evidence_warning_count,
  publication_warning_count,
  copied_artifact_count,
  skipped_artifact_count,
  implementation_count,
  scenario_count,
  protocol_count,
  validation_passed,
  validation_failed,
  validation_unsupported,
  validation_not_applicable,
  validation_inconclusive,
  validation_infrastructure_failure,
  benchmark_accepted,
  benchmark_rejected,
  benchmark_not_run_validation_failed,
  benchmark_not_run_unsupported,
  benchmark_not_run_load_tool_failed,
  benchmark_not_run_parser_failed,
  artifact_root_key,
  bundle_prefix,
  evidence_report_json_key,
  evidence_report_markdown_key,
  artifacts_index_key,
  publication_manifest_key,
  publication_warnings_key,
  publication_skipped_key,
  report_index_entry_key,
  report_index_key,
  registry_key,
  latest_key
FROM public_report_latest
LIMIT 1
"@ -Description "latest publication")

    if ($latestRows.Count -lt 1) {
        throw "D1 latest publication row is missing."
    }

    $latestRow = $latestRows[0]
    $latestRunId = [string]$latestRow.run_id
    if ([string]::IsNullOrWhiteSpace($latestRunId)) {
        throw "D1 latest publication row is missing a run id."
    }

    if (-not ($runRows | Where-Object { [string]::Equals([string]$_.run_id, $latestRunId, [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1)) {
        throw "D1 latest publication row references run '$latestRunId', but no such run exists in public_report_runs."
    }

    if ($latestRow.PSObject.Properties.Name -contains "registry_key" -and
        -not [string]::Equals([string]$latestRow.registry_key, "public/registry/report-index.json", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "D1 latest publication row has a mismatched registry key '$($latestRow.registry_key)'."
    }

    if ($latestRow.PSObject.Properties.Name -contains "latest_key" -and
        -not [string]::Equals([string]$latestRow.latest_key, "public/registry/latest.json", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "D1 latest publication row has a mismatched latest key '$($latestRow.latest_key)'."
    }

    if ([string]::IsNullOrWhiteSpace([string]$latestRow.registry_key)) {
        throw "D1 latest publication row has a blank registry key."
    }

    if ([string]::IsNullOrWhiteSpace([string]$latestRow.latest_key)) {
        throw "D1 latest publication row has a blank latest key."
    }

    if (-not (Test-R2ObjectExists -Bucket $Bucket -ObjectKey ([string]$latestRow.registry_key))) {
        throw "D1 latest publication row references missing registry object '$($latestRow.registry_key)'."
    }

    if (-not (Test-R2ObjectExists -Bucket $Bucket -ObjectKey ([string]$latestRow.latest_key))) {
        throw "D1 latest publication row references missing latest object '$($latestRow.latest_key)'."
    }

    $registryObject = Read-R2JsonObject -Bucket $Bucket -ObjectKey ([string]$latestRow.registry_key) -TempDirectory $TempDirectory
    if ($null -eq $registryObject) {
        throw "D1 latest publication row references a malformed registry object '$($latestRow.registry_key)'."
    }

    if (-not ($registryObject.entries | Where-Object { [string]::Equals([string]$_.runId, $latestRunId, [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1)) {
        throw "D1 latest publication registry '$($latestRow.registry_key)' does not include run '$latestRunId'."
    }

    $publishedRunIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($run in $runRows) {
        [void]$publishedRunIds.Add([string]$run.run_id)
    }

    foreach ($registryEntry in @($registryObject.entries)) {
        $registryRunId = [string]$registryEntry.runId
        if ([string]::IsNullOrWhiteSpace($registryRunId)) {
            throw "D1 latest publication registry '$($latestRow.registry_key)' contains a blank run id."
        }

        if (-not $publishedRunIds.Contains($registryRunId)) {
            throw "D1 latest publication registry '$($latestRow.registry_key)' references stale run '$registryRunId' that is absent from public_report_runs."
        }
    }

    foreach ($publishedRunId in $publishedRunIds) {
        if (-not ($registryObject.entries | Where-Object { [string]::Equals([string]$_.runId, $publishedRunId, [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1)) {
            throw "D1 latest publication registry '$($latestRow.registry_key)' is missing published run '$publishedRunId'."
        }
    }

    $latestObject = Read-R2JsonObject -Bucket $Bucket -ObjectKey ([string]$latestRow.latest_key) -TempDirectory $TempDirectory
    if ($null -eq $latestObject) {
        throw "D1 latest publication row references a malformed latest object '$($latestRow.latest_key)'."
    }

    if ($latestObject.PSObject.Properties.Name -contains "runId" -and
        -not [string]::Equals([string]$latestObject.runId, $latestRunId, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "D1 latest publication latest object '$($latestRow.latest_key)' points at run '$($latestObject.runId)' instead of '$latestRunId'."
    }
}

function Write-R2Object {
    param(
        [Parameter(Mandatory = $true)][string]$ObjectKey,
        [Parameter(Mandatory = $true)][string]$FilePath
    )

    $contentType = Get-ObjectContentType -RelativePath ([System.IO.Path]::GetFileName($FilePath))
    $cacheControl = Get-ObjectCacheControl -ObjectKey $ObjectKey
    $helperScript = Join-Path $PSScriptRoot "r2_s3_helper.py"
    if (-not (Test-Path -LiteralPath $helperScript)) {
        throw "R2 helper script not found: $helperScript"
    }

    Invoke-Tool -FilePath $PythonPath -Arguments @(
        $helperScript,
        "put",
        $BucketName,
        $ObjectKey,
        $FilePath,
        $contentType,
        $cacheControl
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
    $statements.Add(("INSERT INTO public_report_runs (" + ($runColumns -join ", ") + ") VALUES " + (New-SqlTuple $runValues) + " ON CONFLICT(run_id) DO UPDATE SET " + ($runAssignments -join ", ") + ";")) | Out-Null

    $statements.Add("DELETE FROM public_report_run_implementations WHERE run_id = " + (ConvertTo-SqlLiteral $Entry.runId) + ";") | Out-Null
    if ($implementationRows.Count -gt 0) {
        $statements.Add("INSERT INTO public_report_run_implementations (run_id, implementation_id) VALUES " + ($implementationRows -join ", ") + ";") | Out-Null
    }

    $statements.Add("DELETE FROM public_report_run_scenarios WHERE run_id = " + (ConvertTo-SqlLiteral $Entry.runId) + ";") | Out-Null
    if ($scenarioRows.Count -gt 0) {
        $statements.Add("INSERT INTO public_report_run_scenarios (run_id, scenario_id) VALUES " + ($scenarioRows -join ", ") + ";") | Out-Null
    }

    $statements.Add("DELETE FROM public_report_run_protocols WHERE run_id = " + (ConvertTo-SqlLiteral $Entry.runId) + ";") | Out-Null
    if ($protocolRows.Count -gt 0) {
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

$D1DatabaseId = if ([string]::IsNullOrWhiteSpace($D1DatabaseId)) { $env:PROTOCOL_LAB_DB_ID } else { $D1DatabaseId }
if ([string]::IsNullOrWhiteSpace($D1DatabaseId)) {
    throw "D1 database id is required for the searchable metadata index."
}

Write-Host "ProtocolLab publication handoff"
Write-Host "  run id: $RunId"
Write-Host "  run root: $RunRoot"
Write-Host "  bundle root: $BundleRoot"
Write-Host "  R2 bucket: $BucketName"
Write-Host "  D1 database id: $D1DatabaseId"

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

if (-not (Test-Path -LiteralPath $BundleRoot)) {
    throw "Publication bundle was not created: $BundleRoot"
}

$cloudflareApiToken = $env:CLOUDFLARE_API_TOKEN
if ([string]::IsNullOrWhiteSpace($cloudflareApiToken)) {
    $cloudflareApiToken = $env:CF_API_TOKEN
}

if ([string]::IsNullOrWhiteSpace($cloudflareApiToken)) {
    throw "CLOUDFLARE_API_TOKEN or CF_API_TOKEN is required for D1 indexing."
}

$cloudflareAccountId = $env:CLOUDFLARE_ACCOUNT_ID
if ([string]::IsNullOrWhiteSpace($cloudflareAccountId)) {
    $cloudflareAccountId = $env:CF_ACCOUNT_ID
}

if ([string]::IsNullOrWhiteSpace($cloudflareAccountId)) {
    throw "CLOUDFLARE_ACCOUNT_ID is required for D1 indexing and the default R2 endpoint."
}

if ([string]::IsNullOrWhiteSpace($env:AWS_ACCESS_KEY_ID)) {
    throw "AWS_ACCESS_KEY_ID is required for R2 publication."
}

if ([string]::IsNullOrWhiteSpace($env:AWS_SECRET_ACCESS_KEY)) {
    throw "AWS_SECRET_ACCESS_KEY is required for R2 publication."
}

if ([string]::IsNullOrWhiteSpace($env:AWS_DEFAULT_REGION)) {
    $env:AWS_DEFAULT_REGION = "auto"
}

if ([string]::IsNullOrWhiteSpace($env:R2_ENDPOINT)) {
    $env:R2_ENDPOINT = "https://$cloudflareAccountId.r2.cloudflarestorage.com"
}
else {
    $env:R2_ENDPOINT = Normalize-R2Endpoint -Endpoint $env:R2_ENDPOINT
}

if (-not (Get-Command $PythonPath -ErrorAction SilentlyContinue)) {
    throw "python was not found on PATH. Install Python or pass -PythonPath."
}

Invoke-Tool -FilePath $PythonPath -Arguments @("-c", "import boto3") -WorkingDirectory $RepoRoot

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

    Assert-UploadedRunBundleComplete -Bucket $BucketName -RunId $RunId -BundleRoot $BundleRoot -TempDirectory $tempRoot

    $sqlPath = Join-Path $tempRoot "public-report-index.sql"
    Invoke-D1SqlFile -AccountId $cloudflareAccountId -DatabaseId $D1DatabaseId -ApiToken $cloudflareApiToken -Path $SchemaPath -Description "schema"

    $existingLatestRow = Get-D1LatestPublicationRow -AccountId $cloudflareAccountId -DatabaseId $D1DatabaseId -ApiToken $cloudflareApiToken
    $shouldUpdateLatest = Test-D1RunShouldUpdateLatest -LatestRow $existingLatestRow -Entry $entry
    Write-D1SqlFile -Entry $entry -Manifest $manifest -Warnings $warnings -SqlPath $sqlPath -PublishedAt $publishedAt -UpdateLatest $shouldUpdateLatest

    Invoke-D1SqlFile -AccountId $cloudflareAccountId -DatabaseId $D1DatabaseId -ApiToken $cloudflareApiToken -Path $sqlPath -Description "metadata"

    $publishedRunRows = @(Wait-D1PublishedRunRows -AccountId $cloudflareAccountId -DatabaseId $D1DatabaseId -ApiToken $cloudflareApiToken -RunId $RunId -CurrentEntry $entry)
    if ($publishedRunRows.Count -lt 1) {
        throw "D1 published run scan returned no runs after metadata indexing."
    }

    $registryEntries = @(Get-D1BackedRegistryEntries -RunRows $publishedRunRows -CurrentEntry $entry -Bucket $BucketName -TempDirectory $tempRoot)
    $mergedRegistry = Build-RegistryFromEntries -Entries $registryEntries
    Assert-RegistryIncludesRun -Registry $mergedRegistry -RunId $RunId -Description "Rebuilt public registry"
    $registryPath = Join-Path $tempRoot "report-index.json"
    $mergedRegistry | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $registryPath -Encoding utf8NoBOM
    Write-R2Object -ObjectKey "public/registry/report-index.json" -FilePath $registryPath

    $latestRow = Get-D1LatestPublicationRow -AccountId $cloudflareAccountId -DatabaseId $D1DatabaseId -ApiToken $cloudflareApiToken
    if ($null -eq $latestRow -or [string]::IsNullOrWhiteSpace([string]$latestRow.run_id)) {
        throw "D1 latest publication row is missing after metadata indexing."
    }

    $latestRunId = [string]$latestRow.run_id
    $latestEntry = @($registryEntries | Where-Object { [string]::Equals([string]$_.runId, $latestRunId, [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1)
    if ($latestEntry.Count -lt 1) {
        throw "D1 latest publication row references run '$latestRunId', but no matching registry entry was rebuilt."
    }

    $latestJson = Build-LatestObjectFromD1Run -Entry $latestEntry[0] -Row $latestRow
    $latestPath = Join-Path $tempRoot "latest.json"
    $latestJson | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $latestPath -Encoding utf8NoBOM
    Write-R2Object -ObjectKey "public/registry/latest.json" -FilePath $latestPath

    Assert-UploadedRegistryObjectsComplete -Bucket $BucketName -RunId $RunId -TempDirectory $tempRoot -ExpectedLatestRunId $latestRunId

    if ($VerifyPublishedRuns) {
        Assert-PublishedRunsComplete -Bucket $BucketName -AccountId $cloudflareAccountId -DatabaseId $D1DatabaseId -ApiToken $cloudflareApiToken -TempDirectory $tempRoot
    }
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
