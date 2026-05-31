# Architecture — Runner Model

**Status:** Implemented (core lifecycle complete; hosted execution deferred)

## Purpose

The runner is the orchestration engine of ProtocolLab. It loads
implementation manifests and scenario definitions, expands a scenario matrix,
manages target lifecycle, performs validation, executes load tools, captures
artifacts, and produces reports. The runner is implementation-neutral — it
does not reference Incursa protocol assemblies or any other protocol
implementation directly.

## Assembly

- **Project:** `src/Incursa.ProtocolLab.Runner`
- **Namespace:** `Incursa.ProtocolLab.Runner`
- **Dependencies:** `Incursa.ProtocolLab.Model`, `Incursa.ProtocolLab.Adapter.Contracts`

## Internal Structure

```
Runner/
  Abstractions/       RunnerCommandResult, RunnerCommandOptions, RunnerMessageSeverity
  Compatibility/      CompatibilityClassifier, RunCellCompatibility, ScenarioSupport
  Diagnostics/        DiagnosticTarget, RuntimeCounterCapture, DockerContainerMetricsCapture,
                      ProcessMetricsCapture, RunMetadataCapture
  Events/             RunnerEvent, IRunnerEventSink, RecordingRunnerEventSink
  Lifecycle/          TargetOrchestrator, AdapterSessionOrchestrator, TargetExecutionResult
  LoadTools/          LoadToolInvoker, ManagedHttp3LoadGenerator, DockerResourceControl
  Orchestration/      RunnerEngine, RunPlanBuilder, ReportPublicationWorkflow
  Planning/           (matrix-related planning)
  Validation/         HttpScenarioValidator, ProtocolProofValidator, ScenarioValidator
```

## Core Concepts

### RunnerEngine

`RunnerEngine` is the primary entry point. It exposes:

- `CheckAsync()` — verify tool and manifest state
- `List()` — enumerate implementations, scenarios, load tools
- `ValidateAsync()` — run validation for selected cells
- `RunBenchmarkAsync()` — validate then benchmark for selected cells
- `Report()` — generate markdown summary for a completed run
- `PublishReportAsync()` — prepare a public-safe publication bundle from a completed run

Each method returns `RunnerCommandResult`, a structured result with messages
and artifact references. The runner emits events through `IRunnerEventSink`
for CLI consumption.

### RunPlanBuilder and Matrix Expansion

The runner builds a run plan by:

1. Loading implementation manifests from `implementations/`
2. Loading scenario definitions from `scenarios/`
3. Loading load tool manifests from `load-tools/`
4. Loading load profiles from `load-profiles/`
5. Expanding a scenario matrix: the Cartesian product of implementation ×
   scenario × protocol × connections × streams × repetitions × network
   profile

See [Scenario Model](scenario-model.md) for matrix expansion details.

### ExecutionProfile and RunCellIdentity

`ExecutionProfiles.Infer()` resolves the execution profile from CLI arguments,
target mode, target network mode, and any explicit override. The resulting
`ExecutionProfile` is carried on the `RunCell`, recorded in `RunMetadata`,
and normalized into the report and artifact pipeline.

`RunCellIdentity` composes the stable identity used for artifact paths and
report grouping. It includes the implementation id, scenario id, normalized
protocol id, execution profile id, network profile, load profile id,
connections, streams per connection, and repetition. The identity is
sanitized into deterministic path segments so collisions are avoided even
when names contain characters that would otherwise collide on disk.

### CompatibilityClassifier

Before execution, each cell is classified for compatibility. Classification
checks:

- Scenario role, protocol, family, and capability compatibility with the
  implementation manifest
- Network profile provider availability
- Load tool compatibility with the scenario protocol and traffic shape

Incompatible cells produce explicit outcomes with reasons (e.g.,
`MissingCapability`, `MissingLoadTool`, `ExperimentalNotEnabled`,
`PlaceholderNotRunnable`). Cells are never silently skipped.

### Target Lifecycle

`TargetOrchestrator` manages the lifecycle of a target server:

1. **Start** — launch the target as a process, Docker container, or connect
   to an external target, depending on the manifest's target kind.
2. **Readiness** — probe the target's readiness endpoint until it responds
   or a timeout is reached.
3. **Use** — the target is available for validation and benchmarking.
4. **Stop** — terminate or dispose the target, clean up Docker resources.

Docker mode supports:
- Published-port networking (host → container port mapping)
- Shared Docker network (generated bridge network with container aliases)
- Resource limits (CPU, memory) applied through Docker arguments
- Container metrics capture (CPU, memory, network via Docker stats)

### Validation

Validation is a gate performed before any benchmarking. It verifies:

1. **Protocol proof** — for HTTP/3 cells, prove that the target negotiates
   exact HTTP/3 without HTTP/1.1 or HTTP/2 fallback. Proof is performed via
   curl `--http3-only` or managed `HttpClient` with exact version policy.
2. **Endpoint validation** — verify that HTTP endpoints return expected
   status codes, content types, headers, and body content.
3. **Unsupported check** — cells that are incompatible with the target
   produce explicit unsupported outcomes.

Validation results are recorded separately from benchmark metrics. Protocol
proof artifacts (`protocol-proof.json`, `protocol-proof.stdout.txt`) are
distinct from load-tool artifacts.

### Load Tool Execution

`LoadToolInvoker` resolves and invokes load tools:

1. Select the load tool based on manifest configuration and cell
   compatibility.
2. Build the load tool command line (or Docker command) using the requested
   load shape (connections, streams, duration, warmup).
3. Execute the load tool as a process or Docker container.
4. Capture raw stdout and stderr.
5. Parse output best-effort into `HttpMetrics` (request rate, throughput,
   latency percentiles, connect time, TTFB).
6. If parsing fails, preserve raw artifacts and set
   `parsedMetricsAvailable=false`.

The runner also records the effective load shape and `LoadShapeSemantics` for
each result. This lets the report explain when a tool ignored, derived, or
constrained a requested field. Different tools may interpret the same load
profile differently; the semantics and warnings are part of the evidence, not
just implementation notes.

Load tools are categorized:
- `managed-lab` — in-process tools (managed HttpClient)
- `external-reference` — external tools (h2load, oha)
- `experimental` — experimental or custom tools

Results from different categories are never directly ranked together.

### Evidence Classification

`BenchmarkEvidenceEvaluator` classifies each benchmark result with:

- **Evidence class:** `local-smoke`, `local-lab`, `external-reference-local`,
  `isolated-host`, `publishable`
- **Comparability status:** whether results can be compared across
  implementations
- **Reasons:** specific factors affecting comparability (shared host, Docker
  rewrite, missing metrics, single repetition, etc.)

This classification appears in result JSON and aggregate reports.

Report claim level is a separate report-level concept. The runner computes it
in the report pipeline after results are collected, so the report can state
what a whole run is allowed to claim rather than forcing consumers to infer it
from per-result evidence alone.

### Diagnostics (Optional)

- `RuntimeCounterCapture` — captures `dotnet-counters` output for .NET
  targets during benchmark load. Requires `dotnet-counters` tool and
  resolved target process identity.
- `DockerContainerMetricsCapture` — captures Docker stats (CPU, memory,
  network) for Docker load-generator and target containers.
- `ProcessMetricsCapture` — captures process-level CPU and memory.
- `RunMetadataCapture` — captures host OS, .NET runtime, Docker backend,
  git commit, and other environment metadata.

Diagnostic data is best-effort. Missing diagnostics produce warnings, not
failures.

## Events

The runner emits events during execution through `IRunnerEventSink`:

- `RunnerEvent` carries the command kind, severity, message, and timestamp.
- `RecordingRunnerEventSink` captures events for replay.
- `NoopRunnerEventSink` discards events.

The CLI uses events to render console output without coupling presentation
to orchestration logic.

## Proposed Extensions

- **CI execution profile:** A run configuration that pre-selects
  implementations, scenarios, load tools, and profiles for pipeline
  execution with non-zero exit codes on failure.
- **Hosted execution backend:** An `IRunnerHost` or similar abstraction
  that delegates lifecycle to a remote environment with verified
  provenance.
- **Plugin model:** A way for external adapters and load tools to register
  without recompiling the runner.

## Related Documents

- [Scenario Model](scenario-model.md) — how scenarios and matrices work
- [Adapter Model](adapter-model.md) — adapter control plane contract
- [Load Model](load-model.md) — load tools, load shapes, and load profiles
- [Artifact Model](artifact-model.md) — artifact layout and paths
- [Architecture Overview](overview.md)
