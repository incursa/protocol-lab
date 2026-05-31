<#
.SYNOPSIS
Builds the local Caddy HTTP/3 benchmark target Docker image.

.DESCRIPTION
Builds servers/caddy/Dockerfile into the local Docker image used by
ProtocolLab Docker target mode. The image runs Caddy with the repo-local
Caddyfile, serves /plaintext and /json, and uses Caddy's internal local CA at
container runtime. The script does not run benchmarks, stage files, commit
changes, or write tracked artifacts.

.PARAMETER ImageTag
Docker image tag to build. Defaults to incursa/protocol-lab-caddy-bench-server:local.

.PARAMETER NoCache
Pass --no-cache to docker build.

.PARAMETER VerboseOutput
Print the resolved Docker build command before running it.
#>
[CmdletBinding()]
param(
    [string]$ImageTag = "incursa/protocol-lab-caddy-bench-server:local",
    [switch]$NoCache,
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$dockerfilePath = Join-Path $repoRoot "servers\caddy\Dockerfile"
$buildContext = Join-Path $repoRoot "servers\caddy"

if (-not (Test-Path -LiteralPath $dockerfilePath)) {
    throw "Caddy Dockerfile not found: $dockerfilePath"
}

$caddyfilePath = Join-Path $buildContext "Caddyfile"
if (-not (Test-Path -LiteralPath $caddyfilePath)) {
    throw "Caddyfile not found: $caddyfilePath"
}

$docker = Get-Command docker -ErrorAction SilentlyContinue
if (-not $docker) {
    throw "Docker was not found on PATH. Install/start Docker Desktop before building the Caddy HTTP/3 target image."
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

$buildArgs += $buildContext

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

Write-Host "Caddy HTTP/3 benchmark target image built: $ImageTag"
