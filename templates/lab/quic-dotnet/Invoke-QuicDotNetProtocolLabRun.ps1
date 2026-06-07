[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ProtocolLabRoot,

    [Parameter(Mandatory = $true)]
    [string] $ControllerUri,

    [string] $AdapterProject = "__ADAPTER_PROJECT__",

    [string] $AdapterExecutable = "__ADAPTER_EXECUTABLE__",

    [string] $Configuration = "Release",

    [string[]] $RuntimeIdentifier = @("linux-x64"),

    [string] $SuiteId = "h3-local-v1",

    [string[]] $ScenarioId = @("http.core.plaintext"),

    [string] $Protocol = "h3",

    [string] $LoadProfileId = "smoke",

    [int] $TimeoutSeconds = 1800
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$protocolLabRootFullPath = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ProtocolLabRoot).Path)
$submitScript = Join-Path $protocolLabRootFullPath "scripts/lab/Submit-ProtocolLabPackageRun.ps1"
if (-not (Test-Path -LiteralPath $submitScript -PathType Leaf)) {
    throw "ProtocolLab submit script was not found: $submitScript"
}

$packageResultJson = & pwsh -NoLogo -NoProfile -File (Join-Path $PSScriptRoot "New-QuicDotNetProtocolLabPackage.ps1") `
    -ProtocolLabRoot $protocolLabRootFullPath `
    -AdapterProject $AdapterProject `
    -AdapterExecutable $AdapterExecutable `
    -Configuration $Configuration `
    -RuntimeIdentifier $RuntimeIdentifier `
    -Force

$packageResult = $packageResultJson | ConvertFrom-Json
$resultRoot = Join-Path (Get-Location) "artifacts/protocol-lab/results"
New-Item -ItemType Directory -Force -Path $resultRoot | Out-Null

$artifactPath = Join-Path $resultRoot "latest.zip"
$jobResultJson = & pwsh -NoLogo -NoProfile -File $submitScript `
    -ControllerUri $ControllerUri `
    -PackagePath $packageResult.path `
    -ImplementationId "quic-dotnet-dev" `
    -SuiteId $SuiteId `
    -ScenarioId $ScenarioId `
    -Protocol $Protocol `
    -LoadProfileId $LoadProfileId `
    -TimeoutSeconds $TimeoutSeconds `
    -ArtifactOutputPath $artifactPath

$jobResult = $jobResultJson | ConvertFrom-Json
$jobResultPath = Join-Path $resultRoot "$($jobResult.jobId).json"
$jobResultJson | Set-Content -LiteralPath $jobResultPath

[ordered]@{
    package = $packageResult
    job = $jobResult
    jobResultPath = $jobResultPath
} | ConvertTo-Json -Depth 32
