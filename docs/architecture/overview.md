# Architecture — Overview

**Status:** Implemented (v1 core loop complete; execution profile, effective load shape, collision-proof cell identity, and report claim levels are implemented; some provenance and hosted concerns remain deferred)

## Scope

This document describes the architectural components of ProtocolLab and how
they relate to each other. Detailed models for each component are documented
separately:

- [Runner Model](runner-model.md)
- [Scenario Model](scenario-model.md)
- [Test Case And Run Plan Model](test-case-run-plan-model.md)
- [Adapter Model](adapter-model.md)
- [Load Model](load-model.md)
- [Artifact Model](artifact-model.md)
- [Measurement Model](measurement-model.md)
- [Report Model](report-model.md)

The canonical requirements trace is maintained in
[`docs/spec/requirements-trace.md`](../spec/requirements-trace.md). The
existing [`docs/architecture.md`](../architecture.md) remains as the
foundational architecture document covering scope, components, and
boundaries; the documents in this directory provide deeper technical detail
for each sub-model.

## Component Map

```
CLI Host (Incursa.ProtocolLab.Cli)
  |
  v
Runner Engine (Incursa.ProtocolLab.Runner)
  |-- Orchestration (RunnerEngine, RunPlanBuilder, ReportPublicationWorkflow)
  |-- Compatibility (CompatibilityClassifier)
  |-- Lifecycle (TargetOrchestrator, AdapterSessionOrchestrator)
  |-- Validation (HttpScenarioValidator, ProtocolProofValidator)
  |-- Load Tools (LoadToolInvoker, ManagedHttp3LoadGenerator)
  |-- Diagnostics (RuntimeCounterCapture, DockerContainerMetricsCapture)
  |-- Events (RunnerEvent, IRunnerEventSink)
  |
  v
Model (Incursa.ProtocolLab.Model)
  |-- Scenarios, Manifests, Load Tools, Load Profiles
  |-- Matrix Expansion, Run Cells, Execution Profiles
  |-- Requested/Effective Load Shape, Load Shape Semantics
  |-- Results, Reports, Aggregates, Claim Levels
  |-- Artifact Layout, Artifact Paths, RunCellIdentity
  |-- Network Profiles, Suites, Canonical Protocol IDs
```

## Data Flow

```
Implementations (YAML) ----+
Scenarios (YAML) ----------+--> Matrix Expansion --> Run Cells
Load Tools (YAML) ---------+                            |
Load Profiles (YAML) ------+                            v
Suites (YAML) -------------+              Target Lifecycle (start/ready)
                                                        |
                                                        v
                                                  Validation
                                                  (protocol proof + endpoint checks)
                                                        |
                                              +---------+---------+
                                              |                   |
                                          Passed              Failed/
                                                              Unsupported
                                              |                   |
                                              v                   v
                                         Benchmark           Explicit
                                         (load tool)         Outcome
                                              |
                                              v
                                         Artifacts
                                         (stdout, stderr,
                                          metrics, metadata)
                                              |
                                              v
                                         Reports
                                         (summary.md,
                                          aggregate-results.json)
```

## Key Architectural Decisions

1. **Implementation-neutral runner.** The runner (`Incursa.ProtocolLab.Runner`)
   does not reference Incursa protocol assemblies. All targets enter through
   manifests, commands, containers, ports, and artifact contracts.

2. **Validation-before-benchmark gate.** Benchmark data is accepted only when
   validation status is `passed`. Unsupported, failed, or not-run validation
   results produce explicit outcomes with reasons.

3. **Canonical protocol identifiers.** The model normalizes common aliases to
   short, stable ids (`h1`, `h2`, `h3`, `quic`, `webtransport`, `masque`) so
   reports, artifacts, and compatibility checks use one vocabulary.

4. **Requested and effective load shape are separate.** The runner records the
   requested shape, the effective shape, and the semantics that explain any
   ignored, derived, or unsupported fields.

5. **Collision-proof artifact layout.** Artifacts follow a deterministic run
   cell identity built from implementation, scenario, protocol, execution
   profile, network profile, load profile, connections, streams, and
   repetition.

6. **Evidence classification and claim levels.** Every result is classified
   with an evidence class (`local-smoke` through `publishable`) and a
   comparability status, and every report carries a claim level. Results from
   different evidence classes or incompatible execution environments are not
   directly ranked together.

7. **Adapter control plane separation.** The adapter control plane (HTTP/1.1
   JSON at `/protocol-lab/adapter/v1`) is separate from the protocol endpoint
   under test. The control plane manages lifecycle; the endpoint carries
   protocol traffic.

8. **Best-effort parsing.** Load-tool output is parsed best-effort. If parsing
   fails, the raw artifacts are preserved and `parsedMetricsAvailable` is set
   to `false`. No metrics are fabricated.

9. **Separation of concerns.** The CLI owns command parsing and console
   rendering. The runner owns orchestration and execution logic. The model
   owns shared types. Adapter contracts own the control-plane surface. Server
   implementations own protocol behavior.

10. **Spec-first package composition.** New protocol lanes start with a
    test-case/scenario specification, then a test executor, then a reference
    implementation package, and finally a repeatable run plan that pins exact
    package identities and selected IDs.

## Project Boundaries

| Concern | Owner | Must Not |
|---------|-------|----------|
| Orchestration, validation flow, load-tool invocation, artifact capture, reporting | Runner | Contain protocol-specific code paths |
| Command parsing, console rendering, exit codes | CLI | Contain orchestration logic |
| Shared types (manifests, scenarios, results, artifacts) | Model | Contain execution logic |
| Adapter control-plane contract types and client | Adapter.Contracts | Contain protocol endpoint logic |
| Protocol stacks, server behavior, custom metrics | Implementations | Couple to runner assemblies |
| Request generation, raw metric output | Load Tools | Couple to target implementation details |

## Current Implementation Coverage

| Component | Status | Notes |
|-----------|--------|-------|
| Runner engine | Implemented | `validate`, `run`, `list`, `check`, `report`, `publish-report` commands |
| Scenario model | Implemented | HTTP application scenarios plus modeled H3/QUIC/WebTransport/MASQUE families |
| Load profiles | Implemented | `smoke`, `local-comparison`, `local-regression` presets with explicit purpose/evidence metadata |
| Execution profile | Implemented | `ExecutionProfile` recorded per run/cell and normalized from CLI/host state |
| Load shape semantics | Implemented | Requested vs effective shape plus warnings for ignored/derived/unsupported fields |
| Matrix expansion | Implemented | Cartesian product of impl × scenario × protocol × load shape |
| Target lifecycle | Implemented | Process, Docker, and external modes; readiness probing |
| Validation | Implemented | HTTP endpoint validation + H3 protocol proof |
| Load tools | Implemented | h2load (process + Docker), oha, managed HttpClient H3 |
| Artifact layout | Implemented | Deterministic paths, 60+ artifact types |
| Evidence classification | Implemented | 5-class system with comparability gates |
| Reporting | Implemented | Markdown summaries, aggregate JSON, claim levels, run index, public publication bundles |
| Adapter control plane | Implemented | v1 contract + conformance suite plus fake/reference fixture adapters |
| Network impairment | Modeled | Provider model exists; execution is deferred |
| WebTransport/MASQUE | Modeled | Scenario families are modeled; validators and load generators are deferred |
| Raw QUIC | Implemented - narrow reference slice | Fixture adapter-backed QUIC scenarios plus package-backed reference test-executor coverage for multiplex and duplex scenarios; production implementations are external packages |
| Hosted execution | Deferred | Not implemented in the public repo; handled as private/internal work |
| CI execution profile | Implemented as model | The enum exists, but execution wiring remains deferred |
