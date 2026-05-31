# ProtocolLab - Vision

**Status:** Current (public/community canonical; internal/private extensions live in the sibling repo)

## Purpose

ProtocolLab is a scenario-driven validation, benchmarking, and diagnostic lab
for modern transport protocols including QUIC, HTTP/3, WebTransport, MASQUE,
and related extensions. It connects functional validation with repeatable
performance measurement so that protocol implementors and operators can
understand correctness, efficiency, scalability, and comparative behavior.

The public repository is intended for self-serve local validation and
measurement. It does not claim official certification, industry-standard
status, or verified benchmark authority.

The lab is designed to support:

- **Local developer validation** - a single engineer running `validate` and
  `run` on a workstation.
- **Docker and container execution** - targets and load tools running in
  isolated containers with resource controls and metrics capture.
- **CI execution** - automated pipeline invocation producing deterministic
  artifact bundles.
- **Internal/private hosted execution** - controlled runs with retained
  artifacts, provenance, and environment controls in the sibling internal
  repository.

## Core Principles

1. **The runner is implementation-neutral.** Targets are described through
   manifests, processes, containers, ports, environment variables, and
   artifact contracts. The runner does not reference protocol implementations
   directly.
2. **Public contracts are authored here first.** The internal repository
   consumes public contracts instead of re-stating or redefining them.
3. **Validation and benchmarking are related but separate concerns.**
   Validation proves correctness. Benchmarking measures performance.
   Benchmark data is accepted only after validation passes.
4. **Unsupported scenarios are explicit outcomes**, not silent skips.
5. **Raw artifacts are preserved even when parsing fails.** Parsed metrics are
   best-effort and clearly marked.
6. **Execution profile, effective load shape, and claim level are separate
   concepts.** A result must not collapse them into one field.
7. **Public/community results must not fabricate controlled or publishable
   provenance.** Claim gating is part of the model, not a documentation note.

## Workload Families

| Family | Status | Description |
|--------|--------|-------------|
| `http.application` | Implemented | HTTP request/response benchmarks across HTTP/1.1, HTTP/2, HTTP/3 |
| `h3.protocol` | Modeled | HTTP/3-specific protocol behavior (QPACK, cancellation, multiplexing). Load generation remains deferred |
| `quic.transport` | Implemented - fixture only | Raw QUIC transport behavior and fixture-only adapter coverage |
| `webtransport` | Modeled | WebTransport sessions. Validators and load generators remain deferred |
| `masque` | Modeled | MASQUE CONNECT-UDP tunnels. Validators and load generators remain deferred |

## Execution Environments (Current and Future)

**Implemented:**
- Local process execution (targets start as child processes on the host)
- Docker target execution with published-port and shared-network networking
- Docker load-tool execution (repo-owned h2load image)
- Managed-lab HTTP/3 load generation (in-process HttpClient)
- Optional `dotnet-counters` runtime diagnostics

**Future or internal/private:**
- Docker Compose or orchestrated multi-container topologies
- CI execution profile with private retention policy choices
- Hosted execution backend (controlled environment, attested provenance)
- Bare-metal and LXC backends
- Network impairment through `docker-tc` and `ns3-simulator`

## Validation vs Benchmarking

Validation proves that a target is acceptable for a scenario - endpoint
behavior, protocol negotiation, status codes, headers, and body content.
Benchmarking measures load performance after validation passes.

Protocol proof (for example, exact HTTP/3 negotiation without fallback) is
part of validation, not benchmarking. A target that validates over HTTP/3 may
still have no compatible H3 load generator; this is an explicit unsupported or
unavailable outcome, not a silent skip.

See [Validation vs Benchmarking](../spec/validation-vs-benchmarking.md) for
the detailed separation rules.

## Reporting and Provenance

Reports distinguish local/self-run results from controlled/attested runs
through evidence classification, comparability status, execution profile, and
claim level. Local results from a shared-host environment are useful for
regression and profiling but are not automatically publishable benchmark
evidence.

The public repo now records requested load shape, effective load shape, and
report claim level separately. That makes it explicit when a run is a local
measurement versus when it is trying to support a controlled claim.

## Relationship to Incursa

ProtocolLab is a standalone project. It does not require Incursa protocol
assemblies to build or run. Incursa HTTP/3 and Incursa QUIC remain canonical
targets - their manifests, containers, and benchmarks are first-class - but
the runner treats them through the same generic contracts as Kestrel, Caddy,
nginx, and any future implementation.

The public repo remains the community-facing surface. The sibling internal
repo carries hosted or commercial extensions, private diagnostics, and
extended operational workflows. Those are separate layers and are not implied
by this repository or its local results.

## Product Boundaries

The public-canonical surface of ProtocolLab is designed to be useful on its
own for local validation, local benchmarking, CI automation, and self-serve
comparisons. The private/internal layer adds hosted execution, attested
provenance, retained artifact archives, extended scenarios, private CI
integration, dashboards, and diagnostic analysis without removing or
degrading the public-canonical surface.

See [Product Boundaries](product-boundaries.md).
