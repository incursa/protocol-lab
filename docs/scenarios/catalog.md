# Scenario Catalog

## Layout

Scenarios are organized by protocol family under `scenarios/`:

```
scenarios/
  http/         — HTTP application workloads
    core/       — Basic HTTP scenarios (plaintext, json, status)
    payload/    — Sized payload scenarios (bytes, stream)
    headers/    — Header manipulation scenarios
    upload/     — Upload scenarios (echo, hash, sink)

  http3/        — H3 protocol-level scenarios
    protocol/   — Multiplexing, QPACK, cancellation

  quic/         — QUIC transport scenarios
    transport/  — Handshake, streams, multiplex, churn, duplex

  websocket/    — WebSocket scenarios (placeholder)
  webtransport/ — WebTransport scenarios (placeholder)
  masque/       — MASQUE scenarios (placeholder)

  network/
    profiles/   — Network impairment profiles (NOT scenarios)
```

## Stable scenarios (v1)

| ID | Status | Kind | Layer | Protocol |
|----|--------|------|-------|----------|
| `http.core.plaintext` | stable | workload | application | h1 |
| `http.core.json` | stable | workload | application | h1 |
| `http.core.status` | stable | workload | application | h3 |
| `http.payload.bytes.1kb` | stable | workload | application | h1 |
| `http.payload.bytes.64kb` | stable | workload | application | h3 |
| `http.payload.bytes.1mb` | stable | workload | application | h3 |
| `http.payload.stream.100x16kb` | stable | workload | application | h3 |
| `http.headers.response.50x32` | stable | workload | application | h3 |
| `http.headers.inspect-request` | stable | workload | application | h3 |
| `http.upload.echo.64kb` | stable | workload | application | h3 |
| `http.upload.hash.1mb` | stable | workload | application | h3 |
| `http.upload.sink.1mb` | stable | workload | application | h3 |

The HTTP core plaintext, JSON, and 1KB payload scenarios use `h1` as their
primary protocol for the HTTP/1 core lane, but their requirements remain valid
for `h1`, `h2`, and `h3` when a selected executor and implementation both
declare support.

## Experimental scenarios

| ID | Status | Kind | Layer | Protocol |
|----|--------|------|-------|----------|
| `h3.protocol.multiplex-100-streams` | experimental | protocolValidation | protocol | h3 |
| `h3.protocol.qpack-repeated-headers` | experimental | protocolValidation | protocol | h3 |
| `h3.protocol.cancel-mid-response` | experimental | protocolValidation | protocol | h3 |
| `quic.transport.multiplex.100x64kb` | stable, load-validated | protocolValidation | transport | quic |
| `quic.transport.duplex-streams` | stable, load-validated | protocolValidation | transport | quic |
| `quic.transport.handshake-cold` | stable, unsupported | protocolValidation | transport | quic |
| `quic.transport.stream-throughput.1mb` | stable, unsupported | protocolValidation | transport | quic |
| `quic.transport.connection-churn` | stable, unsupported | protocolValidation | transport | quic |

## Placeholder scenarios (not runnable by default)

| ID | Status | Kind | Layer | Protocol |
|----|--------|------|-------|----------|
| `websocket.echo` | placeholder | protocolValidation | application | ws |
| `webtransport.session-open` | placeholder | protocolValidation | application | h3 |
| `webtransport.session-bidi-echo` | placeholder | protocolValidation | application | h3 |
| `masque.connect-udp-tunnel` | placeholder | protocolValidation | transport | h3 |
| `masque.connect-udp` | placeholder | protocolValidation | transport | h3 |

Placeholder scenarios require explicit opt-in to run, and even then report as not implemented since no validator or load tool exists.

## Listing scenarios

Use the CLI to view the catalog:

```
protocol-lab list scenarios
```

Output is grouped by protocol and includes status, kind, layer, traffic shape, and required capabilities.
