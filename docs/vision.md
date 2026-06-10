# Incursa Protocol Lab Vision

`incursa/protocol-lab` is the public/community ProtocolLab repository. It is
the canonical source for shared contracts, docs, and local validation
behavior. The sibling internal repository carries private operational
extensions, hosted execution planning, and unreleased work; it should not be
needed to use the public repo.

Interoperability tests prove that protocol implementations can work together.
They do not show how efficient, scalable, or operationally serious an
implementation is. ProtocolLab exists to connect functional validation with
repeatable performance measurement without claiming official certification or
verified benchmark authority.

The harness answers a narrower question set today:

- How does an implementation behave under scenario-driven validation?
- What does a local or Docker-based measurement actually measure?
- How do results change across load shapes, execution profiles, and protocol
  families?

## Core Principles

- The runner is implementation-neutral.
- Public contracts are authored in the public repo first.
- Internal work depends on public contracts, not the other way around.
- Implementations are described through manifests, containers, commands,
  ports, environment variables, and artifacts.
- Validation and benchmarking are related but separate concerns.
- Benchmark results are accepted only after scenario validation passes.
- Unsupported scenarios are explicit outcomes, not silent skips.
- Raw benchmark artifacts are preserved even when parsing fails.
- Requested load shape, effective load shape, execution profile, and report
  claim level are distinct model concepts.
- Public/community runs must not fabricate controlled or publishable
  provenance.

## Workload Families

- `http.application`: HTTP-style request/response application benchmarks
  across HTTP/1.1, HTTP/2, and HTTP/3 where supported.
- `h3.protocol`: HTTP/3-specific protocol behavior such as QPACK, cancellation,
  stream behavior, H3 framing, and multiplexing.
- `quic.transport`: Raw QUIC transport behavior such as connection setup,
  stream throughput, stream concurrency, flow control, datagrams, loss
  recovery, RTT behavior, and future connection migration.
- `webtransport`: Modeled today; validation and load generation remain
  deferred.
- `masque`: Modeled today; validation and load generation remain deferred.

## Current Implementation Shape

The public repo currently supports:

- Adapter Contract v1 and reusable adapter conformance fixtures
- Test Executor Contract v1 and reusable test-executor conformance fixtures
- package v2 schemas and tooling for implementation, test-executor,
  scenario-pack, and toolchain packages
- neutral scenario, suite, capability, metric, artifact, endpoint, and
  provenance concepts
- local runner and load-tool catalog support for fixture/developer workflows
- raw QUIC package fixtures for the currently enabled multiplex and duplex
  transport cells
- optional runtime counters and Docker container metrics for local workflows

Hosted execution, attested provenance, and other private/internal capabilities
are handled in the sibling internal repository.
