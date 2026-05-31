# ADR-0001: Measurement Provenance

## Status

Proposed

## Context

ProtocolLab collects measurements from multiple sources: load-tool output
parsers, process resource snapshots, Docker container stats, .NET runtime
counters, and (in the future) qlog traces and packet captures. These
measurements have different trust characteristics. A requests-per-second
metric parsed from h2load JSON on a dedicated lab machine is not the same
claim as a requests-per-second metric from the managed HttpClient on a
developer laptop.

Currently, measurement data flows into `BenchmarkResult` as typed fields
(`HttpMetrics`, `ServerMetrics`, `ProcessMetricSample`, etc.) without a
unified provenance model. The evidence classification system
(`BenchmarkEvidenceAssessment`) captures per-result context, but individual
metric samples do not carry their own provenance.

Consumers of ProtocolLab results need to know:

- Which collector produced each measurement.
- Whether the collector was operating in its intended configuration.
- What execution profile the measurement was collected under.
- What measurement profile (smoke, diagnostic, regression, benchmark, soak)
  governed the collection.

Without this information, consumers must either trust all measurements equally
(which is incorrect) or manually reconstruct provenance from result metadata.

## Decision

Every measurement sample recorded by ProtocolLab will include provenance
information. Specifically:

1. Introduce a `MeasurementCollector` abstraction that identifies the source
   of measurements (identity, version, configuration, availability).

2. Introduce a `MeasurementSample` record that carries, at minimum:
   - `MetricName` — what was measured
   - `Value` and `Unit` — the measurement
   - `Source` — which entity the metric describes (target, load-generator,
     runner)
   - `Scope` — target, load-generator, or runner
   - `CollectorId` — identity of the producing collector
   - `TimestampUtc` — when the sample was taken
   - `Interval` — observation interval for rate/counter metrics
   - `Tags` — optional dimensions

3. Every collector must declare its identity and whether it is operating in a
   supported configuration for the current execution profile.

4. The existing `BenchmarkResult` will continue to carry structured typed
   metrics (`HttpMetrics`, `ServerMetrics`) for backward compatibility. The
   new sample types are additive, not a replacement.

5. The evidence classification system will incorporate collector availability
   and configuration into its assessment — a result where the process resource
   collector was unavailable receives different evidence from one where all
   collectors were active.

## Consequences

### Positive

- Consumers can determine the trustworthiness of individual metrics by
  inspecting provenance rather than inferring it from run-level metadata.
- New collectors can be added without changing the result schema — they
  produce `MeasurementSample` records with a unique collector identity.
- Tooling (dashboards, analysis scripts) can filter or weight measurements
  by collector identity and execution profile.
- The gap between local diagnostic data and controlled benchmark data is
  explicitly captured in the data, not just in documentation.

### Negative

- Adds type-surface to the model (`MeasurementCollector`, `MeasurementSample`).
- Existing collectors (`ProcessMetricsCapture`, `DockerContainerMetricsCapture`,
  `RuntimeCounterCapture`) must be adapted to produce collector-identified
  samples, which is refactoring work.
- Reports and aggregates must be updated to render provenance information.

### Neutral

- The existing typed metric records (`HttpMetrics`, `ServerMetrics`) remain
  the primary API for common metrics. `MeasurementSample` is for collector-
  produced diagnostic and resource metrics.
- This decision does not force every metric in `BenchmarkResult` to be
  represented as a `MeasurementSample`. Parse-produced metrics from load tools
  may continue to use their existing typed records.

## Related

- [ADR-0002: Execution Profiles](ADR-0002-execution-profiles.md)
- [ADR-0003: Report Claim Levels](ADR-0003-report-claim-levels.md)
- [Measurement Model](../architecture/measurement-model.md)
- [Report Model](../architecture/report-model.md)
