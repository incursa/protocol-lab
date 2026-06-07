$ErrorActionPreference = "Stop"

$PackageRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PackageRoot "bin/win-x64/__ADAPTER_EXECUTABLE__.exe") @args
exit $LASTEXITCODE
