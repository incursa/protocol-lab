# Repository Layout

This repository keeps runner orchestration, CLI presentation, model types, test fixtures, and canonical SpecTrace artifacts in separate physical areas so ownership is obvious from the path.

## Runner Specs

- Requirement artifacts stay as direct children under `specs/requirements/protocol-lab/`.
- Runner architecture, work items, and verification artifacts live under:
  - `specs/architecture/protocol-lab/runner/`
  - `specs/work-items/protocol-lab/runner/`
  - `specs/verification/protocol-lab/runner/`
- Fixture-lab SpecTrace artifacts also live in those runner subfolders.

## Runner Code

Runner-owned code lives under `src/Incursa.ProtocolLab.Runner/` and is grouped by concern:

- `Abstractions/`
- `Compatibility/`
- `Diagnostics/`
- `Events/`
- `Lifecycle/`
- `LoadTools/`
- `Orchestration/`
- `Planning/`
- `Validation/`

The runner stays implementation-neutral. It must not absorb protocol implementations, command parsing, or console presentation.

## CLI Host

`src/Incursa.ProtocolLab.Cli/` owns command registration, argument parsing, console rendering, and process exit codes.

## Model

`src/Incursa.ProtocolLab.Model/` owns shared manifests, run cells, results, reports, and artifact layout types.

## Fixtures

The runner contract fixture lab lives under `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/`.

It contains fake manifests, fake scenarios, a fixture suite, fake load tools, and dummy scripts used by direct runner tests and CLI smoke tests.

## Future Contract Areas

If new work is added later, keep the path aligned with the owning contract area:

- adapter contracts
- execution backend contracts
- load tool contracts
- scenario contracts
- capability model
- artifact and result model

Adapter contract documentation lives under `docs/architecture/`, and adapter
schemas live under `schemas/adapter/v1/`.

## Validation

After moving files or artifacts, run:

```powershell
dotnet restore Incursa.ProtocolLab.sln
dotnet build Incursa.ProtocolLab.sln --no-restore
dotnet test Incursa.ProtocolLab.sln --no-build
workbench validate --profile core
```
