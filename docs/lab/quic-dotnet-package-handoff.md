# Preparing quic-dotnet for ProtocolLab Lab Runs

This is the handoff plan for adding `.plabpkg` production and lab-run submission to the `quic-dotnet` repo.

The target workflow is:

1. Publish the `quic-dotnet` ProtocolLab adapter from the current working tree.
2. Stage a ProtocolLab Lab Package source directory.
3. Build a `.plabpkg` archive.
4. Upload it to the lab controller.
5. Submit a benchmark job referencing the exact package SHA-256.
6. Poll for completion and download artifacts.

## Add These Files to quic-dotnet

Recommended files:

```text
eng/protocol-lab/
  New-QuicDotNetProtocolLabPackage.ps1
  Invoke-QuicDotNetProtocolLabRun.ps1
  templates/
    protocol-lab-package.json
    implementations/
      quic-dotnet-dev.yaml
    scripts/
      run-current-platform.ps1
      run-linux.sh
      run-windows.ps1
```

Starter versions of these files live in `templates/lab/quic-dotnet/` in this repo. Copy that directory to `quic-dotnet/eng/protocol-lab/`, then replace:

- `__ADAPTER_PROJECT__` with the ProtocolLab adapter project path in `quic-dotnet`.
- `__ADAPTER_EXECUTABLE__` with the adapter binary name without `.exe`.
- Manifest protocol/capability fields if the adapter only supports raw QUIC or only supports HTTP/3.

The scripts in `protocol-lab/scripts/lab/` can be called directly from the `quic-dotnet` scripts, or copied into `quic-dotnet` if that repo needs to work without a sibling ProtocolLab checkout.

## Package Script Responsibilities

`eng/protocol-lab/New-QuicDotNetProtocolLabPackage.ps1` should:

1. Accept `-Configuration`, `-ProtocolLabRoot`, `-OutputPath`, and optional `-PackageVersion`.
2. Run `dotnet publish` for the ProtocolLab adapter project.
3. Stage files under `artifacts/protocol-lab/package-source/{packageVersion}/`.
4. Copy the package manifest and implementation manifest templates.
5. Copy launch scripts.
6. Copy published binaries to `bin/{rid}/`.
7. Call `protocol-lab/scripts/lab/New-ProtocolLabPackage.ps1`.
8. Return JSON containing `packageId`, `packageVersion`, `sha256`, and package path.

Suggested version format for dirty development builds:

```powershell
$timestamp = Get-Date -AsUTC -Format "yyyyMMddTHHmmssZ"
$shortSha = git rev-parse --short HEAD
$dirty = if ((git status --porcelain).Length -gt 0) { "dirty" } else { "clean" }
$packageVersion = "dev-$timestamp-$shortSha-$dirty"
```

## Run Script Responsibilities

`eng/protocol-lab/Invoke-QuicDotNetProtocolLabRun.ps1` should:

1. Call `New-QuicDotNetProtocolLabPackage.ps1`.
2. Call `protocol-lab/scripts/lab/Submit-ProtocolLabPackageRun.ps1`.
3. Pass `-ImplementationId quic-dotnet-dev`.
4. Default to `-SuiteId h3-local-v1`, `-ScenarioId http.core.plaintext`, `-Protocol h3`, and `-LoadProfileId smoke`.
5. Write the final job JSON under `artifacts/protocol-lab/results/{jobId}.json`.
6. Download artifact archives under `artifacts/protocol-lab/results/{jobId}.zip` when the controller exposes an artifact archive URL.

## Template package manifest

`eng/protocol-lab/templates/protocol-lab-package.json`:

```json
{
  "schemaVersion": "protocol-lab-package-v1",
  "packageId": "quic-dotnet-dev",
  "packageVersion": "__PACKAGE_VERSION__",
  "kind": "implementation",
  "displayName": "QUIC.NET development build",
  "entryManifests": [
    "implementations/quic-dotnet-dev.yaml"
  ],
  "environments": [
    {
      "os": "linux",
      "arch": "x64",
      "entrypoint": {
        "kind": "bash",
        "path": "scripts/run-linux.sh",
        "arguments": [],
        "workingDirectory": "."
      }
    },
    {
      "os": "windows",
      "arch": "x64",
      "entrypoint": {
        "kind": "pwsh",
        "path": "scripts/run-windows.ps1",
        "arguments": [],
        "workingDirectory": "."
      }
    }
  ],
  "dependencies": {
    "requiresDotNet": false,
    "requiresDocker": false,
    "requiresPwsh": true,
    "requiresBash": true
  }
}
```

## Template implementation manifest

`eng/protocol-lab/templates/implementations/quic-dotnet-dev.yaml`:

```yaml
id: quic-dotnet-dev
name: QUIC.NET Development Adapter
description: Package-carried QUIC.NET ProtocolLab adapter built from the developer working tree.
image: ""
targetKind: process
targetContract: adapter-v1
executable: pwsh
project: ""
workingDirectory: .
dockerfile: ""
buildContext: ""
containerName: ""
dockerNetwork: ""
dockerNetworkMode: published-port
dockerBaseUrl: http://127.0.0.1:53381
baseUrl: http://127.0.0.1:53381
adapterControlPlaneBaseUrl: http://127.0.0.1:53381
certificateMode: none
roles:
  - server
supportedProtocols:
  - quic
  - h3
supportedWorkloadFamilies:
  - quic.transport
  - http.application
ports:
  - name: control-plane
    containerPort: 53381
    hostPort: 53381
    protocol: tcp
environment:
  ASPNETCORE_URLS: http://127.0.0.1:53381
dockerEnvironment: {}
commandArguments:
  - -NoLogo
  - -NoProfile
  - -File
  - scripts/run-current-platform.ps1
readinessCheck:
  type: http
  url: /protocol-lab/adapter/v1/health
  timeoutSeconds: 10
shutdownBehavior: process-exit
capabilities:
  - adapter-control-plane
  - quic.server
  - http3.server
artifactExports:
  - server.stdout.txt
  - server.stderr.txt
qlogSupport: false
sslKeyLogSupport: false
notes: Trusted lab package execution only.
```

Adjust `supportedProtocols`, `supportedWorkloadFamilies`, and capabilities to match the adapter that `quic-dotnet` actually exposes.

## Template launch scripts

`eng/protocol-lab/templates/scripts/run-current-platform.ps1`:

```powershell
$ErrorActionPreference = "Stop"

if ($IsWindows) {
    & "$PSScriptRoot/run-windows.ps1" @args
    exit $LASTEXITCODE
}

if ($IsLinux) {
    & bash "$PSScriptRoot/run-linux.sh" @args
    exit $LASTEXITCODE
}

throw "Unsupported OS for quic-dotnet ProtocolLab package."
```

`eng/protocol-lab/templates/scripts/run-linux.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ADAPTER="$PACKAGE_ROOT/bin/linux-x64/QuicDotNet.ProtocolLabAdapter"
chmod +x "$ADAPTER" 2>/dev/null || true
exec "$ADAPTER" "$@"
```

`eng/protocol-lab/templates/scripts/run-windows.ps1`:

```powershell
$ErrorActionPreference = "Stop"
$PackageRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PackageRoot "bin/win-x64/QuicDotNet.ProtocolLabAdapter.exe") @args
exit $LASTEXITCODE
```

Rename the executable paths to the actual adapter binary name in `quic-dotnet`.

## Example Pack Command

From `quic-dotnet`:

```powershell
pwsh ./eng/protocol-lab/New-QuicDotNetProtocolLabPackage.ps1 `
  -ProtocolLabRoot C:/shared/src/incursa/protocol-lab `
  -Configuration Release `
  -OutputPath ./artifacts/protocol-lab/packages/quic-dotnet-dev.plabpkg
```

## Example Submit Command

From `quic-dotnet`:

```powershell
pwsh ./eng/protocol-lab/Invoke-QuicDotNetProtocolLabRun.ps1 `
  -ProtocolLabRoot C:/shared/src/incursa/protocol-lab `
  -ControllerUri http://lab-controller:5080 `
  -ScenarioId http.core.plaintext `
  -LoadProfileId smoke
```

Or call the shared ProtocolLab script directly after creating a package:

```powershell
pwsh C:/shared/src/incursa/protocol-lab/scripts/lab/Submit-ProtocolLabPackageRun.ps1 `
  -ControllerUri http://lab-controller:5080 `
  -PackagePath ./artifacts/protocol-lab/packages/quic-dotnet-dev.plabpkg `
  -ImplementationId quic-dotnet-dev `
  -SuiteId h3-local-v1 `
  -ScenarioId http.core.plaintext `
  -Protocol h3 `
  -LoadProfileId smoke `
  -ArtifactOutputPath ./artifacts/protocol-lab/results/latest.zip
```

## Controller Contract Expected by the Scripts

The shared submit script expects:

- `POST /api/lab/packages` accepts multipart form field `file`.
- Package upload returns `packageId`, `packageVersion`, and `sha256`.
- `POST /api/lab/jobs` accepts a JSON job request with `packages`.
- Job submission returns `jobId` or `id`.
- `GET /api/lab/jobs/{jobId}` returns `status` or `state`.
- Terminal job statuses are `Completed`, `Failed`, `Canceled`, `Cancelled`, or `TimedOut`.
- Artifact archive URL is returned as one of:
  - `artifactArchiveUrl`
  - `artifactsDownloadUrl`
  - `downloadUrl`
  - `artifacts.archiveUrl`
  - `artifacts.downloadUrl`

## Codex Prompt for quic-dotnet

Use this prompt inside the `quic-dotnet` repo:

```text
Add ProtocolLab Lab Package support to this repo.

Implement eng/protocol-lab/New-QuicDotNetProtocolLabPackage.ps1 and eng/protocol-lab/Invoke-QuicDotNetProtocolLabRun.ps1.

Use ProtocolLab Lab Package v1:
- package extension .plabpkg
- root manifest protocol-lab-package.json
- schemaVersion protocol-lab-package-v1
- packageId quic-dotnet-dev
- kind implementation
- entryManifests includes implementations/quic-dotnet-dev.yaml
- include linux-x64 and win-x64 environments if the repo can publish both; otherwise implement the current host RID first
- package published adapter binaries, not source
- call <protocol-lab-root>/scripts/lab/New-ProtocolLabPackage.ps1 to create the package
- call <protocol-lab-root>/scripts/lab/Submit-ProtocolLabPackageRun.ps1 to upload, submit, poll, and download results

The implementation manifest should be process-backed adapter-v1 and should launch scripts/run-current-platform.ps1 with pwsh. The scripts should dispatch to bin/linux-x64 or bin/win-x64. Use the actual adapter project and binary names from this repo.

Default the run script to:
- ImplementationId quic-dotnet-dev
- SuiteId h3-local-v1
- ScenarioId http.core.plaintext
- Protocol h3
- LoadProfileId smoke

Keep all generated package sources, packages, and results under artifacts/protocol-lab/.
```
