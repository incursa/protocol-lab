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

The profile-level keys `http1`, `http2`, `http3`, and `quic` are
protocol-specific intensity settings. The same profile ID can be used by
multiple protocol suites when the scenario behavior remains unchanged and only
the load shape changes.

The load-profile v1 schema still recognizes the legacy `http` key for HTTP/1
compatibility. New public definitions should use `http1`.
