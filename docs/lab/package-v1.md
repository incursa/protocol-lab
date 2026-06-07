# ProtocolLab Lab Package v1

ProtocolLab Lab Package v1 is a trusted internal archive format for moving a prebuilt implementation, its ProtocolLab implementation manifest, and any launch scripts into a lab controller job.

V1 packages are for regression lab and development evidence. They are not a sandbox, and they do not make a run publishable by themselves.

## File Format

- Extension: `.plabpkg`
- Archive format: ZIP-compatible archive
- Required root file: `protocol-lab-package.json`
- Required package kind for V1: `implementation`
- Required identity: `packageId`, `packageVersion`, and controller-computed SHA-256

The root archive must not contain entries that are absolute paths, use `..`, or extract outside the package workspace.

## Manifest

Minimum manifest:

```json
{
  "schemaVersion": "protocol-lab-package-v1",
  "packageId": "quic-dotnet-dev",
  "packageVersion": "dev-2026-06-06T2130Z",
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

### Required Fields

- `schemaVersion`: must be `protocol-lab-package-v1`
- `packageId`: stable package family id, such as `quic-dotnet-dev`
- `packageVersion`: exact package version, usually a timestamp or repo version plus git SHA
- `kind`: `implementation`
- `entryManifests`: package-relative paths to ProtocolLab implementation manifests
- `environments`: supported runtime environments
- `dependencies`: dependency declaration used by workers during readiness checks

### Environment Fields

- `os`: `linux`, `windows`, or `macos`
- `arch`: `x64` or `arm64`
- `entrypoint.kind`: `process`, `bash`, or `pwsh`
- `entrypoint.path`: package-relative executable or script path
- `entrypoint.arguments`: optional argument array
- `entrypoint.workingDirectory`: package-relative working directory

The environment entrypoint tells the worker how the package can be launched. The ProtocolLab runner still consumes the package-provided implementation manifest through a generated catalog overlay.

## Package Layout

Recommended layout for `quic-dotnet`:

```text
protocol-lab-package.json
implementations/
  quic-dotnet-dev.yaml
scripts/
  run-linux.sh
  run-windows.ps1
bin/
  linux-x64/
    QuicDotNet.ProtocolLabAdapter
    *.dll
  win-x64/
    QuicDotNet.ProtocolLabAdapter.exe
    *.dll
```

The implementation manifest should use package-relative paths while staged in the package source directory. Before the worker invokes the runner, it resolves those paths to the extracted package workspace or writes an overlay manifest with resolved absolute paths.

Example package implementation manifest:

```yaml
id: quic-dotnet-dev
name: QUIC.NET Development Adapter
description: Package-carried QUIC.NET ProtocolLab adapter.
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
notes: Package-carried development adapter. Trusted lab package execution only.
```

For V1, prefer script entrypoints in the implementation manifest. They give the package one stable manifest while each script selects the right binary for the current worker OS and architecture.

## Controller API

The controller package API should expose:

- `POST /api/lab/packages`
- `GET /api/lab/packages`
- `GET /api/lab/packages/{packageId}/versions/{version}`
- `GET /api/lab/packages/{packageId}/versions/{version}/download`

Upload response:

```json
{
  "packageId": "quic-dotnet-dev",
  "packageVersion": "dev-2026-06-06T2130Z",
  "sha256": "012345...",
  "manifest": {
    "displayName": "QUIC.NET development build",
    "kind": "implementation",
    "entryManifests": [
      "implementations/quic-dotnet-dev.yaml"
    ]
  }
}
```

Jobs reference packages by exact identity:

```json
{
  "kind": "single-node-benchmark",
  "suiteId": "h3-local-v1",
  "implementationIds": [
    "quic-dotnet-dev"
  ],
  "scenarioIds": [
    "http.core.plaintext"
  ],
  "protocol": "h3",
  "loadProfileId": "smoke",
  "executionMode": "process",
  "packages": [
    {
      "packageId": "quic-dotnet-dev",
      "packageVersion": "dev-2026-06-06T2130Z",
      "sha256": "012345..."
    }
  ]
}
```

## Worker Materialization

Workers should materialize packages under:

```text
.artifacts/lab-workspaces/{jobId}/{attemptId}/packages/{packageId}/
```

The worker must:

1. Download every referenced package before claiming execution readiness.
2. Compute SHA-256 and reject mismatches as infrastructure failures.
3. Safely extract into the per-attempt package workspace.
4. Verify package manifest and entry manifest paths.
5. Generate a catalog overlay that includes package implementation manifests.
6. Run the existing ProtocolLab CLI.
7. Preserve normal run artifacts under `.artifacts/runs/{runId}`.

## quic-dotnet Development Loop

The intended local developer loop is:

1. Build and publish the adapter from the dirty working tree.
2. Stage a `.plabpkg` directory with the published binaries, launch scripts, package manifest, and implementation manifest.
3. Create the `.plabpkg` with `scripts/lab/New-ProtocolLabPackage.ps1`.
4. Upload the package to the controller.
5. Submit a job referencing that exact package SHA-256.
6. Poll the job until completion.
7. Download returned run artifacts or inspect the run root on the controller.

This keeps uncommitted code out of Git and Docker while still making the worker execute exactly the bytes attached to the job.
