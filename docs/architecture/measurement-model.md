# Architecture - Measurement Model

**Status:** Implemented (execution profile, effective load shape, per-cell metrics, and evidence classification exist; formal MeasurementProfile, MeasurementCollector, MeasurementSample, and ComparabilityClass types remain proposed)

## Purpose

The measurement model defines how ProtocolLab collects, structures, and
classifies measurement data from protocol targets and load generators.
Different execution environments and measurement intents produce data of
different trust levels. The model establishes vocabulary and contracts so
that consumers do not mistake a local smoke run for a controlled benchmark.

Architecture Alignment v1 is not the full Measurement Foundation v1. It
implements the execution-profile and effective-load-shape pieces first, while
sample-level provenance remains a future layer.

## Core Concepts

### ExecutionProfile

`ExecutionProfile` describes where and how a run executes. The profile is
implemented and recorded on `RunMetadata`, `RunCell`, `BenchmarkResult`, and
report grouping. It determines which collectors and controls are meaningful
and sets the baseline for evidence classification and claim derivation.

| Profile | Current Code Presence | Description |
|---------|----------------------|-------------|
| `LocalProcess` | Implemented (default mode) | Runner and target on the same host, started as child processes |
| `LocalDockerBridge` | Implemented (`target-mode docker`) | Target in Docker with published-port networking |
| `LocalDockerHostNetwork` | Implemented as model | Target in Docker with host network mode |
| `RemoteProcess` | Implemented as model | Runner and target on separate hosts, process-mode target |
| `RemoteDocker` | Implemented as model | Runner and target on separate hosts, Docker-mode target |
| `CiContainer` | Implemented as model | Runner inside a CI container (e.g., GitHub Actions), Docker-in-Docker or sibling |
| `DedicatedLabBareMetal` | Implemented as model | Controlled lab environment, bare-metal target, no container overhead |
| `DedicatedLabContainer` | Implemented as model | Controlled lab environment, containerized target with known resource constraints |

**Design rule:** The execution profile is recorded once per run and applies
to every cell in that run. Cells within a single run share the same profile.

**Current implementation:** The runner has an `ExecutionProfile` enum and an
`ExecutionProfiles.Infer(...)` helper. Execution mode is inferred from CLI
arguments (`--target-mode`, `--target-network-mode`) and explicit overrides,
then recorded in `RunMetadata`, `RunCell`, and `BenchmarkResult`. The
`BenchmarkEvidenceEvaluator` and report claim derivation both use the
profile.

### Requested vs Effective Load Shape

Requested load shape is what the user or scenario asked for. Effective load
shape is what the selected load tool actually executed after it applied its
own constraints. `LoadShapeSemantics` records the difference, including which
fields were supported, ignored, derived, or unsupported, plus warnings.

This is not cosmetic metadata. Different tools may interpret the same load
profile differently, so the requested shape and the effective shape must be
read independently.

### MeasurementProfile

`MeasurementProfile` describes the intent and intensity of a measurement run.
It influences which collectors are active, what load shapes are appropriate,
and what claim level the results support.

| Profile | Purpose | Typical Duration | Typical Repetitions | Typical Collectors |
|---------|---------|-----------------|--------------------|--------------------|
| `Smoke` | Quick functional check | 5-15 seconds | 1 | Runner timing, process resource |
| `Diagnostic` | Deep instrumentation for debugging | Variable | 1 | All available: counters, qlog, pcap, events |
| `Regression` | Detect performance changes over time | 30-120 seconds | 3-5 | Runner timing, process/Docker resource, counters |
| `Benchmark` | Measure absolute performance | 60-300 seconds | 5-10 | Runner timing, process/Docker resource, counters, qlog |
| `Soak` | Detect memory leaks, degradation over time | 600+ seconds | 1 | Runner timing, process/Docker resource, counters |

**Design rule:** The measurement profile constrains what claim level a
result can support. A `Smoke` profile never produces a `Benchmark` claim.

**Current implementation:** The runner does not have a `MeasurementProfile`
type. Load profiles (`load-profiles/*.yaml`) serve a similar role for load
shape defaults but do not encode collector selection or claim constraints.

### MeasurementCollector

A `MeasurementCollector` is a named, independently-configurable source of
measurement data. Each collector produces one or more `MeasurementSample`
records.

| Collector | Current Implementation | Status |
|-----------|----------------------|--------|
| `runner-timing` | Implicit in `RunnerEngine` elapsed-time tracking | Implemented |
| `process-resource` | `ProcessMetricsCapture`, `TargetProcessMetrics` | Implemented |
| `docker-stats-load-generator` | `DockerContainerMetricsCapture` for load tool | Implemented |
| `docker-stats-target` | `DockerContainerMetricsCapture` for target | Implemented |
| `dotnet-counters` | `RuntimeCounterCapture`, `RuntimeCounterSummary` | Implemented |
| `qlog` | Path reserved in `ArtifactLayout`; capture depends on target | Modeled |
| `packet-capture` | Path reserved; no capture implementation | Deferred |
| `scenario-event` | Not implemented | Deferred |

**Design rule:** Every collector must:
- Declare its identity and version.
- State whether it is available in the current execution profile.
- Produce samples with collector identity attached.
- Handle failure gracefully - unavailable collectors produce warnings, not
  errors.

**Current implementation:** Collectors exist as standalone static classes
without a common interface. There is no `ICollector` or `MeasurementCollector`
abstraction.

### MeasurementSample

A `MeasurementSample` is a single observation from a collector at a point in
time or over an interval.

**Proposed type shape:**

| Field | Type | Purpose |
|-------|------|---------|
| MetricName | string | What was measured (e.g., `cpu_percent`, `working_set_bytes`) |
| Value | double | Numeric value |
| Unit | string | Unit of measurement (e.g., `percent`, `bytes`, `requests_per_second`) |
| Source | string | Origin of the value (e.g., `target-process`, `load-generator-container`) |
| Scope | string | What the metric applies to (`target`, `load-generator`, `runner`) |
| CollectorId | string | Identity of the collector that produced this sample |
| TimestampUtc | DateTimeOffset | When the sample was taken |
| Interval | TimeSpan? | Observation interval (for rate/counter metrics) |
| Tags | Dictionary<string,string>? | Optional dimensions (e.g., `connection=5`, `stream=1`) |

**Current implementation:** Individual sample types exist per collector
(`ProcessMetricSample`, `DockerContainerMetricSample`) but there is no
unified `MeasurementSample` type. The monolithic `BenchmarkResult` carries
metrics inline rather than as a list of typed samples.

### ComparabilityClass

`ComparabilityClass` defines how measurement results from different runs or
cells may be compared. This extends the existing
`BenchmarkComparabilityStatuses` constants with a more structured taxonomy.

| Class | Meaning | Equivalent (Current Code) |
|-------|---------|--------------------------|
| `None` | Cannot be compared; data is diagnostic or incomplete | `invalid` |
| `SameRun` | Comparable only within the same run and profile | `not-comparable` across runs |
| `SameProfile` | Comparable across runs that share the same execution and measurement profile | `comparable-with-warnings` |
| `LabControlled` | Comparable across runs in the same controlled lab environment | `comparable-local` (with lab constraints) |
| `PublicReference` | Comparable across any environment; environment metadata is complete and attested | `comparable-local` (with full attestation) |

**Current implementation:** `BenchmarkComparabilityStatuses` defines
`comparable-local`, `comparable-with-warnings`, `not-comparable`, and
`invalid` as string constants. There is no enum for comparability.

## Design Rules

### Validation is Portable; Benchmarking is Profile-Bound

Validation (endpoint correctness, protocol proof, behavior checks) should
produce the same result regardless of execution profile. A target that
passes HTTP/3 validation on a developer workstation should also pass on
dedicated lab hardware.

Benchmarking (throughput, latency, resource consumption) is sensitive to the
execution environment. A requests-per-second measurement from
`LocalDockerBridge` on an overloaded developer laptop is not directly
comparable to the same measurement from `DedicatedLabBareMetal` with known
CPU isolation.

### Container Runs Are Valid, But Labeled

Docker and container execution is a valid and useful workflow. Target and
load-generator containers can isolate dependencies, control resource limits,
and produce reproducible results. However, performance measurements from
containers must be labeled with the execution profile and container resource
configuration. Results from `LocalDockerBridge` must not be presented as
equivalent to bare-metal results.

### Local Developer Runs Are Diagnostic, Not Controlled

Local runs (local process, local Docker bridge) are essential for
development, debugging, and quick comparisons. They produce valid
diagnostic data. They are not controlled benchmark claims. Every local result
must clearly state that it is local evidence.

### Every Metric Must Include Provenance

Every measurement must carry:
- Which collector produced it.
- Which execution profile the run used.
- Whether the collector was operating in its intended configuration.
- Any warnings or caveats about the measurement.

Provenance is not optional. A metric without provenance is ambiguous.

## Relationship to Existing Types

| Proposed Type | Relationship to Existing Code |
|---------------|-------------------------------|
| `ExecutionProfile` | Implemented and recorded on run metadata, cells, results, and reports |
| `MeasurementProfile` | Proposed; adds collector selection and claim constraints to existing `LoadProfileDefinition` |
| `MeasurementCollector` | Proposed; abstracts over existing static collectors (`ProcessMetricsCapture`, `DockerContainerMetricsCapture`, `RuntimeCounterCapture`) |
| `MeasurementSample` | Proposed; would unify `ProcessMetricSample`, `DockerContainerMetricSample` into a common shape |
| `ComparabilityClass` | Proposed; would type the existing `BenchmarkComparabilityStatuses` string constants as an enum |

## Proposed Future Types (Not Yet in Code)

These types should be added to `Incursa.ProtocolLab.Model` when the remaining
measurement-foundation work begins:

- `MeasurementProfile` - enum with the five values listed above
- `MeasurementCollector` - record or interface for collector identity and
  lifecycle
- `MeasurementSample` - record as described above
- `ComparabilityClass` - enum with the five values listed above

Implementation of these types should remain additive - existing code paths
(`BenchmarkResult`, `BenchmarkEvidenceEvaluator`, individual collectors)
should continue to work. The new types provide a vocabulary and a contract;
refactoring existing code to use them is a separate step.

## Related Documents

- [Report Model](report-model.md) - how measurements become reports
- [Scenario Model](scenario-model.md) - how scenarios define measurement intents
- [Load Model](load-model.md) - how load tools produce measurements
- [Artifact Model](artifact-model.md) - how measurement artifacts are stored
- [Product Boundaries](../protocol-lab/product-boundaries.md) - how measurement trust separates public-canonical from private/internal layers
- [Architecture Overview](overview.md)
