# Architecture Decision Records

**Status:** Proposed (4 ADRs written, all in Proposed status)

## Purpose

This directory contains Architecture Decision Records (ADRs) for ProtocolLab.
ADRs capture significant architectural decisions, the context in which they
were made, the options considered, and the rationale for the chosen approach.

## Format

Each ADR is a Markdown file named `NNNN-title-with-dashes.md` where `NNNN` is
a zero-padded sequential number. ADRs follow a consistent structure:

```markdown
# ADR-NNNN: Title

## Status
Proposed | Accepted | Deprecated | Superseded

## Context
What is the issue, and why does a decision need to be made?

## Decision
What was decided?

## Consequences
What becomes easier or harder as a result?

## Alternatives Considered
What other options were evaluated, and why were they rejected?
```

## Index

| ADR | Title | Status |
|-----|-------|--------|
| [ADR-0001](ADR-0001-measurement-provenance.md) | Measurement Provenance | Proposed |
| [ADR-0002](ADR-0002-execution-profiles.md) | Execution Profiles | Proposed |
| [ADR-0003](ADR-0003-report-claim-levels.md) | Report Claim Levels | Proposed |
| [ADR-0004](ADR-0004-public-private-repository-split.md) | Public / Private Repository Split | Proposed |

Decisions currently documented elsewhere:

| Decision | Documented In |
|----------|--------------|
| Implementation-neutral runner | [Architecture Overview](../architecture/overview.md), [Vision](../protocol-lab/vision.md) |
| Validation-before-benchmark gate | [Validation vs Benchmarking](../spec/validation-vs-benchmarking.md) |
| Adapter control plane separation | [Adapter Model](../architecture/adapter-model.md), [`adapter-contract-v1.md`](../architecture/adapter-contract-v1.md) |
| Evidence classification system | [Architecture Overview](../architecture/overview.md), [Validation vs Benchmarking](../spec/validation-vs-benchmarking.md) |
| Deterministic artifact layout | [Artifact Model](../architecture/artifact-model.md) |
| Best-effort parsing | [Load Model](../architecture/load-model.md) |
| Public/canonical vs private/internal layer separation | [Product Boundaries](../protocol-lab/product-boundaries.md) |
| Public/private repository split | [Public Seed Readiness](../protocol-lab/public-seed-readiness.md), [Product Boundaries](../protocol-lab/product-boundaries.md), [ADR-0004](ADR-0004-public-private-repository-split.md) |
| Deferred workload families (WebTransport, MASQUE) | [Future Workload Families](../spec/future-workload-families.md) |
| Database workloads out of scope for v1 | [Database Workloads Decision](../spec/database-workloads-decision.md) |

## ADR Candidates Still Needing Coverage

| Topic | Where It Is Discussed Now |
|-------|---------------------------|
| Adapter discovery and broader adapter registry | [Adapter Model](../architecture/adapter-model.md), [Runner Model](../architecture/runner-model.md) |
| Load tool registry and streaming metrics | [Load Model](../architecture/load-model.md) |
| Hosted execution backend and CI execution profile | [Runner Model](../architecture/runner-model.md), [Product Boundaries](../protocol-lab/product-boundaries.md) |
| Public validation matrix artifact publication policy | [Public Seed Readiness](../protocol-lab/public-seed-readiness.md), [Product Boundaries](../protocol-lab/product-boundaries.md) |

## Conventions

- ADRs are immutable once accepted. Changes are recorded in a new ADR that
  supersedes or deprecates the old one.
- ADRs are numbered sequentially. Numbers are never reused.
- Every ADR must state its status clearly.
- ADRs are technical, not aspirational. They describe what was decided, not
  what might be decided later.

## Related Documents

- [Architecture Overview](../architecture/overview.md)
- [Product Boundaries](../protocol-lab/product-boundaries.md)
- [Roadmap](../protocol-lab/roadmap.md)
