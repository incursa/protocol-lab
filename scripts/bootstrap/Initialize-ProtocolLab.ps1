<#
.SYNOPSIS
Initializes a local ProtocolLab checkout for v1 development and acceptance.

.DESCRIPTION
Restores repo-local .NET tools, verifies Docker when enabled, optionally builds
the repo-owned h2load HTTP/3 Docker image and public reference target images,
builds the solution, and runs ProtocolLab check. The script does not run
benchmarks, stage files, commit changes, or write tracked artifacts.

.PARAMETER BuildH2LoadImage
Build and prove the repo-owned incursa/protocol-lab-h2load-http3:local image.

.PARAMETER BuildTargetImages
Build the repo-local Kestrel Docker target image used by ProtocolLab target Docker mode.

.PARAMETER SkipDocker
Skip Docker verification. Cannot be combined with image build switches.

.PARAMETER SkipBuild
Skip dotnet build.

.PARAMETER SkipCheck
Skip ProtocolLab check.

.PARAMETER VerboseOutput
Reserved for compatibility; native command output is streamed directly.
#>
[CmdletBinding()]
param(
    [switch]$BuildH2LoadImage,
    [switch]$BuildTargetImages,
    [switch]$SkipDocker,
    [switch]$SkipBuild,
    [switch]$SkipCheck,
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$Solution = Join-Path $RepoRoot "Incursa.ProtocolLab.sln"
$H2LoadImageScript = Join-Path $RepoRoot "scripts\build\Build-H2LoadHttp3Image.ps1"
$KestrelTargetImageScript = Join-Path $RepoRoot "scripts\build\Build-KestrelBenchServerImage.ps1"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message"
}

function Invoke-ProtocolLabCommand {
    param(
        [string]$Label,
        [string]$FilePath,
        [string[]]$Arguments,
        [switch]$Required
    )

    Write-Step $Label
    Write-Host ("$FilePath " + ($Arguments -join " "))

    & $FilePath @Arguments

    if ($LASTEXITCODE -ne 0) {
        $message = "$Label failed with exit code $LASTEXITCODE."
        if ($Required) {
            throw $message
        }

        Write-Warning $message
    }
}

Set-Location $RepoRoot

Write-Step "Verifying .NET SDK"
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw ".NET SDK was not found on PATH. Install the .NET SDK, reopen the shell, and rerun this script."
}
dotnet --version
if ($LASTEXITCODE -ne 0) {
    throw "dotnet --version failed. Verify the .NET SDK installation."
}

Write-Step "Restoring repo-local .NET tools"
Invoke-ProtocolLabCommand -Label "dotnet tool restore" -FilePath "dotnet" -Arguments @("tool", "restore") -Required

if (-not $SkipDocker) {
    Write-Step "Verifying Docker"
    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if (-not $docker) {
        throw "Docker was not found on PATH. Install/start Docker Desktop, or rerun with -SkipDocker for a non-Docker bootstrap."
    }

    Invoke-ProtocolLabCommand -Label "docker version" -FilePath "docker" -Arguments @("version", "--format", "{{.Server.Version}}") -Required

    if ($BuildH2LoadImage) {
        if (-not (Test-Path -LiteralPath $H2LoadImageScript)) {
            throw "h2load image build script not found: $H2LoadImageScript"
        }

        Invoke-ProtocolLabCommand -Label "Build repo-owned h2load HTTP/3 Docker image" -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $H2LoadImageScript) -Required
    }

    if ($BuildTargetImages) {
        if (-not (Test-Path -LiteralPath $KestrelTargetImageScript)) {
            throw "Kestrel target image build script not found: $KestrelTargetImageScript"
        }

        Invoke-ProtocolLabCommand -Label "Build Kestrel Docker target image" -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $KestrelTargetImageScript) -Required
    }

}
elseif ($BuildH2LoadImage -or $BuildTargetImages) {
    throw "-BuildH2LoadImage and -BuildTargetImages cannot be combined with -SkipDocker."
}

if (-not $SkipBuild) {
    Invoke-ProtocolLabCommand -Label "Build ProtocolLab solution" -FilePath "dotnet" -Arguments @("build", $Solution) -Required
}

if (-not $SkipCheck) {
    Invoke-ProtocolLabCommand -Label "Run ProtocolLab check" -FilePath "dotnet" -Arguments @("run", "--project", "src\Incursa.ProtocolLab.Cli", "--", "check") -Required
}

Write-Host ""
Write-Host "ProtocolLab bootstrap completed."
Write-Host ""
Write-Host "Next commands:"
Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 -RunIdPrefix local-v1-acceptance -DurationSeconds 5 -WarmupSeconds 1 -Repetitions 1"
Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 -RunIdPrefix local-phase3b-docker -TargetMode docker -BuildTargetImages -DurationSeconds 5 -WarmupSeconds 1 -Repetitions 1"
Write-Host "  dotnet run --project src\Incursa.ProtocolLab.Cli -- check"
Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analysis\New-ProtocolLabRunIndex.ps1 -RunsRoot .artifacts\runs"
