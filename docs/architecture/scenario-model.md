# Architecture - Scenario Model

**Status:** Implemented (HTTP core scenarios complete; H3 protocol, QUIC fixture-only, WebTransport, and MASQUE remain modeled with execution deferred)

## Purpose

Scenarios define what is being tested. Each scenario specifies a protocol
behavior or application-level action that a target implementation must satisfy
for validation and - if applicable - can be measured under load for
benchmarking.

## Assembly

- **Project:** `src/Incursa.ProtocolLab.Model`
- **Namespace:** `Incursa.ProtocolLab.Model`
- **Key types:** `ScenarioDefinition`, `HttpEndpointSpec`, `H3ProtocolSpec`,
  `QuicTransportSpec`, `WebTransportSpec`, `MasqueSpec`,
  `BenchmarkLoadShape`, `ValidationRules`, `ScenarioMatrix`

## Scenario Definition

A scenario (`ScenarioDefinition`) is string-backed in the current model. It carries:

| Field | Type | Purpose |
|-------|------|---------|
| `SchemaVersion` | string | Scenario schema version |
| `Id` | string | Stable dotted identifier (e.g., `http.core.plaintext`) |
| `Title` | string | Human-readable title |
| `Name` | string | Alternate human-readable name |
| `Family` | string | Workload family (`http.application`, `h3.protocol`, etc.) |
| `Version` | string | Scenario version label |
| `Description` | string | What this scenario tests |
| `Status` | string | `draft`, `stable`, `experimental`, `deprecated`, `placeholder` |
| `Kind` | string | `workload`, `protocolValidation`, `interopValidation`, `diagnostic`, `profile` |
| `Layer` | string | `application`, `protocol`, `transport` |
| `Protocol` | string | Expected protocol |
| `ImplementationRole` | string | Implementation role (`server` or `client`) |
| `Roles` | string[] | Required roles |
| `RequiredCapabilities` | string[] | Capability requirements |
| `Requires` | object | Capability, protocol, and role requirements |
| `TrafficShape` | string | Expected traffic pattern |
| `Endpoint` | object | HTTP method, path, expected status, headers, body (for HTTP scenarios) |
| `H3Protocol` | object | H3-specific: QPACK, cancellation, multiplexing behavior |
| `QuicTransport` | object | Raw QUIC: connections, streams, payload, datagrams |
| `WebTransport` | object | WebTransport session configuration |
| `Masque` | object | MASQUE tunnel configuration |
| `Validation` | object | Required validation checks |
| `Benchmark` | object | Connections, streams, warmup, duration, repetitions |
| `BenchmarkCompat` | object | Compatibility and metric hints |
| `Artifacts` | object | Expected artifact types |
| `Comparability` | object | Comparability rules |
| `NetworkProfile` | string | Required network profile |
| `RequiredMetrics` | string[] | Required metrics |
| `ArtifactRequirements` | string[] | Required artifact types |
| `Tags` | string[] | Search/filter tags |

The current model stores the compatibility and behavior data as nested
records rather than as a big enum surface.

Protocol values are normalized through `ProtocolIds`. The current canonical
ids are `h1`, `h2`, `h3`, `quic`, `webtransport`, and `masque`. Common HTTP
aliases such as `http1`, `http2`, and `http3` are accepted and normalized to
the short ids.

## Workload Families

| Family | Key | Protocol Layer | Status |
|--------|-----|---------------|--------|
| HTTP Application | `http.application` | Application | Implemented |
| H3 Protocol | `h3.protocol` | Application (H3-specific) | Modeled |
| QUIC Transport | `quic.transport` | Transport | Implemented - fixture only |
| WebTransport | `webtransport` | Application | Modeled |
| MASQUE | `masque` | Application | Modeled |

### HTTP Application Scenarios

HTTP scenarios use endpoint-oriented fields. An `HttpEndpointSpec` defines:

- Method, path, query parameters
- Request headers and body generation rules
- Expected status code, content type, response headers
- Expected body rule (exact match, JSON equivalence, size check)

Current inventory (under `scenarios/http/`):
- `core/` - plaintext, json, status, bytes
- `payload/` - varied response sizes
- `headers/` - header response scenarios
- `upload/` - request body upload scenarios

The current canonical scenario ids here remain the `http.core.*` and related
names, not implementation names.

### H3 Protocol Scenarios (Modeled)

`H3ProtocolSpec` models H3-specific behavior but validators and load
generators are deferred:

- QPACK encoding/decoding behavior
- Stream cancellation and reset
- Multiplexing and stream concurrency
- H3 frame-level behavior

### QUIC Transport Scenarios (Implemented - fixture only)

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

### Placeholder Families

The catalog can also carry placeholder families such as WebSocket when a
scenario needs to exist before the protocol is fully modeled. Those entries
remain explicit placeholders and do not imply a canonical protocol id.

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
one set of benchmark results. `ExecutionProfile` is recorded on the cell and
feeds artifact identity, but it is not currently a matrix expansion dimension.

### MatrixOptions

Filtering options for matrix expansion:

- Specific implementation ids
- Specific scenario ids
- Protocol filter
- Connection count range
- Stream count range
- Repetition count
- Network profile filter
- Load tool filter
- Load profile (preset defaults)

## Validation Rules

`ValidationRules` specify required checks for a scenario:

- `requireEndpointValidation` - validate HTTP endpoint status, headers, body
- `requireProtocolProof` - prove protocol negotiation (e.g., exact H3)
- `requireStreamValidation` - validate stream-level behavior
- `requireConnectivity` - verify basic reachability

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

- [Runner Model](runner-model.md) - how scenarios are executed
- [Load Model](load-model.md) - how benchmark load shapes work
- [Architecture Overview](overview.md)
