# quic-dotnet Package Handoff Boundary

This document records the public ProtocolLab expectations for package-backed
`quic-dotnet` lab runs. The public `protocol-lab` repository no longer carries
quic-dotnet production adapter templates. quic-dotnet owns its package authoring
scripts, package source layout, adapter binaries, and launch scripts locally.

ProtocolLab provides only the neutral pieces:

- Adapter Contract v1 under `/protocol-lab/adapter/v1`
- Test Executor Contract v1 under `/protocol-lab/test-executor/v1`
- Component Package v2 schema and builder tooling
- conformance suites for adapter and test executor HTTP control planes
- neutral package submission helpers

## Package Targets

quic-dotnet should produce separate package families for distinct execution
lanes:

| Package ID | Kind | Purpose |
| --- | --- | --- |
| `quic-dotnet-dev` | `implementation` | HTTP/3 implementation package exposing Adapter Contract v1. |
| `quic-dotnet-raw-dev` | `implementation` | Raw QUIC implementation package exposing Adapter Contract v1. |

Raw QUIC support remains intentionally narrow. Until additional validator and
artifact gates exist, `quic-dotnet-raw-dev` should only declare:

- `quic.transport.multiplex.100x64kb`
- `quic.transport.duplex-streams`

## Package v2 Requirements

Each package root must contain `protocol-lab-package.json` using:

```json
{
  "schemaVersion": "protocol-lab-package-v2",
  "kind": "implementation"
}
```

The complete manifest must satisfy
`schemas/package/v2/package.schema.json`. For implementation packages this
means:

- `entryManifests` contains at least one package-relative implementation
  manifest path.
- `providedImplementations` declares each implementation id, supported
  protocols, and supported scenarios.
- every environment entrypoint path and working directory is package-relative.
- dependencies explicitly declare required runtime/tool availability.

Package-relative paths must not be absolute, drive-qualified, or use `..`
traversal segments.

## Conformance Expectations

Before a package is submitted to a controller:

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~AdapterConformanceSuiteTests
```

When quic-dotnet also produces a package-carried test executor, verify it with:

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~TestExecutorConformanceSuiteTests
```

The controller-side package proof should record:

- package id, version, kind, and SHA-256
- selected implementation id
- selected scenario/test/suite ids
- selected test-executor package and id
- selected protocol
- materialized package root and entrypoint
- job result artifacts that identify all selected packages

## Submission Shape

After quic-dotnet builds its `.plabpkg`, submit it with the neutral ProtocolLab
helper and explicit package references:

```powershell
pwsh C:/shared/src/incursa/protocol-lab/scripts/lab/Submit-ProtocolLabPackageRun.ps1 `
  -ControllerUri http://lab-controller:5080 `
  -PackagePath ./artifacts/protocol-lab/packages/quic-dotnet-dev.plabpkg `
  -PackageReference <test-executor-package-id:version:sha256> `
  -PackageReference <scenario-pack-id:version:sha256> `
  -ImplementationId quic-dotnet-dev `
  -TestExecutorId managed-httpclient-h3-load `
  -SuiteId h3-local-v1 `
  -ScenarioId http.core.plaintext `
  -Protocol h3 `
  -LoadProfileId smoke `
  -ArtifactOutputPath ./artifacts/protocol-lab/results/latest.zip
```

Raw QUIC submissions should select `quic-dotnet-raw-dev`, `Protocol quic`, and
one of the two currently enabled raw QUIC scenarios listed above. A raw QUIC
job must never fall back to the managed HTTP/3 test executor.

## Controller Contract Expected by the Helper

The shared submit script expects:

- `POST /api/lab/packages` accepts multipart form field `file`.
- Package upload returns `packageId`, `packageVersion`, and `sha256`.
- `POST /api/lab/jobs` accepts a JSON job request with `packages`.
- Job submission returns `jobId` or `id`.
- `GET /api/lab/jobs/{jobId}` returns `status` or `state`.
- Terminal job statuses are `Completed`, `Failed`, `Canceled`, `Cancelled`,
  or `TimedOut`.
- Artifact archive URL is returned as one of `artifactArchiveUrl`,
  `artifactsDownloadUrl`, `downloadUrl`, `artifacts.archiveUrl`, or
  `artifacts.downloadUrl`.

## Prompt for quic-dotnet

Use this prompt inside the `quic-dotnet` repo:

```text
Add ProtocolLab Component Package v2 support owned by this repo.

Create local package authoring scripts under eng/protocol-lab/ for:
- quic-dotnet-dev: HTTP/3 implementation package
- quic-dotnet-raw-dev: raw QUIC implementation package

Use schemaVersion protocol-lab-package-v2 and kind implementation. Declare
providedImplementations with protocols and scenarios. Keep raw QUIC limited to
quic.transport.multiplex.100x64kb and quic.transport.duplex-streams.

Package published adapter binaries and package-local launch scripts. Do not
depend on public protocol-lab concrete adapter projects. Call the public
protocol-lab scripts only for neutral package build/submission helpers and
validate package manifests against schemas/package/v2/package.schema.json.

Before submission, prove Adapter Contract v1 conformance for each package
lane. For any package-carried test executor, also prove Test Executor Contract
v1 conformance.
```
