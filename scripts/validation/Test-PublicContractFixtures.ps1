[CmdletBinding()]
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,

    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

function Invoke-ProtocolLabConformance {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [switch]$ShouldFail
    )

    $command = @(
        'run',
        '--project',
        (Join-Path $Root 'src/Incursa.ProtocolLab.Cli'),
        '--configuration',
        $Configuration,
        '--'
    ) + $Arguments

    & dotnet @command
    $exitCode = $LASTEXITCODE
    if ($ShouldFail) {
        if ($exitCode -eq 0) {
            throw "Expected conformance command to fail: dotnet $($command -join ' ')"
        }

        Write-Host "Expected failure observed: dotnet $($command -join ' ')"
        return
    }

    if ($exitCode -ne 0) {
        throw "Conformance command failed with exit code ${exitCode}: dotnet $($command -join ' ')"
    }
}

$packageRoot = Join-Path $Root 'fixtures/public-contracts/packages'
$validPackages = Get-ChildItem -LiteralPath $packageRoot -Directory |
    Where-Object { $_.Name -ne 'invalid' } |
    Sort-Object Name

if (-not $validPackages) {
    throw "No valid package fixtures found under $packageRoot."
}

foreach ($package in $validPackages) {
    Invoke-ProtocolLabConformance -Arguments @(
        'conformance',
        'package',
        '--package',
        $package.FullName,
        '--root',
        $Root
    )
}

$invalidPackageRoot = Join-Path $packageRoot 'invalid'
if (Test-Path -LiteralPath $invalidPackageRoot -PathType Container) {
    foreach ($package in Get-ChildItem -LiteralPath $invalidPackageRoot -Directory | Sort-Object Name) {
        Invoke-ProtocolLabConformance -ShouldFail -Arguments @(
            'conformance',
            'package',
            '--package',
            $package.FullName,
            '--root',
            $Root
        )
    }
}

function Get-RunPlanPackageFixtures {
    param(
        [Parameter(Mandatory)]
        [string]$RunPlanPath
    )

    $runPlan = Get-Content -LiteralPath $RunPlanPath -Raw | ConvertFrom-Json
    $packageIds = @($runPlan.packages | ForEach-Object { $_.packageId } | Where-Object { $_ } | Sort-Object -Unique)
    $resolved = @()

    foreach ($packageId in $packageIds) {
        $matches = @($validPackages | Where-Object {
            $manifestPath = Join-Path $_.FullName 'protocol-lab-package.json'
            if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
                return $false
            }

            $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
            return [string]::Equals($manifest.packageId, $packageId, [System.StringComparison]::OrdinalIgnoreCase)
        })

        if ($matches.Count -ne 1) {
            throw "Run plan '$RunPlanPath' references packageId '$packageId', but found $($matches.Count) matching fixture package directories."
        }

        $resolved += $matches[0].FullName
    }

    return $resolved
}

$validRunPlanRoot = Join-Path $Root 'fixtures/public-contracts/run-plans/valid'
foreach ($runPlan in Get-ChildItem -LiteralPath $validRunPlanRoot -File -Filter '*.json' | Sort-Object Name) {
    $runPlanPackages = Get-RunPlanPackageFixtures -RunPlanPath $runPlan.FullName
    $arguments = @(
        'conformance',
        'run-plan',
        '--run-plan',
        $runPlan.FullName,
        '--root',
        $Root
    )
    foreach ($package in $runPlanPackages) {
        $arguments += @('--package', $package)
    }

    Invoke-ProtocolLabConformance -Arguments $arguments
}

$invalidRunPlanRoot = Join-Path $Root 'fixtures/public-contracts/run-plans/invalid'
if (Test-Path -LiteralPath $invalidRunPlanRoot -PathType Container) {
    foreach ($runPlan in Get-ChildItem -LiteralPath $invalidRunPlanRoot -File -Filter '*.json' | Sort-Object Name) {
        $runPlanPackages = Get-RunPlanPackageFixtures -RunPlanPath $runPlan.FullName
        $arguments = @(
            'conformance',
            'run-plan',
            '--run-plan',
            $runPlan.FullName,
            '--root',
            $Root
        )
        foreach ($package in $runPlanPackages) {
            $arguments += @('--package', $package)
        }

        Invoke-ProtocolLabConformance -ShouldFail -Arguments $arguments
    }
}

Write-Host "Public contract package and run-plan fixtures passed conformance gates."
