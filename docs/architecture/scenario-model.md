# Architecture — Scenario Model

**Status:** Implemented (HTTP core scenarios complete; H3 protocol, QUIC, WebTransport, and MASQUE remain modeled with execution deferred)

## Purpose

Scenarios define what is being tested. Each scenario specifies a protocol
behavior or application-level action that a target implementation must satisfy
for validation and — if applicable — can be measured under load for
benchmarking.

## Assembly

- **Project:** `src/Incursa.ProtocolLab.Model`
- **Namespace:** `Incursa.ProtocolLab.Model`
- **Key types:** `ScenarioDefinition`, `HttpEndpointSpec`, `H3ProtocolSpec`,
  `QuicTransportSpec`, `WebTransportSpec`, `MasqueSpec`,
  `BenchmarkLoadShape`, `ValidationRules`, `ScenarioMatrix`

## Scenario Definition

A scenario (`ScenarioDefinition`) carries:

| Field | Type | Purpose |
|-------|------|---------|
| Id | string | Unique identifier (e.g., `http.core.plaintext`) |
| Name | string | Human-readable name |
| Family | string | Workload family (`http.application`, `h3.protocol`, etc.) |
| Description | string | What this scenario tests |
| Kind | ScenarioKind | `Validation`, `Benchmark`, or `Both` |
| Layer | ScenarioLayer | Protocol layer (`Application`, `Transport`) |
| Protocol | string | Expected protocol (`h3`, `h2`, `h1`, `quic`) |
| Role | string | Implementation role (`server` or `client`) |
| Endpoint | HttpEndpointSpec | HTTP method, path, expected status, headers, body (for HTTP scenarios) |
| H3 | H3ProtocolSpec | H3-specific: QPACK, cancellation, multiplexing behavior |
| Quic | QuicTransportSpec | Raw QUIC: connections, streams, payload, datagrams |
| WebTransport | WebTransportSpec | WebTransport session configuration (stub) |
| Masque | MasqueSpec | MASQUE tunnel configuration (stub) |
| Validation | ValidationRules | Required validation checks |
| Benchmark | BenchmarkLoadShape | Connections, streams, warmup, duration, repetitions |
| TrafficShape | TrafficShape enum | `Unidirectional`, `Bidirectional`, `RequestResponse` |
| Requires | ScenarioRequires | Capability requirements |
| Artifacts | ScenarioArtifacts | Expected artifact types |
| Comparability | ScenarioComparability | Comparability rules |
| Tags | string[] | Search/filter tags |
| Status | ScenarioStatus | `Active`, `Draft`, `Deprecated` |

## Workload Families

| Family | Key | Protocol Layer | Status |
|--------|-----|---------------|--------|
| HTTP Application | `http.application` | Application | Implemented |
| H3 Protocol | `h3.protocol` | Application (H3-specific) | Modeled |
| QUIC Transport | `quic.transport` | Transport | Modeled |
| WebTransport | `webtransport` | Application | Modeled |
| MASQUE | `masque` | Application | Modeled |

### HTTP Application Scenarios

HTTP scenarios use endpoint-oriented fields. An `HttpEndpointSpec` defines:

- Method, path, query parameters
- Request headers and body generation rules
- Expected status code, content type, response headers
- Expected body rule (exact match, JSON equivalence, size check)

Current inventory (under `scenarios/http/`):
- `core/` — plaintext, json, status, bytes
- `payload/` — varied response sizes
- `headers/` — header response scenarios
- `upload/` — request body upload scenarios

### H3 Protocol Scenarios (Modeled)

`H3ProtocolSpec` models H3-specific behavior but validators and load
generators are deferred:

- QPACK encoding/decoding behavior
- Stream cancellation and reset
- Multiplexing and stream concurrency
- H3 frame-level behavior

### QUIC Transport Scenarios (Modeled)

`QuicTransportSpec` models raw QUIC behavior:

- Connection setup latency and throughput
- Stream types (unidirectional, bidirectional)
- Stream concurrency and churn
- Datagram behavior
- Flow control and loss recovery
- Connection migration (future)

### WebTransport and MASQUE (Modeled)

Model stubs exist (`WebTransportSpec`, `MasqueSpec`) with placeholder
scenario YAML files. Both return explicit unsupported outcomes until real
validators and load generators are implemented.

## Scenario Matrix

The scenario matrix (`ScenarioMatrix`) expands selected implementations,
scenarios, protocols, and load shapes into individual run cells.

### RunCell

A `RunCell` is the atomic unit of work:

```
RunCell = Implementation × Scenario × Protocol ×
          Connections × Streams × Repetition ×
          Duration × Warmup × NetworkProfile
```

Each cell produces one set of validation results and (if validation passes)
one set of benchmark results.

### MatrixOptions

Filtering options for matrix expansion:

- Specific implementation IDs
- Specific scenario IDs
- Protocol filter
- Connection count range
- Stream count range
- Repetition count
- Network profile filter
- Load tool filter
- Load profile (preset defaults)

## Validation Rules

`ValidationRules` specify required checks for a scenario:

- `requireEndpointValidation` — validate HTTP endpoint status, headers, body
- `requireProtocolProof` — prove protocol negotiation (e.g., exact H3)
- `requireStreamValidation` — validate stream-level behavior
- `requireConnectivity` — verify basic reachability

Rules that cannot be satisfied produce explicit unsupported outcomes.

## Scenario Support

`ScenarioSupport.Evaluate()` checks whether an implementation can satisfy a
scenario by comparing:

- Implementation role vs scenario role
- Implementation protocols vs scenario protocol
- Implementation workload families vs scenario family
- Implementation capabilities vs scenario requirements

Unsupported scenarios return a clear status with a reason.

## Related Documents

- [Runner Model](runner-model.md) — how scenarios are executed
- [Load Model](load-model.md) — how benchmark load shapes work
- [Architecture Overview](overview.md)
