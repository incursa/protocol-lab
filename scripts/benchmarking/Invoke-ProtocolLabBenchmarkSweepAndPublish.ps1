<#
.SYNOPSIS
Runs the ProtocolLab benchmark sweep and uploads completed runs to R2.

.DESCRIPTION
Runs the benchmark catalog through Invoke-ProtocolLabBenchmarkAll.ps1 for the
Regression and Comparison workflow profiles, then batch-publishes every
completed run matching the generated run ID prefix through
Publish-ProtocolLabRuns.ps1.

The benchmark scripts keep going when an individual stage fails. This wrapper
does the same: it uploads any completed runs that produced public evidence, then
writes a summary of the benchmark and publication stages.

.PARAMETER RunIdPrefix
Prefix used for generated benchmark run IDs. Each workflow profile appends a
lowercase profile segment before the suite suffix.

.PARAMETER IncludeQuick
Also run the Quick profile. Quick is the smallest public-report artifact proof
and is not part of the default sweep because it duplicates the smoke lane.

.PARAMETER IncludeAcceptance
Also run the v1 local acceptance workflow before publication. This can require
Docker and local h2load assets unless paired with the acceptance skip switches.

.PARAMETER VerifyUploadedObjects
Ask the R2 uploader to verify uploaded objects after each run upload.

.PARAMETER NoRestore
Pass --no-restore to dotnet build/test/run stages. Intended for tight
source-reference loops after the first restore.

.PARAMETER DryRun
Print planned benchmark and publication commands without running benchmarks or
uploading objects.

.PARAMETER FailOnError
Throw after the sweep summary is written if any benchmark or publication stage
failed.

.PARAMETER SkipR2CredentialPreflight
Skip the up-front R2 credential check. Use this only when credentials are made
available by a mechanism this wrapper cannot inspect but the uploader can.
#>
[CmdletBinding()]
param(
    [string]$RunIdPrefix = ("local-sweep-" + (Get-Date -Format "yyyyMMddHHmmss")),
    [string]$Output = ".artifacts\runs",
    [string]$PublicationOutputRoot = ".artifacts\publication",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$IncludeQuick,
    [switch]$IncludeAcceptance,
    [switch]$AcceptanceSkipManaged,
    [switch]$AcceptanceSkipExternal,
    [switch]$AcceptanceSkipCounters,
    [int]$DurationSeconds,
    [int]$WarmupSeconds,
    [int]$Repetitions,
    [int]$Connections,
    [int]$StreamsPerConnection,
    [string]$BaseUrl,
    [switch]$NoRestore,
    [switch]$VerifyUploadedObjects,
    [ValidateRange(1, 64)]
    [int]$UploadConcurrency = 8,
    [string]$PowerShellPath = "pwsh",
    [string]$R2CredentialsPath,
    [string]$R2CredentialsPathEnvironmentVariable = "PROTOCOL_LAB_R2_CREDENTIALS_PATH",
    [string]$R2SecretVault,
    [string]$R2AccessKeyIdSecretName = "ProtocolLab-R2-AccessKeyId",
    [string]$R2SecretAccessKeySecretName = "ProtocolLab-R2-SecretAccessKey",
    [string]$CloudflareAccountIdSecretName = "ProtocolLab-CloudflareAccountId",
    [string]$R2EndpointSecretName = "ProtocolLab-R2-Endpoint",
    [string]$R2SessionTokenSecretName = "ProtocolLab-R2-SessionToken",
    [switch]$RequirePublishable,
    [switch]$SkipR2CredentialPreflight,
    [switch]$DryRun,
    [switch]$FailOnError
)

$ErrorActionPreference = "Stop"
$script:AnyFailures = $false
$script:Results = New-Object System.Collections.Generic.List[object]

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$BenchmarkAllScript = Join-Path $RepoRoot "scripts\benchmarking\Invoke-ProtocolLabBenchmarkAll.ps1"
$AcceptanceScript = Join-Path $RepoRoot "scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1"
$PublishRunsScript = Join-Path $RepoRoot "scripts\publication\Publish-ProtocolLabRuns.ps1"
$OutputRoot = if ([System.IO.Path]::IsPathRooted($Output)) { [System.IO.Path]::GetFullPath($Output) } else { [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $Output)) }

function Add-SweepResult {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Status,
        [AllowNull()][object]$ExitCode,
        [AllowNull()][object]$CommandLine
    )

    $script:Results.Add([pscustomobject]@{
        Name = $Name
        Status = $Status
        ExitCode = $ExitCode
        CommandLine = $CommandLine
    }) | Out-Null
}

function Escape-MdCell {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    return ([string]$Value).Replace("|", "\|").Replace("`r`n", "<br>").Replace("`n", "<br>")
}

function Resolve-SweepPath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$BasePath
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
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

function Test-R2CredentialValues {
    param([hashtable]$Values)

    $hasAccessKey = (-not [string]::IsNullOrWhiteSpace($Values["AWS_ACCESS_KEY_ID"])) -or (-not [string]::IsNullOrWhiteSpace($Values["R2_ACCESS_KEY_ID"]))
    $hasSecretKey = (-not [string]::IsNullOrWhiteSpace($Values["AWS_SECRET_ACCESS_KEY"])) -or (-not [string]::IsNullOrWhiteSpace($Values["R2_SECRET_ACCESS_KEY"]))
    $hasEndpoint = -not [string]::IsNullOrWhiteSpace($Values["R2_ENDPOINT"])
    $hasAccountId = -not [string]::IsNullOrWhiteSpace($Values["CLOUDFLARE_ACCOUNT_ID"])

    return $hasAccessKey -and $hasSecretKey -and ($hasEndpoint -or $hasAccountId)
}

function Read-R2CredentialFileValues {
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

    return $values
}

function Test-R2CredentialPreflight {
    $environmentValues = @{
        AWS_ACCESS_KEY_ID = [Environment]::GetEnvironmentVariable("AWS_ACCESS_KEY_ID")
        AWS_SECRET_ACCESS_KEY = [Environment]::GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")
        R2_ENDPOINT = [Environment]::GetEnvironmentVariable("R2_ENDPOINT")
        CLOUDFLARE_ACCOUNT_ID = [Environment]::GetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID")
    }

    if (Test-R2CredentialValues -Values $environmentValues) {
        Write-Host "R2 credential preflight: environment variables"
        return
    }

    $credentialPath = $null
    if (-not [string]::IsNullOrWhiteSpace($R2CredentialsPath)) {
        $credentialPath = $R2CredentialsPath
    }
    elseif (-not [string]::IsNullOrWhiteSpace($R2CredentialsPathEnvironmentVariable)) {
        $credentialPath = [Environment]::GetEnvironmentVariable($R2CredentialsPathEnvironmentVariable)
    }

    if (-not [string]::IsNullOrWhiteSpace($credentialPath)) {
        $resolvedCredentialPath = Resolve-SweepPath -Path $credentialPath -BasePath $RepoRoot
        $fileValues = Read-R2CredentialFileValues -Path $resolvedCredentialPath
        if (Test-R2CredentialValues -Values $fileValues) {
            Write-Host "R2 credential preflight: $resolvedCredentialPath"
            return
        }

        throw "R2 credentials file is incomplete: $resolvedCredentialPath"
    }

    $secretValues = @{
        AWS_ACCESS_KEY_ID = Get-SecretManagementValue -Name $R2AccessKeyIdSecretName -VaultName $R2SecretVault
        AWS_SECRET_ACCESS_KEY = Get-SecretManagementValue -Name $R2SecretAccessKeySecretName -VaultName $R2SecretVault
        CLOUDFLARE_ACCOUNT_ID = Get-SecretManagementValue -Name $CloudflareAccountIdSecretName -VaultName $R2SecretVault
        R2_ENDPOINT = Get-SecretManagementValue -Name $R2EndpointSecretName -VaultName $R2SecretVault
        AWS_SESSION_TOKEN = Get-SecretManagementValue -Name $R2SessionTokenSecretName -VaultName $R2SecretVault
    }

    if (Test-R2CredentialValues -Values $secretValues) {
        Write-Host "R2 credential preflight: PowerShell SecretManagement"
        return
    }

    throw "R2 upload credentials are incomplete. Set AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY, and either CLOUDFLARE_ACCOUNT_ID or R2_ENDPOINT; or provide a credentials file with -R2CredentialsPath / $R2CredentialsPathEnvironmentVariable; or store them with PowerShell SecretManagement. Pass -SkipR2CredentialPreflight only if the uploader can resolve credentials by another mechanism."
}

function Invoke-SweepStage {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    Write-Host ""
    Write-Host "==> $Name"
    $commandLine = "$FilePath " + ($Arguments -join " ")
    Write-Host $commandLine

    if ($DryRun) {
        Add-SweepResult -Name $Name -Status "planned" -ExitCode $null -CommandLine $commandLine
        return
    }

    & $FilePath @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) {
        Add-SweepResult -Name $Name -Status "passed" -ExitCode $exitCode -CommandLine $commandLine
    }
    else {
        $script:AnyFailures = $true
        Add-SweepResult -Name $Name -Status "failed" -ExitCode $exitCode -CommandLine $commandLine
        Write-Warning "$Name failed with exit code $exitCode."
    }
}

function Write-SweepSummary {
    param([Parameter(Mandatory = $true)][string]$Path)

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# ProtocolLab Benchmark Sweep And Publication Summary")
    $lines.Add("")
    $lines.Add("Generated: $([DateTimeOffset]::UtcNow.ToString('u'))")
    $lines.Add("Run ID prefix: ``$RunIdPrefix``")
    $lines.Add("Runs root: ``$OutputRoot``")
    $lines.Add("")
    $lines.Add("| Stage | Status | Exit Code | Command |")
    $lines.Add("| --- | --- | --- | --- |")

    foreach ($result in $script:Results) {
        $lines.Add("| $(Escape-MdCell $result.Name) | $(Escape-MdCell $result.Status) | $(Escape-MdCell $result.ExitCode) | $(Escape-MdCell $result.CommandLine) |")
    }

    New-Item -ItemType Directory -Force (Split-Path -Parent $Path) | Out-Null
    Set-Content -LiteralPath $Path -Value $lines -Encoding utf8
}

function Copy-StageSummary {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$DestinationPath
    )

    if ($DryRun -or -not (Test-Path -LiteralPath $SourcePath)) {
        return
    }

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
    Write-Host "Stage summary: $DestinationPath"
}

if (-not (Test-Path -LiteralPath $BenchmarkAllScript)) {
    throw "Benchmark-all script not found: $BenchmarkAllScript"
}

if (-not (Test-Path -LiteralPath $PublishRunsScript)) {
    throw "Publication script not found: $PublishRunsScript"
}

Set-Location $RepoRoot
New-Item -ItemType Directory -Force $OutputRoot | Out-Null

if (-not $DryRun -and -not $SkipR2CredentialPreflight) {
    Test-R2CredentialPreflight
}

$profiles = New-Object System.Collections.Generic.List[string]
if ($IncludeQuick) {
    $profiles.Add("Quick") | Out-Null
}

$profiles.Add("Regression") | Out-Null
$profiles.Add("Comparison") | Out-Null

foreach ($profile in $profiles) {
    $profilePrefix = "$RunIdPrefix-$($profile.ToLowerInvariant())"
    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $BenchmarkAllScript,
        "-WorkflowProfile", $profile,
        "-RunIdPrefix", $profilePrefix,
        "-Output", $Output,
        "-PublicationOutputRoot", $PublicationOutputRoot,
        "-Configuration", $Configuration
    )

    if ($PSBoundParameters.ContainsKey("DurationSeconds")) {
        $arguments += @("-DurationSeconds", "$DurationSeconds")
    }

    if ($PSBoundParameters.ContainsKey("WarmupSeconds")) {
        $arguments += @("-WarmupSeconds", "$WarmupSeconds")
    }

    if ($PSBoundParameters.ContainsKey("Repetitions")) {
        $arguments += @("-Repetitions", "$Repetitions")
    }

    if ($PSBoundParameters.ContainsKey("Connections")) {
        $arguments += @("-Connections", "$Connections")
    }

    if ($PSBoundParameters.ContainsKey("StreamsPerConnection")) {
        $arguments += @("-StreamsPerConnection", "$StreamsPerConnection")
    }

    if (-not [string]::IsNullOrWhiteSpace($BaseUrl)) {
        $arguments += @("-BaseUrl", $BaseUrl)
    }

    if ($NoRestore) {
        $arguments += "-NoRestore"
    }

    if ($DryRun) {
        $arguments += "-DryRun"
    }

    if ($FailOnError) {
        $arguments += "-FailOnError"
    }

    Invoke-SweepStage -Name "Benchmark $profile profile" -FilePath "powershell" -Arguments $arguments
    Copy-StageSummary -SourcePath (Join-Path $OutputRoot "workflow-summary.md") -DestinationPath (Join-Path $OutputRoot "$profilePrefix-workflow-summary.md")
}

if ($IncludeAcceptance) {
    if (-not (Test-Path -LiteralPath $AcceptanceScript)) {
        throw "Acceptance script not found: $AcceptanceScript"
    }

    $acceptanceArguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $AcceptanceScript,
        "-RunIdPrefix", "$RunIdPrefix-acceptance",
        "-Output", $Output
    )

    if ($AcceptanceSkipManaged) {
        $acceptanceArguments += "-SkipManaged"
    }

    if ($AcceptanceSkipExternal) {
        $acceptanceArguments += "-SkipExternal"
    }

    if ($AcceptanceSkipCounters) {
        $acceptanceArguments += "-SkipCounters"
    }

    if ($PSBoundParameters.ContainsKey("DurationSeconds")) {
        $acceptanceArguments += @("-DurationSeconds", "$DurationSeconds")
    }

    if ($PSBoundParameters.ContainsKey("WarmupSeconds")) {
        $acceptanceArguments += @("-WarmupSeconds", "$WarmupSeconds")
    }

    if ($PSBoundParameters.ContainsKey("Repetitions")) {
        $acceptanceArguments += @("-Repetitions", "$Repetitions")
    }

    if ($PSBoundParameters.ContainsKey("Connections")) {
        $acceptanceArguments += @("-Connections", "$Connections")
    }

    if ($PSBoundParameters.ContainsKey("StreamsPerConnection")) {
        $acceptanceArguments += @("-StreamsPerConnection", "$StreamsPerConnection")
    }

    Invoke-SweepStage -Name "Acceptance workflow" -FilePath "powershell" -Arguments $acceptanceArguments
}

$publishArguments = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $PublishRunsScript,
    "-RunsRoot", $Output,
    "-PrefixFilter", $RunIdPrefix,
    "-UploadConcurrency", "$UploadConcurrency",
    "-PowerShellPath", $PowerShellPath,
    "-R2CredentialsPathEnvironmentVariable", $R2CredentialsPathEnvironmentVariable,
    "-R2AccessKeyIdSecretName", $R2AccessKeyIdSecretName,
    "-R2SecretAccessKeySecretName", $R2SecretAccessKeySecretName,
    "-CloudflareAccountIdSecretName", $CloudflareAccountIdSecretName,
    "-R2EndpointSecretName", $R2EndpointSecretName,
    "-R2SessionTokenSecretName", $R2SessionTokenSecretName
)

if ($VerifyUploadedObjects) {
    $publishArguments += "-VerifyUploadedObjects"
}

if (-not [string]::IsNullOrWhiteSpace($R2CredentialsPath)) {
    $publishArguments += @("-R2CredentialsPath", $R2CredentialsPath)
}

if (-not [string]::IsNullOrWhiteSpace($R2SecretVault)) {
    $publishArguments += @("-R2SecretVault", $R2SecretVault)
}

if ($RequirePublishable) {
    $publishArguments += "-RequirePublishable"
}

if ($DryRun) {
    $publishArguments += "-DryRun"
}

if ($FailOnError) {
    $publishArguments += "-FailOnError"
}

Invoke-SweepStage -Name "Publish completed runs to R2" -FilePath "powershell" -Arguments $publishArguments
Copy-StageSummary -SourcePath (Join-Path $OutputRoot "publication-summary.md") -DestinationPath (Join-Path $OutputRoot "$RunIdPrefix-publication-summary.md")

$summaryPath = Join-Path $OutputRoot "$RunIdPrefix-sweep-publication-summary.md"
Write-SweepSummary -Path $summaryPath

Write-Host ""
Write-Host "Sweep summary: $summaryPath"
Write-Host ""
Write-Host "Sweep results:"
$script:Results | Format-Table -AutoSize | Out-String | Write-Host

if ($FailOnError -and $script:AnyFailures) {
    throw "One or more benchmark sweep or publication stages failed. Review the sweep summary and run artifacts."
}
