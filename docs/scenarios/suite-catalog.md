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
| HTTP/1 | `http1-core-smoke`, `http1-conformance-smoke`, `http1-benchmark-smoke` | `http1-smoke-conformance`, `http1-diagnostic-suite`, `http1-regression-suite`, `http1-comparison-benchmark`, `http1-soak-suite` |
| HTTP/2 | `http2-core-smoke` | `http2-smoke-conformance`, `http2-diagnostic-suite`, `http2-regression-suite`, `http2-comparison-benchmark`, `http2-soak-suite` |
| HTTP/3 | `http3-core-smoke`, `http3-protocol-diagnostic` | `http3-smoke-conformance`, `http3-diagnostic-suite`, `http3-regression-suite`, `http3-comparison-benchmark`, `http3-soak-suite` |
| QUIC | `quic-transport-smoke`, `quic-transport-diagnostic` | `quic-smoke-conformance`, `quic-diagnostic-suite`, `quic-regression-suite`, `quic-comparison-benchmark`, `quic-soak-suite` |

## Intent Rules

Conformance suites select behavior validation. Benchmark suites select
performance-result intent but do not make a run publishable by themselves.
Diagnostic suites may require richer protocol evidence and can include
higher-overhead observations. Regression and soak suites select the same
public scenario contracts with different load intent and evidence shape.

Package fixture suites under
[`fixtures/public-contracts/packages/`](../../fixtures/public-contracts/packages/)
are package-local examples. They must reference scenario IDs supplied by that
package fixture, not root catalog scenario IDs unless the package explicitly
provides them.
