---
title: "Load Profile Catalog"
---

# Load Profile Catalog

The public load-profile catalog is the set of YAML files under
[`load-profiles/`](../../load-profiles/). Load profiles describe intensity,
repetition, and evidence tier. They do not require a specific load generator,
runner, programming language, process model, telemetry backend, or hosted lab.

## Profiles

| Load Profile ID | Purpose | Status | Generic Or Protocol-Specific |
| --- | --- | --- | --- |
| `smoke` | smoke | stable | Generic with HTTP/1, HTTP/2, HTTP/3, and QUIC settings |
| `diagnostic` | diagnostic | stable | Generic with protocol-diagnostic artifact expectations |
| `regression` | regression | stable | Generic with HTTP/1, HTTP/2, HTTP/3, and QUIC settings |
| `comparison` | comparison | stable | Generic non-publishable comparison profile |
| `soak` | soak | experimental | Generic long-duration stability profile |
| `http1-smoke` | smoke | stable | HTTP/1-specific |
| `http1-diagnostic` | diagnostic | stable | HTTP/1-specific |
| `http1-regression` | regression | stable | HTTP/1-specific |
| `http1-comparison` | comparison | stable | HTTP/1-specific |
| `http1-soak` | soak | experimental | HTTP/1-specific |
| `http2-smoke` | smoke | stable | HTTP/2-specific |
| `http2-diagnostic` | diagnostic | stable | HTTP/2-specific |
| `http2-regression` | regression | stable | HTTP/2-specific |
| `http2-comparison` | comparison | stable | HTTP/2-specific |
| `http2-soak` | soak | experimental | HTTP/2-specific |
| `http3-smoke` | smoke | stable | HTTP/3-specific |
| `http3-diagnostic` | diagnostic | stable | HTTP/3-specific |
| `http3-regression` | regression | stable | HTTP/3-specific |
| `http3-comparison` | comparison | stable | HTTP/3-specific |
| `http3-soak` | soak | experimental | HTTP/3-specific |
| `quic-smoke` | smoke | stable | QUIC-specific |
| `quic-diagnostic` | diagnostic | stable | QUIC-specific |
| `quic-regression` | regression | stable | QUIC-specific |
| `quic-comparison` | comparison | stable | QUIC-specific |
| `raw-quic-peer-confidence` | comparison | stable | Raw QUIC peer comparison; scenario-owned connection and stream matrices with five repetitions |
| `quic-soak` | soak | experimental | QUIC-specific |

The profile-level keys `http1`, `http2`, `http3`, and `quic` are
protocol-specific intensity settings. The same profile ID can be used by
multiple protocol suites when the scenario behavior remains unchanged and only
the load shape changes.

Generic profiles are useful when a suite can share one profile across protocol
families. Protocol-specific profiles are useful when the public selection needs
the profile ID itself to identify the protocol lane.

The load-profile v1 schema still recognizes the legacy `http` key for HTTP/1
compatibility. New public definitions should use `http1`.
