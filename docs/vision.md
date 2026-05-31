# Incursa Protocol Lab Vision

`incursa/protocol-lab` is a standalone protocol benchmarking and validation harness for modern internet transports and application protocols.

Interoperability tests prove that protocol implementations can work together. They do not show how efficient, scalable, or operationally serious an implementation is. This project exists to connect functional validation with repeatable performance measurement.

The harness should eventually answer:

- How expensive is the HTTP/3 layer?
- How good is the QUIC transport?
- How does Incursa compare with Kestrel/MsQuic, nginx, Caddy, quic-go, and future implementations?
- How does performance change under concurrency, latency, loss, uploads, downloads, headers, cancellation, and stream multiplexing?

## Core Principles

- The runner is implementation-neutral.
- Incursa HTTP/3 and Incursa QUIC are preferred canonical target implementations.
- Incursa protocol code must not be embedded in the neutral runner.
- Implementations are described through manifests, containers, commands, ports, environment variables, and artifacts.
- Validation and benchmarking are related but separate concerns.
- Benchmark results are accepted only after scenario validation passes.
- Unsupported scenarios are explicit outcomes, not silent skips.
- Raw benchmark artifacts are preserved even when parsing fails.

## Workload Families

- `http.application`: HTTP-style request/response application benchmarks across HTTP/1.1, HTTP/2, and HTTP/3 where supported.
- `h3.protocol`: HTTP/3-specific protocol behavior such as QPACK, cancellation, stream behavior, H3 framing, and multiplexing.
- `quic.transport`: Raw QUIC transport behavior such as connection setup, stream throughput, stream concurrency, flow control, datagrams, loss recovery, RTT behavior, and future connection migration.
- `webtransport`: Future family. Documentation and model extensibility only until explicitly scheduled.
- `masque`: Future family. Documentation and model extensibility only until explicitly scheduled.

## First Useful Outcome

The first implementation phase should prove the end-to-end shape with the smallest useful HTTP slice:

- scenario and manifest parsing
- matrix expansion
- validation-before-benchmark control flow
- deterministic artifact paths
- JSON result files
- markdown summaries
- a Kestrel baseline server for `/plaintext` and `/json`
- focused unit tests

Raw QUIC, Incursa integration, additional servers, network impairment, WebTransport, MASQUE, and database workloads are intentionally deferred.
