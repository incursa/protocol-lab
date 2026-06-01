<#
.SYNOPSIS
Builds the local quic-go HTTP/3 benchmark target Docker image.

.DESCRIPTION
Builds src/Incursa.ProtocolLab.Adapters.QuicGo/Dockerfile into the local
Docker image used by ProtocolLab Docker target mode. The image runs a Go-based
quic-go HTTP/3 server that serves the comparison suite endpoints and generates
its certificate material at runtime. The script does not run benchmarks, stage
files, commit changes, or write tracked artifacts.

.PARAMETER ImageTag
Docker image tag to build. Defaults to incursa/protocol-lab-quic-go-bench-server:local.

.PARAMETER NoCache
Pass --no-cache to docker build.

.PARAMETER VerboseOutput
Print the resolved Docker build command before running it.
#>
[CmdletBinding()]
param(
    [string]$ImageTag = "incursa/protocol-lab-quic-go-bench-server:local",
    [switch]$NoCache,
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$dockerfilePath = Join-Path $repoRoot "src\Incursa.ProtocolLab.Adapters.QuicGo\Dockerfile"
$buildContext = Join-Path $repoRoot "src\Incursa.ProtocolLab.Adapters.QuicGo"

if (-not (Test-Path -LiteralPath $dockerfilePath)) {
    throw "quic-go Dockerfile not found: $dockerfilePath"
}

$goModPath = Join-Path $buildContext "go.mod"
if (-not (Test-Path -LiteralPath $goModPath)) {
    throw "quic-go go.mod not found: $goModPath"
}

$docker = Get-Command docker -ErrorAction SilentlyContinue
if (-not $docker) {
    throw "Docker was not found on PATH. Install/start Docker Desktop before building the quic-go HTTP/3 target image."
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

Write-Host "quic-go HTTP/3 benchmark target image built: $ImageTag"
