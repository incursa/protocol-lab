# MSQuic/.NET Raw QUIC Adapter v1

`MSQuic/.NET Raw QUIC Adapter v1` is the first real raw QUIC ProtocolLab
adapter. It exposes the ProtocolLab Adapter Contract v1 control plane over
HTTP/1.1 JSON and hosts an in-process raw QUIC server using `System.Net.Quic`.

The adapter is not the protocol endpoint under test:

- the adapter control plane listens on a local HTTP URL such as
  `http://127.0.0.1:53381`
- the adapter hosts a raw QUIC/UDP server on a separate port using
  `System.Net.Quic.QuicListener`
- runner validation and load generation must use the returned QUIC/UDP
  protocol endpoint URL, not the control-plane URL

## Prerequisites

The adapter requires a platform with `System.Net.Quic` support:

- **Windows 11+** (or Windows Server 2022+): msquic is included in the OS
- **Linux**: requires `libmsquic` package installation
- **macOS**: requires `libmsquic` via Homebrew

The adapter checks `QuicListener.IsSupported` at runtime and reports
`Degraded` health with an unsupported message when QUIC is unavailable.

## Local run

Start the adapter control plane directly:

```powershell
$env:ASPNETCORE_URLS = "http://127.0.0.1:53381"
dotnet run --project src\Incursa.ProtocolLab.Adapters.MsQuicDotNet\Incursa.ProtocolLab.Adapters.MsQuicDotNet.csproj --no-launch-profile
```

Optional environment variables for customization:

- `PROTOCOL_LAB_MSQUIC_PORT` - fixed QUIC listen port (default: random free port)
- `PROTOCOL_LAB_MSQUIC_ALPN` - ALPN protocol identifier (default: `plab-raw-quic`)
- `PROTOCOL_LAB_MSQUIC_CERT_SUBJECT` - self-signed certificate subject
- `PROTOCOL_LAB_MSQUIC_START_TIMEOUT_SECONDS`
- `PROTOCOL_LAB_MSQUIC_READINESS_TIMEOUT_SECONDS`
- `PROTOCOL_LAB_MSQUIC_HTTP_TIMEOUT_SECONDS`

The control plane exposes these routes under `/protocol-lab/adapter/v1`:

- `GET /health`
- `GET /manifest`
- `POST /sessions`
- `POST /sessions/{sessionId}/prepare`
- `POST /sessions/{sessionId}/start`
- `GET /sessions/{sessionId}/status`
- `GET /sessions/{sessionId}/endpoints`
- `GET /sessions/{sessionId}/metrics`
- `GET /sessions/{sessionId}/artifacts`
- `POST /sessions/{sessionId}/stop`
- `DELETE /sessions/{sessionId}`

## Supported capabilities

- `quic.server` - Raw QUIC server endpoint using System.Net.Quic with MSQuic
- `quicTransport` - Raw QUIC transport support
- `quicHandshake` - QUIC connection handshake support
- `quicStreams` - QUIC bidirectional stream support

## Supported scenarios

### Handshake

| Property | Value |
|---|---|
| Scenario | `fixture.quic.handshake` |
| Behavior | Server accepts QUIC connection, handshake completes |
| Connections | 1 |
| Streams | None |

### Bidirectional Stream Echo

| Property | Value |
|---|---|
| Scenario | `fixture.quic.bidirectional-echo` |
| Behavior | Server reads from inbound stream, echoes data back |
| Connections | 1 |
| Streams | 1 bidirectional |

### Bidirectional Stream Bulk

| Property | Value |
|---|---|
| Scenario | `fixture.quic.bidirectional-bulk` |
| Behavior | Server accepts multiple concurrent streams, echoes bulk data |
| Connections | 1 |
| Streams | Up to 16 concurrent bidirectional |

## Unsupported scenarios

The following scenarios are reported structurally as unsupported:

- 0-RTT connection
- QUIC datagram
- Connection migration
- Advanced version negotiation
- Transport parameter edge cases
- Loss/recovery scenarios
- Unidirectional streams (when not supported by the scenario)
- Any scenario outside the `fixture.quic` family
- Non-QUIC protocols (h1, h2, h3)
- Missing or empty ALPN

## QUIC endpoint metadata

The adapter returns the following endpoint metadata in the
`AdapterEndpoint.Extensions` dictionary:

| Key | Type | Value |
|---|---|---|
| `alpn` | `string[]` | Configured ALPN |
| `sni` | `string` | `"localhost"` |
| `transport` | `string` | `"udp"` |
| `streamBehavior` | `string` | `"bidirectional"` |
| `supportedStreamDirections` | `string[]` | `["bidirectional"]` |
| `datagramSupported` | `bool` | `false` |
| `zeroRttSupported` | `bool` | `false` |

## Metrics

| Metric | Scope | Description |
|---|---|---|
| `quic.listening` | endpoint | Whether the QUIC server is currently listening |
| `quic.connections.accepted` | endpoint | Total accepted QUIC connections |
| `quic.streams.opened` | endpoint | Total accepted QUIC streams |
| `quic.bytes.received` | endpoint | Total bytes received |
| `quic.bytes.sent` | endpoint | Total bytes sent |
| `endpoint.port` | endpoint | QUIC protocol endpoint port |

## Artifacts

| Type | Description |
|---|---|
| `stdout` | Adapter stdout capture |
| `stderr` | Adapter stderr capture |
| `session` | Session state snapshot (JSON) |
| `endpoint` | Protocol endpoint snapshot (JSON) |
| `server-log` | QUIC server runtime log (text) |

## Current limitations

- Process-backed only; Docker packaging is deferred.
- Only bidirectional stream support; unidirectional streams not tested.
- Datagram and 0-RTT not supported.
- No connection migration or advanced version negotiation.
- Self-signed certificate; loopback certificate validation is bypassed.
- Metrics are snapshot-based; no continuous monitoring.
- Requires platform with msquic support.
