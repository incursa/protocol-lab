# ProtocolLab - Product Boundaries

**Status:** Current (public/community repo is canonical; sibling internal repo consumes public contracts and extends them privately)

## Overview

ProtocolLab now has an actual public/private repository split:

- `Incursa.ProtocolLab` is the public/community repository.
- `Incursa.ProtocolLab.Internal` is the sibling internal repository.

The public repo is the canonical source for shared contracts, public docs,
and community-friendly validation behavior. The internal repo may depend on
the public repo, but the public repo must not depend on internal code,
private configuration, or private services.

## Layer Model

```
+--------------------------------------------------+
| Internal / Private Layer                         |
| Hosted execution, retained artifacts,            |
| private CI, dashboards, diagnostics,             |
| extended scenarios, unreleased work              |
+--------------------------------------------------+
| Public / Community Layer                         |
| Local validation, local benchmarking,            |
| Docker execution, CI automation,                 |
| shared contracts, open artifact format           |
+--------------------------------------------------+
```

## Public / Community Layer

The public/community layer is the user-facing ProtocolLab surface. It is
designed to be complete and useful on its own.

**Implemented capabilities:**
- Local process and Docker target execution
- Local validation with HTTP/3 protocol proof
- Local benchmarking with `h2load`, `oha`, and managed `HttpClient` load tools
- Deterministic artifact layout under `.artifacts/runs/{runId}`
- Collision-proof run-cell identity in artifact paths
- Evidence classification and comparability gates
- Report claim levels with publishable gating
- Markdown summaries and aggregate JSON reports
- Adapter control plane contract for external lifecycle management
- Suite definitions for repeatable run configurations

**Public/community invariants:**
- Public docs are authored here first.
- Shared contracts are published from the public repo and consumed by the internal repo.
- Public/community outputs must not fabricate controlled-run or publishable provenance.
- Local/shared-host evidence is honest and useful, but it is not equivalent to an attested benchmark.

## Private / Internal Layer

The internal layer adds capabilities that are intentionally not part of the
public/community surface:

- Hosted execution
- Attested provenance and retained artifacts
- Extended scenarios and private adapters
- Private CI integration and retention policy control
- Diagnostic analysis and deeper instrumentation
- Internal scripts, release workflows, and unreleased work

## Boundary Enforcement

The following constraints keep the split honest:

1. **Internal may depend on public.** Internal code should reference public
   shared contracts or packages instead of carrying silent duplicates.
2. **Public must not depend on internal.** No public runtime or docs path may
   require internal assemblies, services, or data.
3. **No fabricated provenance.** Public/community results must not imply
   controlled, hosted, or publishable provenance unless the gating conditions
   are actually met.
4. **Separate configuration.** Internal execution configuration lives outside
   the public CLI surface and public docs.
5. **Contract-first integration.** Any hosted or private service must integrate
   through documented contracts that the public repo already supports.
6. **No implied commercial service.** Public docs, templates, and artifacts
   must not imply that a commercial benchmark service is part of the public
   repo.

## Evidence Classification and Claim Boundaries

The evidence-class system marks the boundary between self-serve and controlled
data. Report claim levels then gate what a whole report is allowed to assert.

| Evidence Class | Layer | Meaning |
|---------------|-------|---------|
| `local-smoke` | Public / Community | Quick functional check, no load measurement |
| `local-lab` | Public / Community | Local benchmark on shared host, useful for regression |
| `external-reference-local` | Public / Community | External tool (h2load) on shared host, useful for comparison |
| `isolated-host` | Internal / Private | Controlled environment, single-tenant host |
| `publishable` | Internal / Private | Attested, verified, retained, reproducible |

Public/community runs produce `local-smoke`, `local-lab`, and
`external-reference-local` evidence. Controlled benchmark claims belong to
the internal/private workflow and must not be fabricated by the public repo.

## Related Documents

- [Measurement Model](../architecture/measurement-model.md) - how execution profile and provenance relate to measurements
- [Report Model](../architecture/report-model.md) - how claim levels and publishable gating describe results
- [Artifact Model](../architecture/artifact-model.md) - how artifact paths and preservation support auditability
- [Vision](vision.md) - higher-level project intent and protocol boundaries
- [Architecture Overview](../architecture/overview.md) - current component map and implementation coverage
