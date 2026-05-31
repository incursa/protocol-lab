# Architecture — Report Model

**Status:** Implemented (RunReport, RunAggregate, MarkdownSummaryWriter,
and evidence classification exist; ReportClaimLevel, EnvironmentManifest, and
structured report pipeline remain proposed)

## Purpose

The report model defines how ProtocolLab transforms raw measurement data
into structured, consumable reports. Reports answer four questions:

1. **What was tested?** — implementation, scenario, protocol, load shape
2. **Where did it run?** — execution profile, environment metadata
3. **What was measured?** — metrics, artifacts, evidence
4. **How much confidence does the result deserve?** — claim level, evidence
   class, comparability class

## Core Concepts

### ReportClaimLevel

`ReportClaimLevel` defines the maximum claim a report can make based on its
execution profile, measurement profile, and evidence completeness.

| Level | Meaning | Requires |
|-------|---------|----------|
| `DiagnosticOnly` | Data is for debugging or development; no performance claims | Any run that produced output |
| `Validation` | Target passed functional validation for the scenario | Validation status `passed` |
| `Regression` | Measurements are suitable for detecting performance changes over time | Same execution profile across runs, multiple repetitions, stable environment |
| `Benchmark` | Measurements are suitable for absolute performance claims | Controlled environment, known resource constraints, attested metadata |
| `Verified` | Measurements have been independently verified and attested | Controlled lab, retained artifacts, signed metadata, external review |

**Design rule:** The claim level constrains what a reader should believe.
A `DiagnosticOnly` report that shows 500K req/s should not be cited as a
benchmark. A `Verified` report should include artifact links and environment
attestations.

**Current implementation:** The runner does not have a `ReportClaimLevel`
type. The existing `BenchmarkEvidenceAssessment` conveys similar information
through `EvidenceClass` and `ComparabilityStatus`, but there is no explicit
claim level on the report as a whole.

### Claim Level Derivation

The claim level is derived from:
- Execution profile (e.g., `DedicatedLabBareMetal` supports higher claims than `LocalProcess`)
- Measurement profile (e.g., `Benchmark` supports higher claims than `Smoke`)
- Repetition count and stability
- Evidence class of constituent results
- Completeness of environment metadata

```
ClaimLevel = f(ExecutionProfile, MeasurementProfile,
               Repetitions, EvidenceClass, MetadataCompleteness)
```

### EnvironmentManifest

`EnvironmentManifest` captures the full execution context of a run. It is
the structured representation of "where did it run."

| Field | Type | Source (Current) |
|-------|------|------------------|
| OsDescription | string | `RunMetadata.OperatingSystem` |
| RuntimeDescription | string | `RunMetadata.FrameworkDescription` |
| ProcessArchitecture | string | `RunMetadata.ProcessArchitecture` |
| OsArchitecture | string | `RunMetadata.OperatingSystemArchitecture` |
| ProcessorCount | int | `RunMetadata.ProcessorCount` |
| TotalMemoryBytes | long? | `RunMetadata.TotalAvailableMemoryBytes` |
| ContainerRuntime | string? | `RunMetadata.DockerVersion` |
| ContainerBackend | string? | `RunMetadata.DockerBackend` |
| ContainerLimits | ContainerLimits? | `DockerResourceLimits` on BenchmarkResult |
| RunnerVersion | string | (not captured; proposed) |
| ScenarioCatalogVersion | string | (not captured; proposed) |
| TargetImage | string? | Implementation manifest image tag |
| TargetImageDigest | string? | Docker inspect image digest |
| NetworkMode | string? | `RunMetadata.NetworkMode` |
| ExecutionProfile | ExecutionProfile | (proposed; see measurement model) |
| HostName | string | `RunMetadata.HostName` |
| GitCommit | string? | `RunMetadata.GitCommit` |
| GitTreeStatus | string? | `RunMetadata.WorkingTreeStatus` |
| TimestampUtc | DateTimeOffset | `RunMetadata.TimestampUtc` |
| RunnerPid | int? | `RunMetadata.ProcessId` |

**Current implementation:** `RunMetadata` captures many of these fields but
not all. Missing: `RunnerVersion`, `ScenarioCatalogVersion`, `TargetImageDigest`,
`ContainerLimits` (structured), `ExecutionProfile`.

### Report Pipeline

```
Raw Measurements (per-cell)
    |
    v
BenchmarkResult (per-cell, per-repetition)
    |
    v
BenchmarkEvidenceEvaluator.Assess()
    |  -- classifies evidence, comparability
    v
RunReportBuilder
    |  -- groups by RunGroupKey
    |  -- computes MetricTriple (median/best/worst)
    |  -- builds RunAggregate[]
    v
RunReport
    |  -- RunMetadata + RunTotals + RunAggregate[]
    v
MarkdownSummaryWriter
    |  -- renders summary.md
    v
aggregate-results.json + summary.md
```

**Future pipeline extension (proposed):**

```
RunReport
    |
    v
ClaimLevelDeriver
    |  -- computes ReportClaimLevel from profile + evidence + metadata
    v
ReportWithClaims
    |  -- RunReport + ClaimLevel + EnvironmentManifest + Warnings
    v
ReportRenderer  (markdown, JSON, HTML, dashboard)
```

## Design Rules

### Report Output Is Not the Private/Internal Product

The report itself — the markdown file, the JSON aggregate — is public-canonical
functionality. It should be complete and useful on its own. The private/internal
layer (if pursued) is:

- **Trusted execution** — controlled lab or hosted environment with attested
  provenance.
- **Retained history** — long-term artifact storage with audit trails.
- **Private CI** — integration with private artifact stores and private/internal
  CI systems.
- **Extended scenarios** — proprietary or specialized protocol scenarios.
- **Expert diagnosis** — deep protocol trace analysis and performance
  profiling.

A markdown summary from `LocalDockerBridge` is honest and useful, but it is
not a product. The infrastructure that produces a `Verified` claim with
retained artifacts and attested metadata is the private/internal layer.

### Reports Must Distinguish Self-Run from Controlled

Every report must make clear whether it was produced by a self-serve
public-canonical run or a controlled/verified run. This is enforced through:
- Evidence class on every result
- Claim level on every report
- Warnings for missing metadata
- Explicit "not publishable" markers on local results

### Reports Must Be Reproducible

Given the same run ID and the same artifacts, re-running the report
generation must produce identical output. Report generation must not depend
on network access, live Docker state, or transient host conditions.

### Reports Must Be Honest About Missing Data

Missing metrics, unavailable collectors, and parse failures must appear in
the report as explicit gaps, not as zero values or silence. A report that
shows 0% CPU because the collector was unavailable is misleading.

### Controlled Reports Require Strict Conditions

A report can claim `Verified` only when:
- Execution profile is `DedicatedLabBareMetal` or `DedicatedLabContainer`.
- Measurement profile is `Benchmark` or `Soak`.
- Environment manifest is complete (all fields present, no warnings).
- Artifacts are retained and accessible (raw stdout/stderr, Docker inspect,
  qlogs, pcaps where applicable).
- Scenario catalog version is stable and recorded.
- Runner version is recorded.
- Target image digest is recorded.
- Multiple repetitions produced stable results.

## Current Report Gaps

| Gap | Detail |
|-----|--------|
| No `ExecutionProfile` type | Profile is inferred from CLI args, not recorded as a typed value |
| No `MeasurementProfile` type | Load profiles shape the load but do not select collectors or constrain claims |
| No `ReportClaimLevel` type | Evidence class is per-result, but no report-level claim exists |
| No `EnvironmentManifest` | `RunMetadata` covers most fields but not runner version, catalog version, image digest, or structured container limits |
| No collector identity on samples | Individual samples carry domain-specific fields but not a unified collector identity |
| No `RunnerVersion` capture | The runner does not record its own version in metadata |
| No `ScenarioCatalogVersion` | Scenario YAML files are loaded without version tracking |
| Markdown is the only renderer | No HTML, JSON-schema-validated, or dashboard renderer |

## Relationship to Existing Types

| Proposed Type | Relationship to Existing Code |
|---------------|-------------------------------|
| `ReportClaimLevel` | New enum; constrains what `BenchmarkEvidenceAssessment.EvidenceClass` implies |
| `EnvironmentManifest` | Extends `RunMetadata` with additional fields + formal ExecutionProfile |
| `ClaimLevelDeriver` | New class; consumes `RunReport` and produces a claim |
| `ReportWithClaims` | Wraps `RunReport` with claim-level metadata |

## Related Documents

- [Measurement Model](measurement-model.md) — how measurements are collected and classified
- [Runner Model](runner-model.md) — how the runner produces results
- [Artifact Model](artifact-model.md) — how report artifacts are stored
- [Product Boundaries](../protocol-lab/product-boundaries.md) — public-canonical vs private/internal reporting
- [Architecture Overview](overview.md)
