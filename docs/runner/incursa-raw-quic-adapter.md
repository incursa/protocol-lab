# Incursa Raw QUIC Adapter v1

## Overview

`Incursa Raw QUIC Adapter v1` is the second real raw QUIC ProtocolLab adapter.
It exposes the ProtocolLab Adapter Contract v1 control plane over HTTP/1.1 JSON
and launches the `IncursaRawQuicServer` (in this repo under `servers/`) as a
child process for the raw QUIC protocol endpoint.

## Relationship to Other Adapters

| Adapter | Protocol | Type | Notes |
|---|---|---|---|
| Kestrel Adapter v1 | HTTP/1.1, HTTP/2, HTTP/3 | Process-backed | Baseline HTTP adapter |
| Incursa HTTP/3 Adapter v1 | HTTP/3 | Process-backed | Incursa HTTP/3 sample |
| MSQuic/.NET Raw Adapter v1 | Raw QUIC | In-process | Baseline raw QUIC adapter |
| **Incursa Raw QUIC Adapter v1** | Raw QUIC | Process-backed | This adapter |

## How to Run

```powershell
$env:ASPNETCORE_URLS = "http://127.0.0.1:53591"
dotnet run --project src\Incursa.ProtocolLab.Adapters.IncursaRawQuic\Incursa.ProtocolLab.Adapters.IncursaRawQuic.csproj --no-launch-profile
```

Optional environment variables:
- `PROTOCOL_LAB_INCURSA_RAW_QUIC_PORT` - QUIC listen port
- `PROTOCOL_LAB_INCURSA_RAW_QUIC_ALPN` - ALPN (default: `plab-raw-quic`)
- `PROTOCOL_LAB_INCURSA_RAW_QUIC_CERT_SUBJECT`
- `PROTOCOL_LAB_INCURSA_RAW_QUIC_SERVER_PROJECT`
- `PROTOCOL_LAB_INCURSA_RAW_QUIC_START_TIMEOUT_SECONDS`
- `PROTOCOL_LAB_INCURSA_RAW_QUIC_READINESS_TIMEOUT_SECONDS`
- `PROTOCOL_LAB_INCURSA_RAW_QUIC_HTTP_TIMEOUT_SECONDS`

## Supported Scenarios

| Scenario | Description |
|---|---|
| `fixture.quic.handshake` | QUIC connection handshake |
| `fixture.quic.bidirectional-echo` | Bidirectional stream echo |
| `fixture.quic.bidirectional-bulk` | Concurrent bidirectional bulk streams |

## Unsupported

- 0-RTT, datagram, migration, version negotiation, transport parameters,
  loss/recovery, unidirectional streams, non-QUIC protocols

## Endpoint Metadata

Same QUIC/UDP metadata keys as MSQuic adapter: `alpn`, `sni`, `transport`,
`streamBehavior`, `supportedStreamDirections`, `datagramSupported`,
`zeroRttSupported`.

## Limitations

- Child process launch only (no Docker)
- Requires platform msquic support (same as MSQuic adapter)
- Self-signed certificate for loopback
- No qlog or SSL key log exports
