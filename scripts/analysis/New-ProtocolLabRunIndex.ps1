<#
.SYNOPSIS
Creates a compact markdown index for ProtocolLab run artifacts.

.DESCRIPTION
Scans one run or all runs under .artifacts\runs and writes index.md with links
to validation summaries, run summaries, aggregate JSON, evidence classes,
comparability status, and selected warnings. Raw load-tool output remains in
per-cell artifacts and is not copied into the index.

.PARAMETER RunsRoot
Run artifact root to scan. Defaults to .artifacts\runs.

.PARAMETER RunId
Optional single run ID to index.

.PARAMETER OutputPath
Optional output markdown path. Defaults to the selected run's index.md or the
runs root index.md.
#>
[CmdletBinding()]
param(
    [string]$RunsRoot = ".artifacts\runs",
    [string]$RunId,
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$ResolvedRunsRoot = if ([System.IO.Path]::IsPathRooted($RunsRoot)) { $RunsRoot } else { Join-Path $RepoRoot $RunsRoot }

function ConvertTo-RelativePath {
    param([string]$Path)

    $root = [System.IO.Path]::GetFullPath($RepoRoot)
    $full = [System.IO.Path]::GetFullPath($Path)
    if (-not $root.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $root += [System.IO.Path]::DirectorySeparatorChar
    }

    $rootUri = New-Object System.Uri($root)
    $fullUri = New-Object System.Uri($full)
    return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($fullUri).ToString()).Replace("/", [System.IO.Path]::DirectorySeparatorChar)
}

function Add-RunSection {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [System.IO.DirectoryInfo]$RunDirectory
    )

    $runName = $RunDirectory.Name
    $summary = Join-Path $RunDirectory.FullName "summary.md"
    $aggregate = Join-Path $RunDirectory.FullName "aggregate-results.json"
    $validation = Join-Path $RunDirectory.FullName "validation-results.json"

    $Lines.Add("## $runName")

    if (Test-Path -LiteralPath $summary) {
        $summaryRelative = ConvertTo-RelativePath $summary
        $Lines.Add("- Summary: ``$summaryRelative``")
    }

    if (Test-Path -LiteralPath $aggregate) {
        $aggregateRelative = ConvertTo-RelativePath $aggregate
        $Lines.Add("- Aggregate JSON: ``$aggregateRelative``")
        $report = Get-Content -LiteralPath $aggregate -Raw | ConvertFrom-Json
        $Lines.Add("- Results: $($report.totals.resultCount); aggregates: $($report.totals.aggregateCount); parsed metrics: $($report.totals.parsedMetricsCount)")
        foreach ($aggregateResult in @($report.aggregates)) {
            $evidenceClass = if ($aggregateResult.evidence) { $aggregateResult.evidence.evidenceClass } else { "n/a" }
            $comparability = if ($aggregateResult.evidence) { $aggregateResult.evidence.comparabilityStatus } else { "n/a" }
            $benchmarkStatuses = ($aggregateResult.benchmarkExecutionStatuses.PSObject.Properties | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ", "
            $warnings = @($aggregateResult.warnings)
            $Lines.Add("- $($aggregateResult.implementationId) / $($aggregateResult.scenarioId) / $($aggregateResult.protocol) / $($aggregateResult.loadTool): evidence=$evidenceClass; comparability=$comparability; benchmark=$benchmarkStatuses")
            if ($warnings.Count -gt 0) {
                $Lines.Add("  - Warnings: " + (($warnings | Select-Object -First 5) -join " | "))
            }
        }
    }

    if (Test-Path -LiteralPath $validation) {
        $validationRelative = ConvertTo-RelativePath $validation
        $Lines.Add("- Validation results: ``$validationRelative``")
        $validations = Get-Content -LiteralPath $validation -Raw | ConvertFrom-Json
        $passed = @($validations | Where-Object { $_.status -eq "passed" }).Count
        $failed = @($validations | Where-Object { $_.status -eq "failed" }).Count
        $unsupported = @($validations | Where-Object { $_.status -eq "unsupported" }).Count
        $Lines.Add("- Validation counts: passed=$passed; failed=$failed; unsupported=$unsupported")
    }

    $cellResults = @(Get-ChildItem -LiteralPath $RunDirectory.FullName -Recurse -Filter "result.json" -File -ErrorAction SilentlyContinue)
    foreach ($resultFile in $cellResults) {
        $result = Get-Content -LiteralPath $resultFile.FullName -Raw | ConvertFrom-Json
        $evidenceClass = if ($result.evidence) { $result.evidence.evidenceClass } else { "n/a" }
        $comparability = if ($result.evidence) { $result.evidence.comparabilityStatus } else { "n/a" }
        $Lines.Add("- Cell: $($result.implementationId) / $($result.scenarioId) / $($result.protocol) / $($result.loadTool) -> $($result.benchmarkExecutionStatus); evidence=$evidenceClass; comparability=$comparability")
    }

    if (-not (Test-Path -LiteralPath $summary) -and -not (Test-Path -LiteralPath $aggregate) -and -not (Test-Path -LiteralPath $validation)) {
        $Lines.Add("- No ProtocolLab summary, aggregate, or validation result file found.")
    }

    $Lines.Add("")
}

if (-not (Test-Path -LiteralPath $ResolvedRunsRoot)) {
    throw "Runs root not found: $ResolvedRunsRoot"
}

$runDirectories = if ($RunId) {
    $path = Join-Path $ResolvedRunsRoot $RunId
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Run directory not found: $path"
    }

    @(Get-Item -LiteralPath $path)
}
else {
    @(Get-ChildItem -LiteralPath $ResolvedRunsRoot -Directory | Sort-Object LastWriteTime -Descending)
}

if (-not $OutputPath) {
    $OutputPath = if ($RunId) {
        Join-Path (Join-Path $ResolvedRunsRoot $RunId) "index.md"
    }
    else {
        Join-Path $ResolvedRunsRoot "index.md"
    }
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# ProtocolLab Run Index")
$lines.Add("")
$lines.Add("Generated: $([DateTimeOffset]::UtcNow.ToString("u"))")
$runsRootRelative = ConvertTo-RelativePath $ResolvedRunsRoot
$lines.Add("Runs root: ``$runsRootRelative``")
$lines.Add("")

foreach ($runDirectory in $runDirectories) {
    Add-RunSection -Lines $lines -RunDirectory $runDirectory
}

New-Item -ItemType Directory -Force (Split-Path -Parent $OutputPath) | Out-Null
Set-Content -LiteralPath $OutputPath -Value $lines -Encoding UTF8
Write-Host "Wrote run index: $OutputPath"
