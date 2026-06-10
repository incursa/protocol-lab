# Raw QUIC Foundation

The raw QUIC foundation provides scenario shapes, adapter endpoint metadata,
fixture proofs, reference package/test-executor contracts, and the local
load-tool shape used for current runner fixture workflows.

## Current State

- Fixture-only scenarios remain under the runner contract fixture lab.
- Production raw QUIC implementations are expected to arrive as
  implementation packages from producer repositories.
- `quic-go-raw-load` is the reference raw QUIC test executor used by public
  component package fixtures. The local runner still has a load-tool catalog
  entry for fixture execution.
- Catalog raw QUIC execution is enabled only for:
  - `quic.transport.multiplex.100x64kb`
  - `quic.transport.duplex-streams`
- `quic.transport.handshake-cold`, `quic.transport.stream-throughput.1mb`,
  and `quic.transport.connection-churn` remain explicitly unsupported until
  scenario-specific validation gates are added.
- The runner accepts QUIC endpoints from adapters without adding QUIC packet
  logic or binding to a concrete QUIC implementation.

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

## Validation Gates

For enabled catalog raw QUIC scenarios, the runner first validates adapter
endpoint metadata, then runs the selected load tool, parses raw QUIC JSON, and
updates the validation result from load-tool evidence. Benchmark metrics are
accepted only when all gates pass:

- bytes sent and bytes received satisfy the scenario's expected byte shape
- completed streams satisfy the expected stream shape
- failed request count is zero
- timeout request count is zero
- raw load-tool stdout and stderr are preserved

If parsing fails or any gate fails, the cell is classified as failed and parsed
metrics are not accepted in `result.json`; the raw stdout/stderr artifacts are
still retained for diagnosis.

## Current Limitations

- Only multiplex and duplex catalog raw QUIC scenarios are enabled.
- Fixture lab raw QUIC load tools are deterministic test fixtures and do not
  generate real QUIC packets.
- qlog, SSL key log, and packet capture support are not available for raw QUIC
  cells; results record missing qlog evidence honestly.
- Datagram and 0-RTT support is declared in metadata but not exercised.
- No Docker packaging for raw QUIC targets.
- Raw QUIC comparison evidence remains local-lab evidence, not publishable
  benchmark claims.
