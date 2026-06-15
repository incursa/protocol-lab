# Scenario Catalog

The public scenario catalog is the set of YAML files under [`scenarios/`](../../scenarios/).

## Current Families

- HTTP/1 application scenarios under [`scenarios/http1/`](../../scenarios/http1/)
- HTTP/2 application scenarios under [`scenarios/http2/`](../../scenarios/http2/)
- HTTP/3 application scenarios under [`scenarios/http3/`](../../scenarios/http3/)
- HTTP/3 protocol scenarios
- Raw QUIC transport scenarios
- WebSocket placeholders
- WebTransport placeholders
- MASQUE placeholders
- Network profile documents

Placeholder scenarios document future contract intent. They do not imply that
any conforming runner, adapter, test executor, or hosted lab can execute them.

Protocol-specific application scenarios use protocol-specific IDs such as
`http1.core.plaintext`, `http2.core.plaintext`, and `http3.core.status`.
Suites must reference scenario IDs that exist in this catalog.
