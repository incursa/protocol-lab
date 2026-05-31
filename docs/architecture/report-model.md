# Architecture - Report Model

**Status:** Implemented (RunReport, RunAggregate, MarkdownSummaryWriter, evidence classification, and ReportClaimLevel are implemented; EnvironmentManifest and structured dashboard pipeline remain proposed)

## Purpose

The report model defines how ProtocolLab transforms raw measurement data
into structured, consumable reports. Reports answer four questions:

1. **What was tested?** - implementation, scenario, protocol, load shape
2. **Where did it run?** - execution profile, environment metadata
3. **What was measured?** - metrics, artifacts, evidence
4. **How much confidence does the result deserve?** - claim level, evidence
   class, comparability class

## Core Concepts

### ReportClaimLevel

`ReportClaimLevel` defines the maximum claim a report can make based on its
execution profile, load-profile purpose, repetitions, evidence completeness,
and metadata quality.

| Level | Meaning | Requires |
|-------|---------|----------|
| `DiagnosticOnly` | Data is for debugging or development; no performance claims | Any run that produced output |
| `Validation` | Target passed functional validation for the scenario | Validation status `passed` |
| `Regression` | Measurements are suitable for detecting performance changes over time | Stable execution profile, multiple repetitions, adequate metadata |
| `Benchmark` | Measurements are suitable for absolute performance claims | Controlled environment, known resource constraints, publishable intent |
| `Verified` | Measurements have been independently verified and attested | Controlled lab, retained artifacts, signed metadata, external review (reserved for future controlled/private attestation) |

**Design rule:** The claim level constrains what a reader should believe.
A `DiagnosticOnly` report that shows 500K req/s should not be cited as a
benchmark. A `Verified` report should include artifact links and environment
attestations.

**Current implementation:** `ReportClaimLevel` is implemented in the public
model. `ReportClaimDeriver.Derive(...)` currently emits `DiagnosticOnly`,
`Validation`, `Regression`, and `Benchmark`. `Verified` is modeled as a
reserved future claim for controlled/private attestation.
`ReportClaimLevels.IsPublishable(...)` gates the publishable levels.

### Claim Level Derivation

The claim level is derived from:
- Execution profile
- Load-profile purpose
- Repetition count and stability
- Evidence class of constituent results
- Completeness of environment metadata

```
ClaimLevel = f(ExecutionProfile, LoadProfilePurpose,
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
| ContainerLimits | ContainerLimits? | `DockerResourceLimits` on `BenchmarkResult` |
| RunnerVersion | string | (not captured; proposed) |
| ScenarioCatalogVersion | string | (not captured; proposed) |
| TargetImage | string? | Implementation manifest image tag |
| TargetImageDigest | string? | Docker inspect image digest |
| NetworkMode | string? | `RunMetadata.NetworkMode` |
| ExecutionProfile | ExecutionProfile | `RunMetadata.ExecutionProfile` |
| HostName | string | `RunMetadata.HostName` |
| GitCommit | string? | `RunMetadata.GitCommit` |
| GitTreeStatus | string? | `RunMetadata.WorkingTreeStatus` |
| TimestampUtc | DateTimeOffset | `RunMetadata.TimestampUtc` |
| RunnerPid | int? | `RunMetadata.ProcessId` |

**Current implementation:** `RunMetadata` captures many of these fields but
not all. Missing: `RunnerVersion`, `ScenarioCatalogVersion`,
`TargetImageDigest`, and `ContainerLimits` as a structured manifest object.

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
    |  -- derives claim level
    |  -- builds RunAggregate[]
    v
RunReport
    |  -- RunMetadata + RunTotals + RunAggregate[]
    |  -- ClaimLevel
    v
MarkdownSummaryWriter
    |  -- renders summary.md
    v
aggregate-results.json + summary.md
```

`ReportClaimLevel` and `BenchmarkEvidenceAssessment` are separate concepts:
evidence class is per-result, while claim level is report-wide.

### Public Publication Bundles

`publish-report` converts a completed run into a public-safe publication
bundle. The bundle is a derivative view of the run, not a new canonical
source of truth. It is intentionally narrow:

- it reads completed run artifacts and the evidence report JSON
- it copies only public-safe artifacts into a staged publication output
- it writes a sanitized `evidence-report-v1.json` and Markdown rendering
- it emits `publication-manifest.json`, `publication-warnings.md`,
  `publication-skipped.md`, and registry JSON for the public site

The bundle workflow must keep claim semantics driven by the Evidence Report
v1 JSON. It may label `DiagnosticOnly` and `publishable=false` explicitly,
but it must not invent verified or official claims that the source data does
not allow.

## Design Rules

### Report Output Is Not the Private/Internal Product

The report itself - the markdown file, the JSON aggregate - is public-canonical
functionality. It should be complete and useful on its own. The private/internal
layer is where hosted execution, retained history, dashboards, and deeper
attestation live.

A markdown summary from `LocalDockerBridge` is honest and useful, but it is
not a publishable benchmark by default. `Benchmark` is the publishable claim
currently emitted by the public derivation path. `Verified` is reserved for
future controlled/private attestation. The public/community repo must not
fabricate either claim.

### Reports Must Distinguish Self-Run from Controlled

Every report must make clear whether it was produced by a self-serve
public-canonical run or a controlled/verified run. This is enforced through:
- Evidence class on every result
- Claim level on every report
- Warnings for missing metadata
- Explicit publishable-gating checks

### Reports Must Be Reproducible

Given the same run ID and the same artifacts, re-running the report
generation must produce identical output. Report generation must not depend
on network access, live Docker state, or transient host conditions.

### Reports Must Be Honest About Missing Data

Missing metrics, unavailable collectors, and parse failures must appear in the
report as explicit gaps, not as zero values or silence. A report that shows 0%
CPU because the collector was unavailable is misleading.

### Controlled Reports Require Strict Conditions

A future report can claim `Verified` only when:
- Execution profile is `DedicatedLabBareMetal` or `DedicatedLabContainer`.
- Load profile purpose is `PublishableBenchmark`.
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
| No `EnvironmentManifest` | `RunMetadata` covers most fields but not runner version, catalog version, image digest, or structured container limits |
| No collector identity on samples | Individual samples carry domain-specific fields but not a unified collector identity |
| No `RunnerVersion` capture | The runner does not record its own version in metadata |
| No `ScenarioCatalogVersion` | Scenario YAML files are loaded without version tracking |
| Markdown is the only renderer | No HTML, JSON-schema-validated, or dashboard renderer |

## Relationship to Existing Types

| Proposed Type | Relationship to Existing Code |
|---------------|-------------------------------|
| `EnvironmentManifest` | Extends `RunMetadata` with additional fields + a formal, persisted execution snapshot |
| `ClaimLevelDeriver` | Implemented as `ReportClaimDeriver`; consumes `RunReport` inputs and produces a claim |
| `ReportWithClaims` | Future wrapper around `RunReport` with claim-level metadata |

## Related Documents

- [Measurement Model](measurement-model.md) - how measurements are collected and classified
- [Runner Model](runner-model.md) - how the runner produces results
- [Artifact Model](artifact-model.md) - how report artifacts are stored
- [Public Report Publication Bundle](../reports/publication-bundle.md) - staged public bundle format and command flow
- [Public Report Safety](../reports/public-report-safety.md) - safety rules for public bundles
- [Product Boundaries](../protocol-lab/product-boundaries.md) - public-canonical vs private/internal reporting
- [Architecture Overview](overview.md)
