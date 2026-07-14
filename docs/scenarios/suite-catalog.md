---
title: "Suite Catalog"
---

# Suite Catalog

The public suite catalog is the set of YAML files under
[`suites/`](../../suites/). Suites group existing scenarios for a result
intent. They do not pin packages, hashes, implementations, test executors, or
runner behavior.

## Root Suites

The root catalog includes two suite groups:

- compatibility suites that use generic profile IDs such as `smoke` or
  `diagnostic`
- protocol-specific profile suites that use IDs such as `http1-smoke`,
  `http2-regression`, `http3-comparison`, or `quic-soak`

| Protocol | Compatibility Suites | Protocol-Specific Profile Suites |
| --- | --- | --- |
| HTTP/1 | `http1-core-smoke`, `http1-conformance-smoke`, `http1-benchmark-smoke` | `http1-performance-smoke`, `http1-smoke-conformance`, `http1-diagnostic-suite`, `http1-regression-suite`, `http1-comparison-benchmark`, `http1-soak-suite` |
| HTTP/2 | `http2-core-smoke` | `http2-performance-smoke`, `http2-smoke-conformance`, `http2-diagnostic-suite`, `http2-regression-suite`, `http2-comparison-benchmark`, `http2-soak-suite` |
| HTTP/3 | `http3-core-smoke`, `http3-protocol-diagnostic`, `http3-peer-characterization` | `http3-smoke-conformance`, `http3-diagnostic-suite`, `http3-regression-suite`, `http3-comparison-benchmark`, `http3-soak-suite` |
| QUIC | `quic-transport-smoke`, `quic-transport-diagnostic` | `quic-smoke-conformance`, `quic-diagnostic-suite`, `quic-regression-suite`, `quic-comparison-benchmark`, `quic-soak-suite` |
| TLS | `tls-performance-smoke`, `tls-contract-breadth-smoke`, `tls-security-diagnostics` | `tls-performance-comparison` |
| gRPC/H2 | `grpc-h2-performance-smoke`, `grpc-h2-contract-breadth-smoke`, `grpc-h2-terminal-outcomes-diagnostic`, `grpc-h2-new-channel-diagnostic` | `grpc-h2-performance-comparison` |
| Classic DNS diagnostics | `dns-classic-calibration-diagnostic-smoke` | None |
| DoT | `dns-dot-performance-smoke` | `dns-dot-performance-comparison` |
| DoH2 | `dns-doh2-performance-smoke` | `dns-doh2-performance-comparison` |
| DoH3 | `dns-doh3-performance-smoke` | `dns-doh3-performance-comparison` |
| DoH3 semantics | `dns-doh3-semantics-diagnostic-smoke` | None |
| DoQ | `dns-doq-performance-smoke` | `dns-doq-performance-comparison` |
| WebSocket/H1 cleartext | `http1-websocket-cleartext-performance-smoke` | `http1-websocket-cleartext-performance-comparison` |
| WebSocket/H1 TLS | `http1-websocket-tls-performance-smoke` | `http1-websocket-tls-performance-comparison` |
| WebSocket/H2 | `http2-websocket-performance-smoke` | `http2-websocket-performance-comparison` |
| WebSocket/H3 | `http3-websocket-performance-smoke` | `http3-websocket-performance-comparison` |

## Intent Rules

Conformance suites select behavior validation. Benchmark suites select
performance-result intent but do not make a run publishable by themselves.
Diagnostic suites may require richer protocol evidence and can include
higher-overhead observations. Regression and soak suites select the same
public scenario contracts with different load intent and evidence shape.
The `http3-peer-characterization` suite is diagnostic external-peer evidence:
it can make package-backed peer rows visible in reports, but it does not create
an official payload benchmark ranking.

All new comparison suites are candidates only. Their load profiles set
`publishable: false`, and each exact protocol binding remains a separate
comparison group. MASQUE has no root suite because no approved deterministic
tunnel fixture, executor, and package-backed target role exists.

Package fixture suites under
[`fixtures/public-contracts/packages/`](../../fixtures/public-contracts/packages/)
are package-local examples. They must reference scenario IDs supplied by that
package fixture, not root catalog scenario IDs unless the package explicitly
provides them.
