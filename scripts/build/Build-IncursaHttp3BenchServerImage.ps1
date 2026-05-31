<#
.SYNOPSIS
Builds the local Incursa HTTP/3 benchmark target Docker image.

.DESCRIPTION
Builds the repo-owned Incursa HTTP/3 endpoint project into the Docker image
used by ProtocolLab Docker target mode. The image runs the endpoint mode
directly, publishes the HTTP/3 UDP port, and uses the runtime-generated
loopback self-signed certificate. The script does not run benchmarks, stage
files, commit changes, or write tracked artifacts.

.PARAMETER ImageTag
Docker image tag to build. Defaults to incursa/protocol-lab-incursa-http3-bench-server:local.

.PARAMETER NoCache
Pass --no-cache to docker build.

.PARAMETER VerboseOutput
Print the resolved Docker build command before running it.
#>
[CmdletBinding()]
param(
    [string]$ImageTag = "incursa/protocol-lab-incursa-http3-bench-server:local",
    [switch]$NoCache,
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$dockerfilePath = Join-Path $RepoRoot "src\Incursa.ProtocolLab.Adapters.IncursaHttp3\Dockerfile"

if (-not (Test-Path -LiteralPath $dockerfilePath)) {
    throw "Incursa HTTP/3 Dockerfile not found: $dockerfilePath."
}

$docker = Get-Command docker -ErrorAction SilentlyContinue
if (-not $docker) {
    throw "Docker was not found on PATH. Install/start Docker Desktop before building the Incursa HTTP/3 target image."
}

$buildArgs = @(
    "build",
    "--pull",
    "--tag", $ImageTag,
    "--file", $dockerfilePath
)

if ($NoCache) {
    $buildArgs += "--no-cache"
}

$buildArgs += $RepoRoot

if ($VerboseOutput) {
    Write-Host ("docker " + ($buildArgs -join " "))
}

Write-Host "Building $ImageTag from $dockerfilePath"
& docker @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "docker build failed with exit code $LASTEXITCODE"
}

Write-Host "Inspecting $ImageTag"
& docker image inspect $ImageTag *> $null
if ($LASTEXITCODE -ne 0) {
    throw "docker image inspect failed for $ImageTag"
}

Write-Host "Incursa HTTP/3 benchmark target image built: $ImageTag"
