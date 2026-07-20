---
title: "Load Model"
---

# Load Model

The public load model defines declarative load intent. It does not define a
repository-owned load generator or wrapper.

## Public Documents

- load profiles describe intensity and repetition shape
- scenarios describe behavior under test
- suites group scenarios for a purpose
- run plans select the package-pinned components that an implementation will
  use

Current public load profiles live under [`../../load-profiles/`](../../load-profiles/).
They are generic profile IDs with protocol-specific intensity settings for
HTTP/1, HTTP/2, HTTP/3, and QUIC where useful. The `http1`, `http2`, `http3`,
and `quic` settings do not create separate behavior semantics; scenarios own
behavior.

For HTTP/2, `connections` is the requested number of simultaneously
established HTTP/2 connections, `concurrency` is the global maximum number of
in-flight HTTP operations, and `streamsPerConnection` is the per-connection
in-flight stream cap. These values are not multiplied together. A compatible
profile must satisfy `concurrency <= connections * streamsPerConnection` and
must state how operations are distributed across connections. The initial
`http2-comparison` profile requests 16 connections, 128 global operations, 8
streams per connection, and `balanced-round-robin` distribution.

TLS handshake timing boundaries define which work belongs in a latency
measurement; they are not performance thresholds. `tlsHandshakeLatency`
starts when TLS processing begins on an established TCP connection and ends
when the authenticated TLS handshake completes. It excludes DNS lookup, TCP
connection establishment, target startup, certificate generation, and
artifact writing. An executor may additionally report
`connectionAndHandshakeLatency`, measured from TCP-connect start through the
same authenticated TLS completion point.

Supporting declarative network-profile documents live under
[`../../scenarios/network/profiles/`](../../scenarios/network/profiles/). They
describe clean, RTT, bandwidth, loss, reordering, ECN, and MTU shapes as
public contract data without claiming a required impairment engine or runner.

## Implementation Responsibilities

An implementation chooses how to translate public load intent into traffic and
measurements. It must preserve the requested load profile, record effective
load shape, and report unsupported or unavailable load behavior explicitly.
It must also fail closed when the effective protocol, executor, load generator,
connection count, global concurrency, stream cap, or distribution differs from
the requested comparison contract.
