---
title: "Scenario Catalog"
---

# Scenario Catalog

The public scenario catalog is the set of YAML files under
[`scenarios/`](../../scenarios/). Scenario YAML files are declarative public
contract inputs. They do not select packages, runners, adapters, hosted labs,
or executable validation logic.

## Protocol Family Map

| Family | Scenario ID Prefix | Protocol Token | Catalog Path | Status |
| --- | --- | --- | --- | --- |
| HTTP/1 | `http1.*` | `h1` | [`scenarios/http1/`](../../scenarios/http1/) | Stable application scenarios |
| HTTP/2 | `http2.*` | `h2` | [`scenarios/http2/`](../../scenarios/http2/) | Stable application scenarios |
| HTTP/3 | `http3.*` | `h3` | [`scenarios/http3/`](../../scenarios/http3/) | Stable application and protocol scenarios |
| QUIC | `quic.*` | `quic` | [`scenarios/quic/`](../../scenarios/quic/) | Stable transport scenarios plus experimental datagram coverage |
| WebSocket placeholder | `websocket.*` | `ws` | [`scenarios/websocket/`](../../scenarios/websocket/) | Generic placeholder only |
| WebSocket over HTTP/1.1 | `http1.websocket.rfc6455.cleartext.*`, `http1.websocket.rfc6455.tls.*` | `h1` | [`scenarios/http1/websocket/`](../../scenarios/http1/websocket/) | Separate draft cleartext ws and TLS-protected wss contracts |
| WebSocket over HTTP/2 | `http2.websocket.rfc8441.*` | `h2` | [`scenarios/http2/websocket/`](../../scenarios/http2/websocket/) | Draft public contracts |
| WebSocket over HTTP/3 | `http3.websocket.rfc9220.*` | `h3` | [`scenarios/http3/websocket/`](../../scenarios/http3/websocket/) | Draft public contracts; package-aligned IDs retained |
| WebTransport | `webtransport.*` | `h3` | [`scenarios/webtransport/`](../../scenarios/webtransport/) | Placeholder only |
| MASQUE | `masque.*` | `h3` | [`scenarios/masque/`](../../scenarios/masque/) | CONNECT-UDP placeholders plus draft CONNECT-IP contract |
| TLS | `tls.*` | `tls` | [`scenarios/tls/`](../../scenarios/tls/) | TLS 1.2/1.3 lifecycle, authentication, early-data, KeyUpdate, and record contracts |
| gRPC over HTTP/2 | `grpc.h2.*` | `h2` | [`scenarios/grpc/h2/`](../../scenarios/grpc/h2/) | Unary, all streaming shapes, terminal outcomes, gzip, metadata, size boundaries, and channel lifecycle |
| Classic DNS diagnostics | `dns.classic.*` | `dns` | [`scenarios/dns/classic/`](../../scenarios/dns/classic/) | UDP, TCP, and truncated-UDP-to-TCP calibration contracts |
| DNS over TLS | `dns.dot.*` | `dot` | [`scenarios/dns/dot/`](../../scenarios/dns/dot/) | Strict and interoperability authoritative contracts plus recursive-resolver diagnostics |
| DNS over HTTPS/2 | `dns.doh2.*` | `doh2` | [`scenarios/dns/doh2/`](../../scenarios/dns/doh2/) | Strict and interoperability authoritative contracts plus recursive-resolver diagnostics |
| DNS over HTTPS/3 | `dns.doh3.*` | `doh3` | [`scenarios/dns/doh3/`](../../scenarios/dns/doh3/) | Strict and interoperability authoritative contracts |
| DNS over QUIC | `dns.doq.*` | `doq` | [`scenarios/dns/doq/`](../../scenarios/dns/doq/) | Strict and interoperability authoritative contracts |

HTTP/3 protocol scenarios use `http3.protocol.*` IDs even though their wire
protocol token is `h3`. HTTP/3 external peer characterization uses
`http3.external.*` IDs for diagnostic interoperability evidence that remains
separate from official HTTP/3 payload benchmarks. QUIC transport scenarios use
`quic.*` IDs and remain separate from HTTP/3 application and protocol
scenarios.

Placeholder scenarios document future contract intent. They do not imply that
any conforming runner, adapter, test executor, package, or hosted lab can
execute them. Planned/open families are tracked here until a useful
implementation-neutral scenario shape is ready.

## Scenario Families

- HTTP/1 core and payload scenarios: `http1.core.*`, `http1.payload.*`
- HTTP/2 core and streaming scenarios: `http2.core.*`,
  `http2.streaming.*`
- HTTP/3 application scenarios: `http3.core.*`, `http3.headers.*`,
  `http3.payload.*`, `http3.upload.*`
- HTTP/3 protocol scenarios: `http3.protocol.*`
- HTTP/3 external peer characterization: `http3.external.*`
- QUIC transport scenarios: `quic.transport.*`
- TLS lifecycle, early-data, KeyUpdate, and record scenarios:
  `tls.handshake.*`, `tls.early-data.*`, `tls.key-update.*`, `tls.record.*`
- gRPC/H2 unary, streaming, terminal-outcome, compression, metadata, message
  boundary, and channel-lifecycle scenarios: `grpc.h2.*`
- secure and classic diagnostic DNS scenarios: `dns.dot.*`, `dns.doh2.*`,
  `dns.doh3.*`, `dns.doq.*`, `dns.classic.*`

Secure-DNS `*.interoperability.*` scenarios retain exact protocol binding and
authenticated endpoint identity while recording standards-compatible
negotiated cryptography. Resolver-role scenarios are cold-cache diagnostics
with a runner-provided local authority as their only upstream; they are not
comparable with authoritative-server scenarios.
- binding-specific WebSocket scenarios: `http1.websocket.*`, `http2.websocket.*`, `http3.websocket.*`
- Future placeholders: `websocket.*`, `webtransport.*`, and the retained MASQUE CONNECT-UDP IDs
- Network profile documents under
  [`scenarios/network/profiles/`](../../scenarios/network/profiles/) are
  supporting declarative profiles, not executable protocol scenarios. The
  representative profile set covers clean, RTT, bandwidth, loss, reordering,
  ECN, and MTU shapes without implying any required execution support.

Suites must reference scenario IDs that exist in this catalog. Package-local
suites must reference package-local scenario IDs supplied by the same scenario
pack fixture.

Draft contract presence does not imply a compatible executor, implementation,
package, runner, benchmark, or publishable result. The five RFC 9220 IDs match
downstream package vocabulary, but public support remains unproven until a
separately authorized package-backed vertical smoke passes.
