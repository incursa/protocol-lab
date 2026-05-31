# Raw QUIC Foundation

The raw QUIC foundation provides the scenario shapes, adapter endpoint metadata,
fixture proofs, and load-tool scaffolding needed before real raw QUIC adapters
and load generators are implemented.

## Current State

- Fixture-only: all raw QUIC scenarios are proven through deterministic fixture
  adapter endpoints and fixture load tools.
- No real QUIC traffic is generated or validated.
- No Incursa or MSQuic raw QUIC adapter exists yet.
- The runner accepts QUIC endpoints from adapters without adding QUIC packet
  logic.

## Scenario Shapes

Raw QUIC transport scenarios live in `scenarios/quic/transport/` with the
`quic.transport` family. Fixture-only scenarios for the adapter-backed path
live in the runner contract fixture lab under
`tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/scenarios/` with
the `fixture.quic` family.

### Handshake

| Field | Value |
|---|---|
| Scenario | `fixture.quic.handshake` |
| Behavior | `handshake-fixture` |
| Connections | 1 |
| Streams | 0 |
| Payload | None |

Proves the adapter can return a QUIC endpoint and the runner can route the
scenario without HTTP assumptions.

### Bidirectional Stream Echo

| Field | Value |
|---|---|
| Scenario | `fixture.quic.bidirectional-echo` |
| Behavior | `bidirectional-echo-fixture` |
| Connections | 1 |
| Stream Type | bidirectional |
| Streams | 1 |
| Payload | 1024 bytes each direction |

Proves bidirectional stream semantics through the adapter path.

### Bidirectional Bulk Send

| Field | Value |
|---|---|
| Scenario | `fixture.quic.bidirectional-bulk` |
| Behavior | `bidirectional-bulk-fixture` |
| Connections | 1 |
| Stream Type | bidirectional |
| Streams | 16 concurrent |
| Payload | 65536 bytes each direction |

Proves concurrent stream throughput routing.

### Unidirectional Stream Send

| Field | Value |
|---|---|
| Scenario | `fixture.quic.unidirectional-send` |
| Behavior | `unidirectional-send-fixture` |
| Connections | 1 |
| Stream Type | unidirectional |
| Streams | 4 sequential |
| Payload | 4096 bytes |

Proves unidirectional stream support through the adapter path.

### Unsupported Feature

| Field | Value |
|---|---|
| Scenario | `fixture.quic.unsupported` |
| Behavior | `datagram-unsupported` |
| Required Capability | `quicDatagram` |

Proves the adapter reports unsupported raw QUIC features structurally.

## Endpoint Discovery Metadata

When the adapter contract returns a QUIC/UDP protocol endpoint, the
`AdapterEndpoint` object includes the following metadata:

| Field | Description | Example |
|---|---|---|
| `Scheme` | Protocol scheme | `"quic"` |
| `Protocol` | Protocol identifier | `"quic"` |
| `Host` | Endpoint host | `"127.0.0.1"` |
| `Port` | Endpoint port | `4433` |
| `NetworkMode` | Network accessibility | `"process-local"` |
| `BindMode` | Bind scope | `"loopback"` |
| `Tls.CertificateMode` | TLS certificate mode | `"fixture-self-signed"` |
| `Tls.Sni` | Server name indication | `"fixture-quic"` |

The `Extensions` bag carries additional QUIC-specific metadata:

| Extension Key | Type | Description |
|---|---|---|
| `alpn` | `string[]` | ALPN protocol identifiers |
| `sni` | `string` | Server name / SNI |
| `streamBehavior` | `string` | Default stream behavior |
| `supportedStreamDirections` | `string[]` | Supported stream directions |
| `datagramSupported` | `bool` | Optional datagram support flag |
| `zeroRttSupported` | `bool` | Optional 0-RTT support flag |
| `transport` | `string` | Transport protocol (e.g. `"udp"`) |

## Current Limitations

- No real QUIC traffic is generated or validated.
- No Incursa or MSQuic raw QUIC adapter exists.
- Raw QUIC load tools are fixture-only (deterministic, no actual QUIC packets).
- QUIC scenario validation is fixture-only (returns "passed" for any QUIC
  endpoint from a ready adapter session).
- No qlog, SSL key log, or packet capture support.
- Datagram and 0-RTT support is declared in metadata but not exercised.
- No Docker packaging for raw QUIC targets.
- Raw QUIC benchmarking claims are not implemented.
