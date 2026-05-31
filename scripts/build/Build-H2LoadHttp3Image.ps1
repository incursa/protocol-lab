<#
.SYNOPSIS
Builds and proves the repo-owned h2load HTTP/3 Docker image.

.DESCRIPTION
Builds the local h2load image from tools/h2load-http3/Dockerfile and verifies
that h2load runs and advertises the options required by ProtocolLab v1:
--h3, --output-file, --qlog-file-base, --connect-to, and --sni.

.PARAMETER Tag
Docker image tag to build. Defaults to incursa/protocol-lab-h2load-http3:local.

.PARAMETER Dockerfile
Dockerfile path relative to the repository root.

.PARAMETER NoCache
Pass --no-cache to docker build.
#>
[CmdletBinding()]
param(
    [string]$Tag = "incursa/protocol-lab-h2load-http3:local",
    [string]$Dockerfile = "tools/h2load-http3/Dockerfile",
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
    throw "Docker was not found on PATH. Install/start Docker Desktop before building the h2load HTTP/3 image."
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

Write-Host "Proving h2load version"
& docker run --rm $Tag h2load --version
if ($LASTEXITCODE -ne 0) {
    throw "h2load --version failed in $Tag"
}

$requiredOptions = @("--h3", "--output-file", "--qlog-file-base", "--connect-to", "--sni")
$help = & docker run --rm --entrypoint sh $Tag -c "h2load --help"
if ($LASTEXITCODE -ne 0) {
    throw "h2load --help failed in $Tag"
}

foreach ($option in $requiredOptions) {
    if (-not ($help -match [regex]::Escape($option))) {
        throw "h2load help in $Tag did not advertise required option $option"
    }
}

Write-Host "h2load HTTP/3 image proof passed for $Tag"
