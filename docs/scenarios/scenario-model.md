# Scenario Model

## What is a scenario?

A ProtocolLab scenario describes a **behavior** that can be validated and benchmarked. Scenarios are written in YAML and conform to the v1 scenario model. Each scenario defines what protocol behavior is expected, independent of any adapter, load tool, execution backend, or host machine.

Conceptually, that behavior is a **test case**. The scenario is the current
catalog file format for one test case. The broader execution vocabulary is
defined in
[Test Case And Run Plan Model](../architecture/test-case-run-plan-model.md).

## What a scenario is NOT

- **Not tied to an implementation.** Scenario IDs must not contain `incursa`, `kestrel`, `msquic`, `caddy`, `nginx`, `docker`, `h2load`, or similar implementation names.
- **Not a load profile.** Duration, connection count, thread count, and concurrency belong in load profiles, not scenarios.
- **Not a validation script.** Validation logic lives in adapters and validators. Scenarios declare what to check, not how.
- **Not host-specific.** Hostnames, ports, Docker details, and local paths must not appear in scenario files.
- **Not a run plan.** Package IDs, package versions, SHA-256 values,
  implementation selection, test executor selection, and controller policy
  belong in run plans or job requests.

## Model fields

### Required v1 fields

| Field | Type | Description |
|-------|------|-------------|
| `schemaVersion` | string | Schema version (e.g., `"1.0"`) |
| `id` | string | Stable dotted identifier (e.g., `http.plaintext`) |
| `title` | string | Human-readable title |
| `description` | string | Detailed scenario description |
| `status` | enum | `draft`, `stable`, `experimental`, `deprecated`, `placeholder` |
| `kind` | enum | `workload`, `protocolValidation`, `interopValidation`, `diagnostic`, `profile` |
| `layer` | enum | `application`, `protocol`, `transport` |
| `protocol` | string | Primary protocol (`h1`, `h2`, `h3`, `quic`, etc.) |
| `roles` | string[] | Required roles (e.g., `["server"]`) |
| `requires` | object | Capability, protocol, and role requirements |
| `trafficShape` | enum | Expected traffic pattern |
| `validation` | object | Validation rules and checks |

### Core enums

**ScenarioStatus:**
- `draft` — Under development
- `stable` — Validated and benchmarkable
- `experimental` — Needs explicit opt-in to run
- `deprecated` — Scheduled for removal
- `placeholder` — Not runnable (no validator/load tool exists)

**ScenarioKind:**
- `workload` — Standard request/response or upload/download workload
- `protocolValidation` — Protocol-level correctness check
- `interopValidation` — Cross-implementation compatibility
- `diagnostic` — Diagnostic or health-check scenario
- `profile` — Performance profile scenario

**ScenarioLayer:**
- `application` — HTTP-level semantics
- `protocol` — HTTP/3 framing, streams, settings
- `transport` — QUIC connection, stream, datagram semantics

**TrafficShape:**
- `requestResponse` — Classic request/response
- `upload` — Client-to-server data transfer
- `download` — Server-to-client data transfer
- `streamingDownload` — Streaming server response
- `streamingUpload` — Streaming client upload
- `bidirectionalStream` — Bidirectional stream communication
- `datagram` — Unreliable datagram exchange
- `handshakeOnly` — Connection establishment only
- `connectionLifecycle` — Full connection lifecycle test

## Scenario ID format

Scenario IDs use a stable dotted format:

```
http.plaintext
http.json
http.bytes
http3.basic-request
quic.handshake
quic.stream.echo
```

Rules:
- At least two segments separated by dots
- Lowercase only
- Implementation-neutral (no `kestrel`, `incursa`, `msquic`, `docker`, etc.)
- Stable (don't rename IDs after they're established)

## Capability matching

Each scenario declares what it needs via the `requires` field:

```yaml
requires:
  capabilities:
    - http.server
  protocols:
    - h1
    - h2
    - h3
  roles:
    - server
```

For HTTP/3-only scenarios:

```yaml
requires:
  capabilities:
    - h3.server
    - quic.server
  protocols:
    - h3
  roles:
    - server
```

The compatibility resolver matches these against adapter capabilities. Mismatched capabilities return `MissingCapability` (distinct from validation failure).

## Validation model

Validation is protocol-specific but model-driven. The validator resolved by the runner receives the scenario definition and context.

```yaml
validation:
  required: true
  checks:
    - status
    - content-type
    - body
  http:
    expectedStatus: 200
    expectedBody: Hello, World!
```

## Benchmark compatibility

Benchmark fields describe compatibility and metrics, not intensity:

```yaml
benchmarkCompat:
  compatibleLoadShapes:
    - fixed-path-request-response
  primaryMetrics:
    - requestsPerSecond
    - latencyP50
    - latencyP95
    - latencyP99
    - errorRate
```

Duration, connections, concurrency, and threads belong in load profiles, not scenarios.

## Artifact expectations

```yaml
artifacts:
  required:
    - validation.json
    - result.json
  optional:
    - qlog
    - counters
    - docker.stats.jsonl
```

Reports distinguish missing required artifacts from unavailable optional artifacts.
