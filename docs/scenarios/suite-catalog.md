# Suite Catalog

The public suite catalog is the set of YAML files under
[`suites/`](../../suites/). Suites group existing scenarios for a result
intent. They do not pin packages, hashes, implementations, test executors, or
runner behavior.

## Root Suites

| Suite ID | Protocol | Purpose | Result Kind | Load Profile | Scenario Scope |
| --- | --- | --- | --- | --- | --- |
| `http1-core-smoke` | `h1` | conformance | conformance | `smoke` | HTTP/1 core and 1KB payload |
| `http1-conformance-smoke` | `h1` | conformance | conformance | `smoke` | HTTP/1 conformance selector |
| `http1-benchmark-smoke` | `h1` | benchmark | benchmark | `smoke` | HTTP/1 benchmark selector |
| `http2-core-smoke` | `h2` | conformance | conformance | `smoke` | HTTP/2 core and streaming response |
| `http3-core-smoke` | `h3` | conformance | conformance | `smoke` | HTTP/3 application smoke |
| `http3-protocol-diagnostic` | `h3` | diagnostic | diagnostic | `diagnostic` | HTTP/3 protocol diagnostics |
| `quic-transport-smoke` | `quic` | conformance | conformance | `smoke` | QUIC transport smoke |
| `quic-transport-diagnostic` | `quic` | diagnostic | diagnostic | `diagnostic` | QUIC transport diagnostics |

## Intent Rules

Conformance suites select behavior validation. Benchmark suites select
performance-result intent but do not make a run publishable by themselves.
Diagnostic suites may require richer protocol evidence and can include
higher-overhead observations. Regression and soak suites may be added when a
stable scenario selection needs those result intents.

Package fixture suites under
[`fixtures/public-contracts/packages/`](../../fixtures/public-contracts/packages/)
are package-local examples. They must reference scenario IDs supplied by that
package fixture, not root catalog scenario IDs unless the package explicitly
provides them.
