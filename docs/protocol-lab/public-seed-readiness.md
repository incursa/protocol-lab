# ProtocolLab Public Seed Readiness

**Status:** Ready for clean repo creation

This report validates the current public staging export as a candidate for the
future public repository.

## Summary

- The staged public seed restores, builds, and tests successfully.
- The Incursa HTTP/3 adapter is now repo-owned and no longer depends on an
  external sample-path shape.
- The export contains no unresolved warning-classified files.
- The export contains no accepted warnings.
- `workbench validate --profile core` passes in the source repository; the
  staging copy is not a git repository.

## Export Snapshot

- Scanned files: `43064`
- Included files: `357`
- Skipped files: `42707`
- Unresolved warnings: `0`
- Accepted warnings: `0`

## Commands Run

### Export and inspection

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Export-ProtocolLabPublicDryRun.ps1 -WhatIf
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Export-ProtocolLabPublicDryRun.ps1
dotnet sln Incursa.ProtocolLab.sln list
workbench validate --profile core
```

### Restore and build

```powershell
dotnet restore Incursa.ProtocolLab.sln
dotnet build Incursa.ProtocolLab.sln --no-restore
```

### Test execution

```powershell
dotnet test Incursa.ProtocolLab.sln --no-restore
dotnet test tests\Incursa.ProtocolLab.Tests\Incursa.ProtocolLab.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchitectureGuardrailTests"
```

### Repo validation

```powershell
workbench validate --profile core
```

## Results

### Restore

- Result: pass
- Notes: all solution projects restored from the staging tree successfully.

### Build

- Result: pass
- Errors: none
- Warnings: `40`
- Notes:
  - The build completed cleanly.
  - Most warnings were `CA1416` platform-compatibility warnings in the QUIC
    server implementations.
  - One test-project nullable warning was reported during build:
    `CS8625` in `tests/Incursa.ProtocolLab.Tests/LoadProfileContractV1Tests.cs`.

### Public-safe tests

- Result: pass
- Passed: `255`
- Failed: `0`
- Skipped: `0`
- Total: `255`
- Notes:
  - The Incursa HTTP/3 adapter conformance suite now observes both the control
    plane and the protocol endpoint.
  - `tests/Incursa.ProtocolLab.Tests/IncursaHttp3AdapterConformanceTests.cs`
    passes against the repo-owned adapter project.

### Targeted guardrail rerun

- Result: pass
- Passed: `9`
- Failed: `0`
- Notes:
  - `tests/Incursa.ProtocolLab.Tests/ArchitectureGuardrailTests.cs` was updated
    in the private source repo so the runner spec guardrail now checks for the
    public repo shape instead of requiring internal work-item files.

### Workbench validation

- Result: pass in source / unavailable in staging
- Notes:
  - The staging directory is a copied tree, not a git checkout.

## Private Path Leak Review

Search terms reviewed in the staged public content:

- source and staging root metadata
- private workspace markers
- sample-brand naming markers
- private-path markers
- secrets, tokens, passwords, certificates, private URLs, customer names

Results:

- No included file in the staged seed contains the reviewed private-path,
  sample-brand, or private-workspace markers.
- No included file contains suspicious secret literals.
- The only absolute source/staging paths appear in generated export metadata,
  which is review-only staging output and not part of the public seed.

## Excluded Paths

The export intentionally excludes these directories and file classes from the
public seed:

- `.git`
- `.artifacts`
- `bin`
- `obj`
- generated benchmark artifacts
- internal analysis outputs
- `AGENTS.md`
- `PLANS.md`
- `docs/analysis/`
- `docs/initial_prompt.md`
- `docs/protocol-lab/public-export-plan.md`
- `docs/protocol-lab/public-private-inventory.md`
- `docs/protocol-lab/public-private-inventory.json`
- `docs/protocol-lab/repository-split-plan.md`
- `scripts/analysis/Summarize-H3Phase2I.ps1`
- `specs/work-items/protocol-lab/`

Validation note:

- The export itself did not include `.git`, `.artifacts`, `bin`, or `obj`.
- The local restore/build/test commands created project-local `bin`/`obj`
  outputs in the staging tree as validation side effects, and those outputs were
  removed before the final leak scan.

## Files That Still Need Cleanup

- None blocking public seed creation.
- The broader planning docs remain classified `public-after-cleanup` for
  wording alignment, but they do not block publication of the public seed.

## Readiness Classification

`ready for clean repo creation`

The staged public seed is buildable, tested, and no longer depends on an
external sample-path shape. The remaining cleanup is non-blocking wording
alignment in broader planning docs outside the Incursa HTTP/3 adapter surface.

