# ProtocolLab - Product Boundaries

**Status:** Current (conceptual public/community boundary model; no hosted/commercial split implemented)

## Overview

ProtocolLab is designed with a layered architecture that separates a
public/community canonical surface from a potential private/internal/commercial
extension layer. This document defines the conceptual split. No code-level
public/internal repo split exists yet; this is captured here for design
direction.

## Layer Model

```
+--------------------------------------------------+
| Private / Internal / Commercial Layer (proposed) |
| Hosted execution, attested provenance,           |
| retained artifacts, extended scenarios,          |
| dashboards, private CI, diagnostic analysis      |
+--------------------------------------------------+
| Public / Canonical Layer (public/community)      |
| Local validation, local benchmarking,            |
| Docker execution, CI automation,                 |
| self-serve comparisons, open artifact format     |
+--------------------------------------------------+
```

## Public / Canonical Layer

The public-canonical layer is the public/community surface of ProtocolLab.
It is designed to be complete and useful on its own after audit.

**Implemented capabilities:**
- Local process and Docker target execution
- Local validation with HTTP/3 protocol proof
- Local benchmarking with h2load, oha, and managed HttpClient load tools
- Deterministic artifact layout under `.artifacts/runs/{runId}`
- Evidence classification and comparability gates
- Markdown summaries and aggregate JSON reports
- Adapter control plane contract for external lifecycle management
- Suite definitions for repeatable run configurations

**Proposed future public-canonical capabilities:**
- CI execution profile (pipeline-friendly invocation)
- Network impairment through open-source providers (docker-tc)
- Additional public-canonical targets (quic-go, open-source QUIC stacks)
- WebTransport and MASQUE family execution
- Public result aggregation and trend analysis (self-serve)

**Public-canonical layer invariant:** Every capability in the public-canonical
layer must work without a private/internal backend, authentication, or hosted
service dependency. Public-canonical results are marked as local evidence and
never imply attested provenance. If a commercial hosted benchmark service is
ever introduced, it must live outside the public repo and must not be implied
by public/community outputs.

## Private / Internal / Commercial Layer (Proposed)

The private/internal layer is a conceptual extension that would add
capabilities relevant to organizations needing controlled, attested, and
retained protocol benchmark data. It is not implemented and no commitment is
made to build it in this repository.

**Proposed private/internal capabilities:**
- **Hosted execution:** ProtocolLab runs in a controlled, attested
  environment with known hardware, OS, and resource configuration.
- **Attested provenance:** Results carry environment attestations,
  verification signatures, and artifact retention guarantees so that
  consumers can distinguish verified runs from self-serve data.
- **Retained artifacts:** Long-term storage of raw load-tool output, server
  logs, pcaps, qlogs, SSL key logs, and runtime diagnostics with audit
  trails.
- **Extended scenarios:** Proprietary or specialized protocol scenarios that
  are not part of the public catalog.
- **Dashboards:** Web-based result browsing with trend analysis, regression
  detection, and comparative visualization across implementations and
  releases.
- **Private CI:** Integration with private artifact stores, private
  container registries, and private/internal CI systems.
- **Diagnostic analysis:** Deep protocol trace analysis, custom metric
  extraction, and implementation-specific performance profiling.

**Private/internal layer invariant:** The private/internal layer extends the
public-canonical layer without removing or degrading it. Public users should
never be blocked from capabilities because a private/internal feature exists.

## Boundary Enforcement

The following constraints ensure the public-canonical layer remains
independent:

1. **No private/internal-only code paths in the runner.** The runner must
   not contain conditional logic that branches on whether a private/internal
   backend is present. Private/internal extensions enter through plugin,
   adapter, or configuration boundaries.
2. **No private/internal secrets in the public repo.** Connection strings, API
   keys, service URLs, and authentication tokens must never appear in the
   public repository.
3. **No fabricated provenance.** Public results must not carry attestations
   that suggest controlled-run provenance.
4. **Separate configuration.** Private/internal execution configuration (host
   endpoints, artifact retention policies, auth) lives in a separate
   configuration layer that is not part of the public CLI surface.
5. **Contract-first integration.** If the private/internal layer adds hosted
   execution, it does so through documented contracts (REST API, artifact
   format, evidence schema) that the public runner already supports.
6. **No implied commercial service.** Public documentation, templates, and
   result artifacts must not imply that a hosted or commercial benchmark
   service is part of the public repo.

## Repository Split (Future Consideration)

If the private/internal layer is pursued, the repository may be split into:

- `Incursa.ProtocolLab` (public/canonical)
- `Incursa.ProtocolLab.Internal` (private/internal)

No split exists today and none is planned until the private/internal layer has
a concrete implementation target. If a split occurs, the public/canonical repo
must continue to build, test, and run without the private/internal repo
present.

## Relationship to Project Structure

The current repository contains public-candidate areas subject to audit:

- Runner orchestration (`src/Incursa.ProtocolLab.Runner`)
- Model types (`src/Incursa.ProtocolLab.Model`)
- CLI host (`src/Incursa.ProtocolLab.Cli`)
- Adapter contracts and conformance (`src/Incursa.ProtocolLab.Adapter.*`)
- Adapter implementations (`src/Incursa.ProtocolLab.Adapters.*`)
- Server implementations (`servers/`)
- Scenarios, manifests, load tools, load profiles, suites
- Scripts, schemas, specs, and tests

These areas are candidates for the public-canonical surface. If a
private/internal layer is added, it would live outside this repository and
consume the public surface through its documented contracts.

## Evidence Classification and Boundaries

The public-canonical layer's evidence classification system already encodes
the boundary between self-serve and controlled data:

| Evidence Class | Layer | Meaning |
|---------------|-------|---------|
| `local-smoke` | Public / Canonical | Quick functional check, no load measurement |
| `local-lab` | Public / Canonical | Local benchmark on shared host, useful for regression |
| `external-reference-local` | Public / Canonical | External tool (h2load) on shared host, useful for comparison |
| `isolated-host` | Private / Internal / Commercial | Controlled environment, single-tenant host |
| `publishable` | Private / Internal / Commercial | Attested, verified, retained, reproducible |

The public-canonical layer produces `local-smoke`, `local-lab`, and
`external-reference-local` results. `isolated-host` and `publishable` are
conceptual targets for the private/internal layer and are not claimed by any
current implementation.

## Related Documents

- [Measurement Model](../architecture/measurement-model.md) - how provenance and collectors separate trust levels
- [Report Model](../architecture/report-model.md) - how claim levels and environment manifests describe results
- [Artifact Model](../architecture/artifact-model.md) - how artifact paths and preservation support auditability
- [Vision](vision.md) - higher-level project intent and protocol boundaries
- [Architecture Overview](../architecture/overview.md) - current component map and implementation coverage
