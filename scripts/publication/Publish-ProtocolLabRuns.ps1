<#
.SYNOPSIS
Publishes one or more completed ProtocolLab runs to R2/D1.

.DESCRIPTION
Scans completed run directories under the selected runs root and invokes
Publish-ProtocolLabReport.ps1 for each one. The script is batch-friendly: it
keeps going when a single run fails so the rest of the completed runs still get
published. By default it treats the uploads as diagnostic local evidence and
passes the diagnostic publication flag through to the underlying publisher.

.PARAMETER RunsRoot
Root directory that contains completed run folders. Defaults to .artifacts\runs.

.PARAMETER RunIds
Optional comma-separated explicit run IDs to publish. When supplied, only
those runs are published.

.PARAMETER PrefixFilter
Optional prefix filter for run IDs. When supplied, only runs whose directory
name starts with the prefix are published.

.PARAMETER RequirePublishable
Do not pass the diagnostic publication flag through to the underlying publisher.
Use this when the run bundle is expected to satisfy the publishable gate.

.PARAMETER VerifyPublishedRuns
Ask the underlying publisher to verify the uploaded payloads after publishing.

.PARAMETER DryRun
Validate the selected runs and print the planned publication commands without
uploading anything.

.PARAMETER FailOnError
Throw after the summary is written if any selected run failed to publish.
#>
[CmdletBinding()]
param(
    [string]$RunsRoot = ".artifacts\runs",
    [string]$RunIds,
    [string]$PrefixFilter,
    [switch]$RequirePublishable,
    [switch]$VerifyPublishedRuns,
    [switch]$DryRun,
    [switch]$FailOnError
)

$ErrorActionPreference = "Stop"
$script:AnyFailures = $false
$script:Results = New-Object System.Collections.Generic.List[object]

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$RunsRootPath = if ([System.IO.Path]::IsPathRooted($RunsRoot)) { $RunsRoot } else { Join-Path $RepoRoot $RunsRoot }
$PublishScript = Join-Path $RepoRoot "scripts\publication\Publish-ProtocolLabReport.ps1"

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

function Write-StageHeader {
    param([Parameter(Mandatory = $true)][string]$Name)

    Write-Host ""
    Write-Host "==> $Name"
}

function Escape-MdCell {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    return ([string]$Value).Replace("|", "\|").Replace("`r`n", "<br>").Replace("`n", "<br>")
}

function Add-Result {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Status,
        [AllowNull()][object]$ExitCode,
        [AllowNull()][object]$RunRoot,
        [AllowNull()][object]$BundleRoot,
        [AllowNull()][object]$CommandLine
    )

    $script:Results.Add([pscustomobject]@{
        Name = $Name
        Status = $Status
        ExitCode = $ExitCode
        RunRoot = $RunRoot
        BundleRoot = $BundleRoot
        CommandLine = $CommandLine
    }) | Out-Null
}

function Invoke-PublishStage {
    param(
        [Parameter(Mandatory = $true)][string]$RunRoot,
        [Parameter(Mandatory = $true)][string]$RunId
    )

    $bundleRoot = Join-Path (Join-Path $RepoRoot ".artifacts\publication") $RunId
    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $PublishScript,
        "-RunId", $RunId,
        "-RunRoot", $RunRoot,
        "-BundleRoot", $bundleRoot
    )

    if (-not $RequirePublishable) {
        $arguments += "-AllowDiagnosticPublication"
    }

    if ($VerifyPublishedRuns) {
        $arguments += "-VerifyPublishedRuns"
    }

    if ($DryRun) {
        $arguments += "-DryRun"
    }

    Write-StageHeader -Name "Publish run $RunId"
    $commandLine = "powershell " + ($arguments -join " ")
    Write-Host $commandLine

    if ($DryRun) {
        Add-Result -Name $RunId -Status "planned" -ExitCode $null -RunRoot $RunRoot -BundleRoot $bundleRoot -CommandLine $commandLine
        return
    }

    & powershell @arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) {
        Add-Result -Name $RunId -Status "passed" -ExitCode $exitCode -RunRoot $RunRoot -BundleRoot $bundleRoot -CommandLine $commandLine
        Write-Host "Published run root: $RunRoot"
        Write-Host "Bundle root: $bundleRoot"
    }
    else {
        $script:AnyFailures = $true
        Add-Result -Name $RunId -Status "failed" -ExitCode $exitCode -RunRoot $RunRoot -BundleRoot $bundleRoot -CommandLine $commandLine
        Write-Warning "Publishing run $RunId failed with exit code $exitCode."
    }
}

function Write-PublishSummary {
    param([Parameter(Mandatory = $true)][string]$Path)

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# ProtocolLab Publication Summary")
    $lines.Add("")
    $lines.Add("Generated: $([DateTimeOffset]::UtcNow.ToString('u'))")
    $lines.Add("Runs root: ``$RunsRootPath``")
    $lines.Add("")
    $lines.Add("| Run ID | Status | Exit Code | Run Root | Bundle Root |")
    $lines.Add("| --- | --- | --- | --- | --- |")

    foreach ($result in $script:Results) {
        $lines.Add("| $(Escape-MdCell $result.Name) | $(Escape-MdCell $result.Status) | $(Escape-MdCell $result.ExitCode) | $(Escape-MdCell $result.RunRoot) | $(Escape-MdCell $result.BundleRoot) |")
    }

    New-Item -ItemType Directory -Force (Split-Path -Parent $Path) | Out-Null
    Set-Content -LiteralPath $Path -Value $lines -Encoding utf8
}

if (-not (Test-Path -LiteralPath $RunsRootPath)) {
    throw "Runs root not found: $RunsRootPath"
}

Set-Location $RepoRoot

$selectedRunRoots = New-Object System.Collections.Generic.List[string]
if (-not [string]::IsNullOrWhiteSpace($RunIds)) {
    foreach ($id in @($RunIds -split ',')) {
        $trimmed = $id.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        $id = $trimmed
        $path = Join-Path $RunsRootPath $id
        if (-not (Test-Path -LiteralPath $path)) {
            $script:AnyFailures = $true
            Add-Result -Name $id -Status "failed" -ExitCode 1 -RunRoot $path -BundleRoot $null -CommandLine $null
            Write-Warning "Run directory not found: $path"
            continue
        }

        $selectedRunRoots.Add($path) | Out-Null
    }
}
else {
    foreach ($runDirectory in Get-ChildItem -LiteralPath $RunsRootPath -Directory) {
        if ($PrefixFilter -and -not $runDirectory.Name.StartsWith($PrefixFilter, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        if (-not (Test-Path -LiteralPath (Join-Path $runDirectory.FullName "evidence-report-v1.json"))) {
            continue
        }

        $selectedRunRoots.Add($runDirectory.FullName) | Out-Null
    }
}

if ($selectedRunRoots.Count -lt 1) {
    throw "No completed runs were selected for publication."
}

foreach ($runRoot in $selectedRunRoots) {
    $runId = [System.IO.Path]::GetFileName($runRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar))
    Invoke-PublishStage -RunRoot $runRoot -RunId $runId
}

$summaryPath = Join-Path $RunsRootPath "publication-summary.md"
Write-PublishSummary -Path $summaryPath
Write-Host ""
Write-Host "Publication summary: $summaryPath"
Write-Host ""
Write-Host "Publication results:"
$script:Results | Format-Table -AutoSize | Out-String | Write-Host

if ($FailOnError -and $script:AnyFailures) {
    throw "One or more selected runs failed to publish. Review the publication summary and run artifacts."
}
