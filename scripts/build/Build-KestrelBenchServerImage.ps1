<#
.SYNOPSIS
Builds the local Kestrel benchmark target Docker image.

.DESCRIPTION
Builds servers/KestrelBenchServer/Dockerfile into the local Docker image used
by ProtocolLab Docker target mode. The image runs the existing Kestrel
benchmark server with explicit H1 and H3 endpoints and generates a short-lived
local development certificate at container startup. The script does not run
benchmarks, stage files, commit changes, or write tracked artifacts.

.PARAMETER Tag
Docker image tag to build. Defaults to incursa/protocol-lab-kestrel-bench-server:local.

.PARAMETER Dockerfile
Dockerfile path relative to the repository root.

.PARAMETER NoCache
Pass --no-cache to docker build.
#>
[CmdletBinding()]
param(
    [string]$Tag = "incursa/protocol-lab-kestrel-bench-server:local",
    [string]$Dockerfile = "servers/KestrelBenchServer/Dockerfile",
    [switch]$NoCache
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$dockerfilePath = Join-Path $repoRoot $Dockerfile

if (-not (Test-Path -LiteralPath $dockerfilePath)) {
    throw "Dockerfile not found: $dockerfilePath"
}

$docker = Get-Command docker -ErrorAction SilentlyContinue
if (-not $docker) {
    throw "Docker was not found on PATH. Install/start Docker Desktop before building the Kestrel target image."
}

$buildArgs = @(
    "build",
    "--pull",
    "--tag", $Tag,
    "--file", $dockerfilePath
)

if ($NoCache) {
    $buildArgs += "--no-cache"
}

$buildArgs += $repoRoot

Write-Host "Building $Tag from $Dockerfile"
& docker @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "docker build failed with exit code $LASTEXITCODE"
}

Write-Host "Inspecting $Tag"
& docker image inspect $Tag *> $null
if ($LASTEXITCODE -ne 0) {
    throw "docker image inspect failed for $Tag"
}

Write-Host "Kestrel benchmark target image built: $Tag"
