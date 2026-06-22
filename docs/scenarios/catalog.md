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
| WebSocket | `websocket.*` | `ws` | [`scenarios/websocket/`](../../scenarios/websocket/) | Placeholder only |
| WebTransport | `webtransport.*` | `h3` | [`scenarios/webtransport/`](../../scenarios/webtransport/) | Placeholder only |
| MASQUE | `masque.*` | `h3` | [`scenarios/masque/`](../../scenarios/masque/) | Placeholder only |
| TLS | none yet | none yet | none yet | Planned/open |
| DNS | none yet | none yet | none yet | Planned/open |
| gRPC over HTTP/2 or HTTP/3 | none yet | `h2` or `h3` when defined | none yet | Planned/open |

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
- Future placeholders: `websocket.*`, `webtransport.*`, `masque.*`
- Network profile documents under
  [`scenarios/network/profiles/`](../../scenarios/network/profiles/) are
  supporting declarative profiles, not executable protocol scenarios.

Suites must reference scenario IDs that exist in this catalog. Package-local
suites must reference package-local scenario IDs supplied by the same scenario
pack fixture.
