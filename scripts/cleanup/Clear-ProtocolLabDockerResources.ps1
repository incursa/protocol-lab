[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"

function Invoke-Docker {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    if ($VerboseOutput) {
        Write-Host ("docker " + ($Arguments -join " "))
    }

    & docker @Arguments
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker was not found on PATH. Start Docker Desktop or install Docker before running cleanup."
}

$containers = @()
$containers += Invoke-Docker @("ps", "-a", "--filter", "label=incursa.protocol-lab.target=true", "--format", "{{.ID}} {{.Names}}")
$containers += Invoke-Docker @("ps", "-a", "--filter", "label=incursa.protocol-lab.load-tool=true", "--format", "{{.ID}} {{.Names}}")
$containers = $containers | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique

foreach ($container in $containers) {
    $id = ($container -split "\s+")[0]
    if ($PSCmdlet.ShouldProcess($container, "Remove ProtocolLab container")) {
        Invoke-Docker @("rm", "--force", $id) | Out-Host
    }
}

$networks = Invoke-Docker @("network", "ls", "--filter", "label=incursa.protocol-lab.network=true", "--format", "{{.ID}} {{.Name}}") |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Sort-Object -Unique

foreach ($network in $networks) {
    $id = ($network -split "\s+")[0]
    if ($PSCmdlet.ShouldProcess($network, "Remove ProtocolLab Docker network")) {
        Invoke-Docker @("network", "rm", $id) | Out-Host
    }
}

Write-Host "ProtocolLab Docker cleanup scan complete."
