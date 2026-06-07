[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ProtocolLabRoot,

    [string] $AdapterProject = "__ADAPTER_PROJECT__",

    [string] $AdapterExecutable = "__ADAPTER_EXECUTABLE__",

    [string] $Configuration = "Release",

    [string[]] $RuntimeIdentifier = @("linux-x64"),

    [string] $PackageVersion,

    [string] $OutputPath,

    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-DefaultPackageVersion {
    $timestamp = Get-Date -AsUTC -Format "yyyyMMddTHHmmssZ"
    $shortSha = "nogit"
    $dirty = "unknown"

    try {
        $shortSha = (git rev-parse --short HEAD 2>$null).Trim()
        $dirty = if ((git status --porcelain 2>$null).Length -gt 0) { "dirty" } else { "clean" }
    }
    catch {
        $shortSha = "nogit"
        $dirty = "unknown"
    }

    return "dev-$timestamp-$shortSha-$dirty"
}

if ($AdapterProject -eq "__ADAPTER_PROJECT__") {
    throw "Set -AdapterProject to the quic-dotnet ProtocolLab adapter project path."
}

if ($AdapterExecutable -eq "__ADAPTER_EXECUTABLE__") {
    throw "Set -AdapterExecutable to the adapter binary name without .exe."
}

$protocolLabRootFullPath = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ProtocolLabRoot).Path)
$packageBuilder = Join-Path $protocolLabRootFullPath "scripts/lab/New-ProtocolLabPackage.ps1"
if (-not (Test-Path -LiteralPath $packageBuilder -PathType Leaf)) {
    throw "ProtocolLab package builder was not found: $packageBuilder"
}

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $PackageVersion = Get-DefaultPackageVersion
}

$repoRoot = [System.IO.Path]::GetFullPath((Get-Location).Path)
$stageRoot = Join-Path $repoRoot "artifacts/protocol-lab/package-source/$PackageVersion"
$publishRoot = Join-Path $repoRoot "artifacts/protocol-lab/publish/$PackageVersion"
$templateRoot = Join-Path $PSScriptRoot "templates"

if (-not (Test-Path -LiteralPath $templateRoot -PathType Container)) {
    throw "Template directory was not found: $templateRoot"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "artifacts/protocol-lab/packages/quic-dotnet-dev.$PackageVersion.plabpkg"
}

Remove-Item -LiteralPath $stageRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null

Copy-Item -LiteralPath (Join-Path $templateRoot "protocol-lab-package.json") -Destination (Join-Path $stageRoot "protocol-lab-package.json")
Copy-Item -LiteralPath (Join-Path $templateRoot "implementations") -Destination (Join-Path $stageRoot "implementations") -Recurse
Copy-Item -LiteralPath (Join-Path $templateRoot "scripts") -Destination (Join-Path $stageRoot "scripts") -Recurse

$manifestPath = Join-Path $stageRoot "protocol-lab-package.json"
(Get-Content -LiteralPath $manifestPath -Raw).Replace("__PACKAGE_VERSION__", $PackageVersion) |
    Set-Content -LiteralPath $manifestPath -NoNewline

foreach ($scriptPath in Get-ChildItem -LiteralPath (Join-Path $stageRoot "scripts") -File -Recurse) {
    (Get-Content -LiteralPath $scriptPath.FullName -Raw).Replace("__ADAPTER_EXECUTABLE__", $AdapterExecutable) |
        Set-Content -LiteralPath $scriptPath.FullName -NoNewline
}

foreach ($rid in $RuntimeIdentifier) {
    $publishOutput = Join-Path $publishRoot $rid
    $publishLog = & dotnet publish $AdapterProject -c $Configuration -r $rid --self-contained false -o $publishOutput 2>&1
    if ($LASTEXITCODE -ne 0) {
        $publishLog | Write-Error
        throw "dotnet publish failed for runtime identifier '$rid'."
    }

    $binOutput = Join-Path $stageRoot "bin/$rid"
    New-Item -ItemType Directory -Force -Path $binOutput | Out-Null
    Get-ChildItem -LiteralPath $publishOutput -Force | Copy-Item -Destination $binOutput -Recurse -Force
}

& pwsh -NoLogo -NoProfile -File $packageBuilder -SourcePath $stageRoot -OutputPath $OutputPath -Force:$Force
