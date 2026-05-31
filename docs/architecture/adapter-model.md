# Architecture — Adapter Model

**Status:** Implemented (v1 control plane contract + conformance suite + Kestrel and Incursa HTTP/3 adapters complete; Incursa Raw QUIC and MsQuic/.NET remain fixture-only; adapter discovery and broader adapter registry remain proposed)

## Purpose

Adapters provide a control plane for managing the lifecycle of protocol
endpoints under test. The adapter control plane is separate from the
protocol endpoint: the control plane manages session lifecycle (create,
prepare, start, stop, delete), and the endpoint carries actual protocol
traffic.

This separation allows the runner to manage targets that are complex to
start (multi-process, Docker, remote, future bare-metal) through a
consistent REST contract without embedding lifecycle logic in the runner.

## Assemblies

- **Contract types:** `src/Incursa.ProtocolLab.Adapter.Contracts` — `Incursa.ProtocolLab.Adapter.Contracts`
- **Conformance suite:** `src/Incursa.ProtocolLab.Adapter.Conformance` — `Incursa.ProtocolLab.Adapter.Conformance`
- **Kestrel adapter:** `src/Incursa.ProtocolLab.Adapters.Kestrel`
- **Incursa HTTP/3 adapter:** `src/Incursa.ProtocolLab.Adapters.IncursaHttp3`
- **Incursa Raw QUIC adapter:** `src/Incursa.ProtocolLab.Adapters.IncursaRawQuic`
- **MsQuic .NET adapter:** `src/Incursa.ProtocolLab.Adapters.MsQuicDotNet`

## Control Plane Contract (v1)

The adapter v1 control plane is an HTTP/1.1 JSON REST API served at
`/protocol-lab/adapter/v1`. All endpoints accept and return
`application/json`.

### Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/protocol-lab/adapter/v1/health` | Health check and identity discovery |
| GET | `/protocol-lab/adapter/v1/manifest` | Adapter capabilities and configuration |
| POST | `/protocol-lab/adapter/v1/sessions` | Create a new session |
| POST | `/protocol-lab/adapter/v1/sessions/{id}/prepare` | Prepare session (setup endpoint, certificates) |
| POST | `/protocol-lab/adapter/v1/sessions/{id}/start` | Start the prepared endpoint |
| GET | `/protocol-lab/adapter/v1/sessions/{id}/status` | Query session and endpoint status |
| GET | `/protocol-lab/adapter/v1/sessions/{id}/endpoints` | List protocol endpoints |
| GET | `/protocol-lab/adapter/v1/sessions/{id}/metrics` | Retrieve per-endpoint metrics |
| GET | `/protocol-lab/adapter/v1/sessions/{id}/artifacts` | List captured artifacts |
| DELETE | `/protocol-lab/adapter/v1/sessions/{id}` | Stop and clean up |

### Key Types

**AdapterIdentity** — name, version, description, supported protocols.

**AdapterManifestResponse** — capabilities (protocols, workload families,
features), default ports, endpoints, metrics schema, and artifact types.

**AdapterSessionCreateRequest** — scenario family, protocol, endpoint
configuration.

**AdapterStatusResponse** — session state, endpoint URLs, readiness, health,
started/stopped timestamps.

**AdapterProblemDetails** — RFC 9457 problem details for error responses.

**AdapterHealthStatus** enum — `Healthy`, `Degraded`, `Unhealthy`.

**AdapterSessionState** enum — `Created`, `Preparing`, `Prepared`,
`Starting`, `Running`, `Stopping`, `Stopped`, `Deleting`, `Failed`.

**AdapterCapabilityStatus** enum — `Supported`, `Unsupported`,
`Experimental`, `Placeholder`.

### Client

`ProtocolLabAdapterClient` is an HTTP/1.1 JSON client that implements the
full control plane surface. It handles serialization, error mapping, and
timeout management. The runner consumes adapters through this client rather
than through in-process references.

### Schemas

JSON schemas at `schemas/adapter/v1/` validate request and response payloads:
- `health.schema.json`
- `manifest.schema.json`
- `sessions.schema.json`
- `prepare.schema.json`
- `start.schema.json`
- `status.schema.json`
- `endpoints.schema.json`
- `metrics.schema.json`
- `artifacts.schema.json`
- `problem-details.schema.json`

## Adapter Conformance Suite

The conformance suite (`Incursa.ProtocolLab.Adapter.Conformance`) provides a
reusable test harness for adapter implementations. It validates:

- Health endpoint behavior
- Manifest completeness and schema conformance
- Full lifecycle flow (create → prepare → start → status → stop → delete)
- Endpoint discovery
- Metric and artifact retrieval
- Error handling and problem-details responses
- Timeout and infrastructure-failure behavior

Outcomes are distinguished: `unsupported` (capability not advertised),
`contract` (violates the schema or API contract), `malformed` (unparseable
response), `infrastructure` (network or process failure), `timeout`.

The conformance suite is the accepted pre-runner proof surface for adapter
implementations. It can verify both current adapters and future adapters
before the runner is taught to consume them.

See [`docs/runner/adapter-conformance.md`](../runner/adapter-conformance.md)
for the conformance specification.

## Implemented Adapters

### Kestrel Adapter v1

Runs as a separate HTTP/1.1 JSON process. Starts the Kestrel benchmark
server as a child endpoint process. Returns the protocol endpoint URL
(H1/H2/H3) to the runner. The adapter keeps the benchmark server lifecycle
independent of the runner process.

**Status:** Implemented

### Incursa HTTP/3 Adapter v1

Manages an Incursa HTTP/3 endpoint through the same control plane contract.
Lifecycle is managed through the adapter; protocol traffic goes to the
adapter-reported endpoint URL.

**Status:** Implemented

### Incursa Raw QUIC Adapter v1

Implemented - fixture only. The control plane is operational and exercises
the raw QUIC fixture surface; real QUIC traffic and benchmark claims remain
deferred.

**Status:** Implemented - fixture only

### MsQuic .NET Adapter v1

Implemented - fixture only. The control plane is operational and exercises
the raw QUIC fixture surface; real QUIC traffic and benchmark claims remain
deferred.

**Status:** Implemented - fixture only

## Adapter Lifecycle Flow

```
Client (Runner)              Adapter Control Plane          Protocol Endpoint
      |                              |                              |
      |--- POST /sessions ---------->|                              |
      |<-- 201 Created (session) ----|                              |
      |                              |                              |
      |--- POST /sessions/{id}/prepare ->|                          |
      |<-- 200 Prepared -------------|                              |
      |                              |--- start endpoint ---------->|
      |                              |<-- endpoint ready -----------|
      |                              |                              |
      |--- POST /sessions/{id}/start -->|                           |
      |<-- 200 Started --------------|                              |
      |                              |                              |
      |=== protocol traffic =====================================>|
      |<== protocol traffic ======================================|
      |                              |                              |
      |--- GET /sessions/{id}/status -->|                           |
      |<-- 200 { running } ----------|                              |
      |                              |                              |
      |--- GET /sessions/{id}/metrics ->|                           |
      |<-- 200 { metrics } ----------|                              |
      |                              |                              |
      |--- DELETE /sessions/{id} --->|                              |
      |                              |--- stop endpoint ----------->|
      |<-- 200 Stopped --------------|                              |
```

## Proposed Extensions

- **Adapter discovery:** A registry or discovery mechanism so the runner can
  find available adapters without hard-coded paths.
- **Bare-metal/LXC backend:** The adapter contract is designed to support
  non-Docker, non-process backends (bare metal, LXC, VM) without changing
  the control plane surface.
- **Streaming metrics:** Push-based or streaming metric delivery for
  long-running benchmark sessions.
- **Multi-endpoint sessions:** Sessions that manage multiple protocol
  endpoints (e.g., separate H3 and QUIC endpoints from a single adapter).

## Related Documents

- [Runner Model](runner-model.md) — how the runner consumes adapters
- [Load Model](load-model.md) — load tool execution
- [Architecture Overview](overview.md)
- [`docs/architecture/adapter-contract-v1.md`](adapter-contract-v1.md) — full v1 contract specification
- [`docs/runner/adapter-conformance.md`](../runner/adapter-conformance.md) — conformance suite
