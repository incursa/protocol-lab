# ProtocolLab - Vision

**Status:** Current (v1 local operational; hosted/attested scope deferred)

## Purpose

ProtocolLab is a scenario-driven validation, benchmarking, and diagnostic lab
for modern transport protocols including QUIC, HTTP/3, WebTransport, MASQUE,
DNS over QUIC, and related extensions. It connects functional validation with
repeatable performance measurement so that protocol implementors and operators
can understand correctness, efficiency, scalability, and comparative behavior.

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
- **Controlled attested runs** (proposed) - hosted execution with retained
  artifacts, provenance, and documented environment controls.

## Core Principles

1. **The runner is implementation-neutral.** Targets are described through
   manifests, processes, containers, ports, environment variables, and artifact
   contracts. The runner does not reference protocol implementations directly.
2. **Incursa HTTP/3 and Incursa QUIC are preferred canonical targets.**
   They enter through the same manifest and execution contracts as every other
   implementation.
3. **Validation and benchmarking are related but separate concerns.**
   Validation proves correctness. Benchmarking measures performance. Benchmark
   data is accepted only after validation passes.
4. **Unsupported scenarios are explicit outcomes**, not silent skips.
5. **Raw artifacts are preserved even when parsing fails.** Parsed metrics are
   best-effort and clearly marked.
6. **Every result carries an evidence classification** (local-smoke,
   local-lab, external-reference-local, isolated-host, publishable) so
   consumers can distinguish informal from controlled data.

## Workload Families

| Family | Status | Description |
|--------|--------|-------------|
| `http.application` | Implemented | HTTP request/response benchmarks across HTTP/1.1, HTTP/2, HTTP/3 |
| `h3.protocol` | Modeled | HTTP/3-specific protocol behavior (QPACK, cancellation, multiplexing). Load generation deferred |
| `quic.transport` | Modeled | Raw QUIC transport behavior (handshake, streams, datagrams, loss recovery) |
| `webtransport` | Modeled | WebTransport sessions. Validators and load generators deferred |
| `masque` | Modeled | MASQUE CONNECT-UDP tunnels. Validators and load generators deferred |

## Execution Environments (Current and Proposed)

**Implemented:**
- Local process execution (targets start as child processes on the host)
- Docker target execution with published-port and shared-network networking
- Docker load-tool execution (repo-owned h2load image)
- Managed-lab HTTP/3 load generation (in-process HttpClient)
- Optional `dotnet-counters` runtime diagnostics

**Proposed / Future:**
- Docker Compose or orchestrated multi-container topologies
- CI execution profile (pre-configured matrix + artifact retention policy)
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
through evidence classification, comparability status, and metadata capture
(host OS, Docker backend, git commit, runner PID). Local results from a
shared-host environment are useful for regression and profiling but are not
treated as publishable benchmark evidence.

**Proposed:** A future reporting layer should mark controlled-run results with
retained artifact links, environment attestations, and verification signatures
so that consumers can distinguish self-serve public-candidate data from
attested private/internal runs.

## Relationship to Incursa

ProtocolLab is a standalone project. It does not require Incursa protocol
assemblies to build or run. Incursa HTTP/3 and Incursa QUIC are canonical
targets - their manifests, containers, and benchmarks are first-class - but
the runner treats them through the same generic contracts as Kestrel, Caddy,
nginx, and any future implementation.

The public repo remains the community-facing surface. Any hosted or
commercial service that extends ProtocolLab would be a separate layer and is
not implied by this repository or its local results.

## Product Boundaries

The public-canonical surface of ProtocolLab is designed to be useful on its
own for local validation, local benchmarking, CI automation, and self-serve
comparisons. That surface remains a public-candidate area and should be
treated as subject to audit before any public release.

A private/internal layer - if pursued as `Incursa.ProtocolLab.Internal` -
would add hosted execution, attested provenance, retained artifact archives,
extended scenarios, private CI integration, dashboards, and diagnostic
analysis without removing or degrading the public-canonical surface.

See [Product Boundaries](product-boundaries.md).
