# Quickstart

This quickstart gets a clean public ProtocolLab checkout to build, contract
validation, and package tooling proof. It does not require the sibling
internal repository and it does not rely on public-repo production adapters.

## 1. Clone

Clone `incursa/protocol-lab` and open a PowerShell prompt at the repository
root.

## 2. Restore Tools

```powershell
dotnet tool restore
```

This restores repo-local tools used by validation and diagnostics.

## 3. Build And Test

```powershell
dotnet restore Incursa.ProtocolLab.sln
dotnet build Incursa.ProtocolLab.sln --no-restore
dotnet test Incursa.ProtocolLab.sln --no-build
```

If you are using the Codex/Workbench environment, also run:

```powershell
workbench validate --profile core
```

## 4. Inspect Local Prerequisites

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- check
```

`check` reports local tool, Docker, manifest, and runner prerequisite state.
It is a local diagnostic command, not hosted-lab readiness.

## 5. Validate Public Contracts

Run the public conformance command against the neutral package fixtures:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\neutral-test-executor
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\neutral-adapter-implementation
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\neutral-scenario-pack
```

Validate your own package root directory or `.plabpkg` archive the same way:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package <package-root-or-plabpkg>
```

Then start your adapter or test-executor control plane locally and run the
behavioral conformance probe against its HTTP base URL:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance adapter --base-url <adapter-control-plane-url> --scenario-id <supported-scenario-id> --scenario-version 1.0 --protocol <protocol-id>
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance test-executor --base-url <test-executor-control-plane-url> --test-id <supported-test-id> --scenario-id <supported-scenario-id> --scenario-version 1.0 --protocol <protocol-id>
```

Run the focused public contract tests:

```powershell
dotnet test tests\Incursa.ProtocolLab.Tests\Incursa.ProtocolLab.Tests.csproj --filter "FullyQualifiedName~AdapterConformanceSuiteTests|FullyQualifiedName~AdapterSchemaValidatorTests|FullyQualifiedName~TestExecutorConformanceSuiteTests|FullyQualifiedName~TestExecutorSchemaValidatorTests|FullyQualifiedName~PublicContractSchemaTests"
```

The public repo defines:

- Adapter Contract v1 under `/protocol-lab/adapter/v1`
- Test Executor Contract v1 under `/protocol-lab/test-executor/v1`
- package v2 for `implementation`, `test-executor`, `scenario-pack`, and
  `toolchain` packages

Package authors should run the reusable conformance suites against their own
adapter or test-executor control plane before submitting packages to a lab.

## 6. Build A Package Fixture

The public repo includes neutral package tooling and fixture/reference
package builders. For the currently narrow raw QUIC package fixture:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\lab\New-ProtocolLabRawQuicComponentPackages.ps1 `
  -PackageVersion local-dev `
  -SourceBackedTestExecutor `
  -OutputRoot .artifacts\lab-packages\raw-quic-components-local-dev `
  -Force
```

The test-executor package advertises `test-executors/quic-go-raw-load.yaml`.
The scenario package advertises only:

- `quic.transport.multiplex.100x64kb`
- `quic.transport.duplex-streams`

Raw QUIC package-backed validation remains intentionally narrow until more
validator and artifact gates exist.

## 7. Submit Package References

Hosted execution is implemented by the internal/private lab, but the public
submission shape is contract-first and package-backed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\lab\Submit-ProtocolLabPackageRun.ps1 `
  -ControllerUri <controller-uri> `
  -PackagePath <implementation-package.plabpkg> `
  -PackageReference <test-executor-package-id:version:sha256> `
  -PackageReference <scenario-package-id:version:sha256> `
  -ImplementationId <implementation-id> `
  -TestExecutorId <test-executor-id> `
  -SuiteId <suite-id> `
  -Protocol <protocol-id>
```

Dependency closure is explicit for now. Package registry lookup and automatic
dependency fetching are future work.

## 8. Local Runner Workflows

The local runner catalog still supports public fixture and local benchmark
workflows, but production implementation adapters and production test
executors should be authored outside this public contract repository.

Use these docs for deeper workflow details:

- [Lab Roles](architecture/lab-roles.md)
- [Adapter Contract v1](architecture/adapter-contract-v1.md)
- [Test Executor Contract v1](architecture/test-executor-contract-v1.md)
- [Package v2](lab/package-v2.md)
- [Local Benchmark Workflow](benchmarking/local-benchmark-workflow.md)

## Troubleshooting

Docker unavailable: start Docker Desktop and rerun `check`. Use Docker paths
only for workflows that actually need them.

Package entry manifest rejected: v2 entry manifests must be package-relative
and kind-scoped: `implementations/`, `test-executors/`, `scenarios/`, or
`suites/`.

Raw QUIC package fixture rejected: ensure the requested scenario is one of
the two enabled raw QUIC scenarios listed above. Other raw QUIC scenarios must
remain unsupported until their validators and artifact gates exist.
