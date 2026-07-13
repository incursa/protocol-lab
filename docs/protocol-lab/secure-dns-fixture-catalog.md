# Secure DNS Fixture and Diagnostic Catalog

This catalog defines the bounded public DNS semantic corpus used by the secure-DNS contract family. The local `plab.test.` authority is authoritative, non-recursive, cache-disabled, and prohibited from using an external upstream. TTL values are zero.

The corpus deliberately does not multiply every semantic case across DoT, DoH2, DoH3, and DoQ. The A fixture remains the cross-transport baseline. Additional semantic breadth and the representative DoH GET binding are selected on DoH3 only.

DNS wire fixture v1 is frozen at `schemas/dns/v1/dns-wire-fixture.schema.json` and retains the original secure-transport A contract. Semantic classification, DoH GET metadata, classic UDP/TCP framing, and truncation-to-TCP metadata are v2-only fields under `schemas/dns/v2/dns-wire-fixture.schema.json`. Validators must route fixtures by the exact `schemaVersion`; a v2 fixture must not be accepted through the v1 schema.

## Canonical wire fixtures

| Fixture ID | Question | Canonical outcome | Query / response bytes | Representative scenario |
|---|---|---|---:|---|
| `dns.plab-test-a.canonical` (v1) | `plab.test. IN A` | `NOERROR`, `192.0.2.1` | 27 / 43 | Frozen secure-transport A baseline |
| `dns.plab-test-a-v2.canonical` | `plab.test. IN A` | `NOERROR`, `192.0.2.1` | 27 / 43 | v2 DoH GET and classic UDP/TCP bindings |
| `dns.plab-test-aaaa.canonical` | `plab.test. IN AAAA` | `NOERROR`, `2001:db8::1` | 27 / 55 | `dns.doh3.query.aaaa` |
| `dns.alias-plab-test-cname-chain.canonical` | `alias.plab.test. IN A` | CNAME to `target.plab.test.`, then `192.0.2.1` | 33 / 95 | `dns.doh3.query.cname-chain` |
| `dns.missing-plab-test-nxdomain.canonical` | `missing.plab.test. IN A` | `NXDOMAIN` with SOA authority | 35 / 112 | `dns.doh3.query.nxdomain` |
| `dns.plab-test-txt-nodata.canonical` | `plab.test. IN TXT` | `NOERROR`/NODATA with SOA authority | 27 / 104 | `dns.doh3.query.nodata` |
| `dns.dnskey-plab-test-large-edns-dnssec-shaped.canonical` | `dnskey.plab.test. IN DNSKEY`, EDNS DO, UDP 512 | 630-byte DNSKEY/RRSIG-shaped response | 45 / 630 | `dns.doh3.query.large-dnssec-shaped`; `dns.classic.udp-truncated-tcp-retry` |

The large fixture exercises EDNS sizing, DNSKEY/RRSIG-shaped record parsing, and truncation behavior. It does not claim that the synthetic RRSIG is cryptographically valid.

## DoH GET identity

`dns.doh3.get.a` uses the v2 A fixture and encodes its 27-byte canonical query as the unpadded base64url value `AAAAAAABAAAAAAAABHBsYWIEdGVzdAAAAQAB`. Its exact request target is `/dns-query?dns=AAAAAAABAAAAAAAABHBsYWIEdGVzdAAAAQAB`; the request body and request `Content-Type` are absent. The response remains `application/dns-message` with status 200 and `Cache-Control: no-store`.

## Classic diagnostic calibration

Classic DNS identities are diagnostic-only and non-publishable:

- `dns.classic.udp.query.a` sends the 27-byte query and receives the 43-byte response as bare UDP datagrams.
- `dns.classic.tcp.query.a` uses two-octet network-order prefixes `001b` and `002b` for those messages.
- `dns.classic.udp-truncated-tcp-retry` advertises a 512-byte UDP payload. The fixture authority returns a 45-byte response with `TC=1`; the client then sends the identical question over TCP with a new unique ID and receives the full 630-byte response with prefix `0276`.

The truncation-driven retry is part of the classic diagnostic identity. It is not an allowed secure-DNS transport fallback.

## Suite selection

- `dns-doh3-semantics-diagnostic-smoke` selects the five additional semantic cases and the representative GET binding with `secure-dns-smoke`.
- `dns-classic-calibration-diagnostic-smoke` selects UDP, TCP, and truncation-to-TCP calibration with `dns-classic-diagnostic`.

Both suites produce diagnostic results only and are intentionally unsuitable for ranking or publication as benchmark comparisons.
