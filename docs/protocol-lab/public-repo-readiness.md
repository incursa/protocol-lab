# ProtocolLab Public Repository Readiness

Status: Contract-first readiness gate

## Summary

The public repository is ready when it can be consumed as the canonical
contract, schema, package-tooling, fixture, and documentation source for
ProtocolLab. Public readiness no longer depends on repo-owned production
Kestrel, Incursa, MSQUIC, or quic-go adapter projects.

## Required Proof

- Public/private boundary scan has no private checkout markers, private path
  markers, secret/token/password patterns, or forbidden file globs.
- `dotnet restore`, `dotnet build`, and `dotnet test` pass from a clean
  checkout.
- `workbench validate --profile core` passes when Workbench is available.
- Adapter Contract v1 schema and conformance tests pass.
- Test Executor Contract v1 schema and conformance tests pass.
- package v2 schema tests pass, including rejection of legacy `load-runner`
  and `providedLoadTools` semantics.
- Raw QUIC component package dry run emits explicit `test-executor` and
  `scenario-pack` packages with package-relative entry manifests.
- Architecture guardrails pass, including checks that the public runner/CLI
  do not reference concrete adapter assemblies or concrete protocol
  implementation libraries.

## Commands

```powershell
git status --short --branch
git remote -v
# private marker scan
# forbidden-file scan
dotnet tool restore
dotnet restore Incursa.ProtocolLab.sln
dotnet build Incursa.ProtocolLab.sln -v minimal
dotnet test Incursa.ProtocolLab.sln --no-build -v minimal
workbench validate --profile core
```

Focused contract proof:

```powershell
dotnet test tests\Incursa.ProtocolLab.Tests\Incursa.ProtocolLab.Tests.csproj --filter "FullyQualifiedName~ArchitectureGuardrailTests|FullyQualifiedName~PublicContractSchemaTests|FullyQualifiedName~LabPackageScriptTests|FullyQualifiedName~AdapterConformanceSuiteTests|FullyQualifiedName~AdapterSchemaValidatorTests|FullyQualifiedName~TestExecutorConformanceSuiteTests|FullyQualifiedName~TestExecutorSchemaValidatorTests"
```

Raw QUIC package dry run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\lab\New-ProtocolLabRawQuicComponentPackages.ps1 `
  -PackageVersion public-readiness `
  -SourceBackedTestExecutor `
  -OutputRoot .artifacts\lab-packages\raw-quic-components-public-readiness `
  -Force
```

## Non-Goals

- Hosted execution proof belongs to `protocol-lab-internal`.
- Production implementation adapters belong to producer repositories.
- Production test executors belong to producer repositories.
- Package registry lookup and automatic dependency fetching are future work.
