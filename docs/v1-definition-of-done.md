# ProtocolLab Public Contract Definition of Done

ProtocolLab public readiness is contract-first. The public repository is
done for a slice when it defines the shared contracts, schemas, package
tooling, conformance fixtures, and documentation that producers and the
internal lab can consume without relying on public-repo production adapters.

## Required

- `dotnet build Incursa.ProtocolLab.sln` succeeds.
- `dotnet test Incursa.ProtocolLab.sln` passes.
- `dotnet tool restore` works from a clean checkout.
- `dotnet run --project src\Incursa.ProtocolLab.Cli -- check` reports
  actionable local prerequisite state.
- Adapter Contract v1 schemas and conformance tests pass.
- Test Executor Contract v1 schemas and conformance tests pass.
- package v2 schemas reject legacy `load-runner` and `providedLoadTools`
  semantics.
- package v2 tooling accepts `implementation`, `test-executor`,
  `scenario-pack`, and `toolchain` packages.
- Runtime component package entry manifests are package-relative and
  kind-scoped under `implementations/`, `test-executors/`, `scenarios/`, or
  `suites/`.
- Raw QUIC package fixtures enable only
  `quic.transport.multiplex.100x64kb` and
  `quic.transport.duplex-streams`.
- Public architecture guardrails prevent runner/CLI dependencies on concrete
  adapter implementation assemblies or concrete protocol implementation
  libraries.
- Public docs describe adapters, test executors, packages, scenarios, tests,
  capabilities, metrics, artifacts, unsupported outcomes, and provenance as
  neutral contracts.
- Missing optional tools produce honest unsupported, unavailable, skipped, or
  warning output instead of fabricated metrics.

## Deferred

- Hosted controller and worker scheduling.
- Package registry lookup and automatic dependency fetching.
- Untrusted package sandboxing.
- Production adapter implementations.
- Production test-executor implementations.
- Publishable isolated-host benchmark automation.
- Raw QUIC scenarios beyond the two enabled package-backed cells.
- WebTransport, MASQUE, and extended protocol validators.

## Verification Commands

Build and test:

```powershell
dotnet restore Incursa.ProtocolLab.sln
dotnet build Incursa.ProtocolLab.sln --no-restore
dotnet test Incursa.ProtocolLab.sln --no-build
```

Focused public contract proof:

```powershell
dotnet test tests\Incursa.ProtocolLab.Tests\Incursa.ProtocolLab.Tests.csproj --filter "FullyQualifiedName~ArchitectureGuardrailTests|FullyQualifiedName~PublicContractSchemaTests|FullyQualifiedName~LabPackageScriptTests|FullyQualifiedName~AdapterConformanceSuiteTests|FullyQualifiedName~AdapterSchemaValidatorTests|FullyQualifiedName~TestExecutorConformanceSuiteTests|FullyQualifiedName~TestExecutorSchemaValidatorTests"
```

Raw QUIC package fixture dry run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\lab\New-ProtocolLabRawQuicComponentPackages.ps1 `
  -PackageVersion local-contract-proof `
  -SourceBackedTestExecutor `
  -OutputRoot .artifacts\lab-packages\raw-quic-components-local-contract-proof `
  -Force
```

Workbench repository-shape validation, when available:

```powershell
workbench validate --profile core
```
