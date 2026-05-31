<#
.SYNOPSIS
Builds the local nginx HTTP/3 benchmark target Docker image.

.DESCRIPTION
Builds servers/nginx/Dockerfile into the local Docker image used by
ProtocolLab Docker target mode. The script proves that the built image's
nginx binary advertises HTTP/3 module support through nginx -V before
reporting success. It does not run benchmarks, stage files, commit changes, or
write tracked artifacts.

.PARAMETER ImageTag
Docker image tag to build. Defaults to incursa/protocol-lab-nginx-bench-server:local.

.PARAMETER NoCache
Pass --no-cache to docker build.

.PARAMETER VerboseOutput
Print resolved Docker commands before running them.
#>
[CmdletBinding()]
param(
    [string]$ImageTag = "incursa/protocol-lab-nginx-bench-server:local",
    [switch]$NoCache,
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$dockerfilePath = Join-Path $repoRoot "servers\nginx\Dockerfile"
$buildContext = Join-Path $repoRoot "servers\nginx"

if (-not (Test-Path -LiteralPath $dockerfilePath)) {
    throw "nginx Dockerfile not found: $dockerfilePath"
}

$nginxConfPath = Join-Path $buildContext "nginx.conf"
if (-not (Test-Path -LiteralPath $nginxConfPath)) {
    throw "nginx.conf not found: $nginxConfPath"
}

$entrypointPath = Join-Path $buildContext "docker-entrypoint.sh"
if (-not (Test-Path -LiteralPath $entrypointPath)) {
    throw "nginx Docker entrypoint not found: $entrypointPath"
}

$docker = Get-Command docker -ErrorAction SilentlyContinue
if (-not $docker) {
    throw "Docker was not found on PATH. Install/start Docker Desktop before building the nginx HTTP/3 target image."
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

$versionArgs = @("run", "--rm", "--entrypoint", "nginx", $ImageTag, "-V")
if ($VerboseOutput) {
    Write-Host ("docker " + ($versionArgs -join " "))
}

Write-Host "Proving nginx HTTP/3 module support with nginx -V"
$versionStdout = [System.IO.Path]::GetTempFileName()
$versionStderr = [System.IO.Path]::GetTempFileName()
try {
    $versionProcess = Start-Process -FilePath "docker" -ArgumentList $versionArgs -NoNewWindow -Wait -PassThru -RedirectStandardOutput $versionStdout -RedirectStandardError $versionStderr
    $versionExitCode = $versionProcess.ExitCode
    $versionText = ((Get-Content -LiteralPath $versionStdout -Raw) + (Get-Content -LiteralPath $versionStderr -Raw))
}
finally {
    Remove-Item -LiteralPath $versionStdout, $versionStderr -Force -ErrorAction SilentlyContinue
}
Write-Host $versionText
if ($versionExitCode -ne 0) {
    throw "nginx -V proof failed with exit code $versionExitCode"
}

if ($versionText -notmatch "--with[-_]http_v3_module") {
    throw "Built nginx image '$ImageTag' does not advertise HTTP/3 support. Expected nginx -V output to include --with-http_v3_module or --with_http_v3_module."
}

$configArgs = @("run", "--rm", $ImageTag, "nginx", "-t")
if ($VerboseOutput) {
    Write-Host ("docker " + ($configArgs -join " "))
}

Write-Host "Validating nginx configuration"
& docker @configArgs
if ($LASTEXITCODE -ne 0) {
    throw "nginx configuration validation failed with exit code $LASTEXITCODE"
}

Write-Host "nginx HTTP/3 benchmark target image built: $ImageTag"
