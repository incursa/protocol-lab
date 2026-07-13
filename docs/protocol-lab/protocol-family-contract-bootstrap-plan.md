---
title: "Protocol Family Contract Bootstrap Plan"
status: draft
owner: protocol-lab-maintainers
date: 2026-07-12
---

# Protocol Family Contract Bootstrap Plan

## Decision Summary

This bootstrap activates public, performance-oriented contract foundations for
TLS 1.3, gRPC over HTTP/2, secure DNS transports, and WebSocket over HTTP/1.1,
HTTP/2, and HTTP/3. It retains and normalizes the existing HTTP, HTTP/3, QUIC,
QUIC DATAGRAM, HTTP Datagram, Capsule, and MASQUE vocabulary. It does not claim
that an executor, target implementation, package, runner, lab, or public result
exists.

The contract model is performance-first but fail-closed. A performance sample
is not eligible for acceptance unless the requested protocol stack and binding,
operation, deterministic payload, expected completion state, failure and
timeout state, and selected component identities are valid. Comprehensive RFC
conformance remains outside this bootstrap.

The immediate executable future target should be WebSocket over HTTP/3 using
the five preserved RFC 9220 IDs. That lane already has the narrowest credible
downstream proof lineage, but the public contract must not import the package's
support status or its package-local suite because that suite uses values outside
public suite-v1.

## Scope And Non-Goals

In scope:

- canonical SpecTrace requirements, architecture, and planned verification;
- typed public scenario and load-profile fields;
- declarative scenarios, suites, profiles, and representative fixtures;
- current-state reconciliation and exact ID preservation;
- implementation-neutral validity, measurement, comparison, and evidence
  boundaries;
- roadmap entries for deferred families.

Not in scope:

- runner, controller, worker, executor, load-tool, server, client, library, or
  proxy implementation;
- implementation, scenario, executor, or toolchain package production;
- generated protobuf source, certificate provisioning, DNS daemon setup, or
  network impairment commands;
- benchmarks, rankings, publishable evidence, deployments, uploads, restarts,
  publication, or lab-machine changes;
- comprehensive RFC conformance requirements;
- synthetic work items or completed verification records.

## Authority And Repository Boundaries

| Repository | Authority in this program | Audit result | Mutation in this pass |
| --- | --- | --- | --- |
| `protocol-lab` | Public SpecTrace, schemas, scenarios, suites, profiles, fixtures, and catalogs | Primary authority | Yes, declarative contracts only |
| `protocol-lab-components` | Versioned implementation, scenario, executor, and toolchain packages | Package claims and exact IDs audited | No |
| `protocol-lab-internal` | Runner, selection, validation, benchmarking, evidence, and readiness | H1/H2/H3/QUIC and RFC 9220 routing audited | No |
| `protocol-lab-site` | Public protocol and evidence presentation | Catalog vocabulary audited | No |
| `quic-dotnet` | QUIC/HTTP3 implementation and RFC-owned implementation traceability | RFC 9220 and RFC 9250 implementation proof audited | No |

## Current-State Inventory

| Family or lane | Public authority state before bootstrap | Downstream state | Classification |
| --- | --- | --- | --- |
| HTTP/1.1 | `http1.core.plaintext`, `http1.core.json`, `http1.payload.bytes.1kb`; suites and profiles exist | Go executor and implementation packages cover a narrow core | canonical and stable |
| HTTP/2 | `http2.core.plaintext`, `http2.core.json`, `http2.streaming.response`; suites and profiles exist | Kestrel H2 package and H2-capable tools exist | canonical and stable, performance breadth incomplete |
| HTTP/3 | broad `http3.*` catalog including payload, upload, headers, stream, cancellation, multiplexing, QPACK, and external peer diagnostics | multiple target and executor packages exist | canonical and stable |
| QUIC transport | handshake, resumption, 0-RTT, churn, streams, multiplexing, duplex, and throughput exist | raw QUIC scenario, target, and load packages exist | canonical and stable |
| QUIC DATAGRAM | `quic.transport.datagram.1kb` | downstream capability varies | canonical but experimental |
| HTTP Datagrams and Capsule Protocol | represented indirectly by MASQUE placeholder vocabulary | no complete public executable lane | canonical concern, incomplete |
| MASQUE CONNECT-UDP | `masque.connect-udp` and `masque.connect-udp-tunnel` | no complete public package-backed lane | placeholder |
| MASQUE CONNECT-IP | no public scenario | no public package-backed lane | missing before bootstrap |
| Generic WebSocket | `websocket.echo` | runner placeholder vocabulary exists | placeholder; deliberately retained |
| WebSocket over HTTP/3 | no root scenarios before bootstrap | executor `aioquic-rfc9220-websocket`, scenario package, and five exact package-local IDs exist | package claim without complete public contract |
| WebSocket over HTTP/1.1 or HTTP/2 | no root binding-specific scenarios | no complete public package-backed lane found | missing before bootstrap |
| TLS performance | no scenarios, suites, or profiles | TLS is used inside other lanes but has no independent public performance lane | missing before bootstrap |
| gRPC over HTTP/2 | no scenarios, suites, or profiles | no complete public package-backed lane found | missing before bootstrap |
| DoT, DoH2, DoH3, DoQ | no public ProtocolLab scenarios | `quic-dotnet` has RFC 9250 implementation requirements and tests, not ProtocolLab performance contracts | implementation-only for DoQ; otherwise missing |
| WebTransport | two public placeholders | no stable standards-based executable claim | intentionally deferred |
| WebRTC, OHTTP, MQTT, AMQP, CoAP, database and media protocols | roadmap only or absent | not audited as immediate packages | intentionally deferred |

Pre-existing package and runner claims are evidence inputs, not public support
authority. In particular, `quic-dotnet` requirement IDs such as
`REQ-QUIC-RFC9220-*` and `REQ-QUIC-RFC9250-*` remain owned by that implementation
repository and are not copied here.

## Existing IDs Deliberately Retained

- every existing `http1.*`, `http2.*`, `http3.*`, and
  `quic.transport.*` root scenario ID;
- `masque.connect-udp` as setup and negotiation intent;
- `masque.connect-udp-tunnel` as forwarding workload intent;
- `websocket.echo` as a generic placeholder only;
- `webtransport.session-open` and `webtransport.session-bidi-echo` as
  placeholders only;
- RFC 9220 package-aligned IDs:
  - `http3.websocket.rfc9220.extended-connect`
  - `http3.websocket.rfc9220.control-frames`
  - `http3.websocket.rfc9220.text-echo`
  - `http3.websocket.rfc9220.binary-echo`
  - `http3.websocket.rfc9220.close`

Downstream identities retained as audit facts, not public requirements:

- executor package
  `org.protocol-lab.components.executor.aioquic-rfc9220-websocket@0.1.7`;
- executor `aioquic-rfc9220-websocket`;
- scenario package
  `org.protocol-lab.components.scenario.aioquic-rfc9220-websocket@0.1.1`;
- package-local suite `aioquic-rfc9220-websocket-proof`.

## Canonical SpecTrace Ownership

Shared lane rules extend `SPEC-PL-CORE-CONTRACTS`:

- `PLAB-PROTOCOL-001`, ordered stack and lane identity;
- `PLAB-PROTOCOL-002`, requested and observed binding with no fallback;
- `PLAB-SCENARIO-002`, shared `semanticTemplateId` and lane-specific IDs;
- `PLAB-SCENARIO-003`, standards, validity, load, metric, artifact, and
  availability declarations;
- `PLAB-EXECUTIONPROFILE-001`, implementation-neutral execution facts;
- `PLAB-NETWORKPROFILE-001`, requested and effective path identity.

Measurement and report extensions are `PLAB-MEASURE-012` through
`PLAB-MEASURE-014` and `REQ-PL-REPORT-0006` through
`REQ-PL-REPORT-0007`. Their architecture homes remain the existing
measurement and report architecture artifacts.

Each newly activated family has a dedicated specification because protocol
semantics do not fit the generic core-contract specification:

| Family | Specification | Requirement range | Architecture | Verification |
| --- | --- | --- | --- | --- |
| TLS | `SPEC-PL-TLS-PERFORMANCE` | `REQ-PL-TLSPERF-0001..0007` | `ARC-PL-TLS-PERFORMANCE` | `VER-PL-TLS-PERFORMANCE-0001` |
| gRPC/H2 | `SPEC-PL-GRPC-H2-PERFORMANCE` | `REQ-PL-GRPCH2-0001..0007` | `ARC-PL-GRPC-H2-PERFORMANCE` | `VER-PL-GRPC-H2-PERFORMANCE-0001` |
| secure DNS | `SPEC-PL-SECURE-DNS-PERFORMANCE` | `REQ-PL-SECDNS-0001..0008` | `ARC-PL-SECURE-DNS-PERFORMANCE` | `VER-PL-SECURE-DNS-PERFORMANCE-0001` |
| WebSocket | `SPEC-PL-WEBSOCKET-PERFORMANCE` | `REQ-PL-WSPERF-0001..0008` | `ARC-PL-WEBSOCKET-PERFORMANCE` | `VER-PL-WEBSOCKET-PERFORMANCE-0001` |

All new verification artifacts are `planned`. They verify repository contract
integrity only and do not verify executable support or performance.

## Standards Source Map

| Concern | Immediate upstream authority |
| --- | --- |
| HTTP/1.1 | RFC 9110 and RFC 9112 |
| HTTP/2 | RFC 9110 and RFC 9113 |
| HTTP/3 and QPACK | RFC 9114 and RFC 9204 |
| QUIC transport and TLS binding | RFC 9000, RFC 9001, RFC 9002 |
| QUIC DATAGRAM | RFC 9221 |
| HTTP Datagrams and Capsule Protocol | RFC 9297 |
| CONNECT-UDP | RFC 9298 |
| CONNECT-IP | RFC 9484 |
| TLS 1.3 | RFC 9846, which obsoletes RFC 8446; RFC 7301 for ALPN; RFC 9525 for service identity |
| gRPC/H2 | official gRPC HTTP/2 protocol document plus RFC 9113 |
| DNS wire semantics | RFC 1035 |
| DoT | RFC 7858 and RFC 8310 |
| DoH | RFC 8484 plus RFC 9113 or RFC 9114 |
| DoQ | RFC 9250 plus RFC 9000 and RFC 9001 |
| WebSocket/H1 | RFC 6455 and RFC 9112 |
| WebSocket/H2 | RFC 8441 and RFC 9113 |
| WebSocket/H3 | RFC 9220 and RFC 9114 |

These references constrain the minimal performance binding. They do not create
comprehensive RFC coverage claims. External standards extraction and check-level
coverage remain owned by `SPEC-PL-SPECIFICATION-COVERAGE`.

## Contract Model

The scenario ID identifies one durable binding-specific operation. The
`semanticTemplateId` links equivalent workload meaning across lanes without
making the lanes comparable by default. `protocolStack` records the ordered
application-to-transport stack. Typed `tls`, `grpc`, `dns`, and `websocket`
objects define the family semantics. `executionProfile` records comparison
facts without prescribing an implementation. `availability` defines
fail-closed unsupported, unavailable, and binding-mismatch outcomes.

Scenario dimensions:

- workload semantics: scenario and family binding object;
- payload: deterministic generator, serialized or wire bytes, and SHA-256;
- load: separate load profile;
- network: existing `scenarios/network/profiles/` authority;
- execution/security: `executionProfile` plus evidence metadata;
- measurement: `requiredMetrics` and `benchmarkCompat`;
- comparison: exact stack, role, platform, topology, load, network, validation,
  telemetry, saturation, and repetition compatibility;
- evidence: profile intent, with every bootstrap comparison profile explicitly
  non-publishable.

Connection churn, channel reuse, concurrent streams, outstanding queries,
message counts under sustained load, soak duration, and network impairment are
profile dimensions. They are not multiplied into scenario IDs.

## Activated Scenario Catalog

| Family | Setup or lifecycle | Small operation | Sustained transfer or concurrency |
| --- | --- | --- | --- |
| TLS | `tls.handshake.full`, `tls.handshake.resumed` | handshake completion with no application data | `tls.record.throughput`, deterministic 1 MiB |
| gRPC/H2 | channel creation is a load/execution dimension | `grpc.h2.unary.echo`, 128-byte deterministic messages | `grpc.h2.server-streaming.echo`, `grpc.h2.bidi-streaming.echo` |
| DoT | connection lifecycle is a profile dimension | `dns.dot.query.a` | same scenario with concurrent/reused-query profile |
| DoH2 | HTTP/2 session lifecycle is a profile dimension | `dns.doh2.query.a` | same scenario with concurrent streams |
| DoH3 | QUIC/H3 session lifecycle is a profile dimension | `dns.doh3.query.a` | same scenario with concurrent streams |
| DoQ | QUIC session lifecycle is a profile dimension | `dns.doq.query.a` | same scenario with concurrent one-query-per-stream operations |
| WebSocket/H1 cleartext | `http1.websocket.rfc6455.cleartext.upgrade`, `.close` | `.text-echo`, `.control-frames` | `.binary-echo` under sustained profile |
| WebSocket/H1 TLS | `http1.websocket.rfc6455.tls.upgrade`, `.close` | `.text-echo`, `.control-frames` | `.binary-echo` under sustained profile |
| WebSocket/H2 | `http2.websocket.rfc8441.extended-connect`, `.close` | `.text-echo`, `.control-frames` | `.binary-echo` under sustained profile |
| WebSocket/H3 | preserved `.extended-connect`, `.close` | preserved `.text-echo`, `.control-frames` | preserved `.binary-echo` under sustained profile |
| MASQUE CONNECT-IP | `masque.connect-ip-tunnel` is a deferred identity only | no approved packet fixture | no executable profile or suite until fixture, executor, and package-backed proxy approval |

The initial gRPC catalog deliberately omits client streaming. The three active
shapes already cover unary latency, one-way streamed transfer, and sustained
duplex exchange. Client streaming can be added when an implementation-neutral
use case adds distinct performance value.

## Suites And Load Profiles

New load profiles:

- `tls-smoke`, `tls-comparison`;
- `grpc-h2-smoke`, `grpc-h2-comparison`;
- `secure-dns-smoke`, `secure-dns-comparison`;
- `websocket-smoke`, `websocket-comparison`.

Smoke profiles use one connection or channel, concurrency one, five seconds,
one second warmup, one repetition, and short timeouts. Comparison candidates
use 30 seconds, 10 seconds warmup, five repetitions, and explicit target and
load-generator telemetry artifacts. They are not publishable.

Suites are binding-specific because the suite-v1 `protocol` token must match
every selected scenario. TLS and gRPC have smoke and comparison suites. Secure
DNS has smoke and comparison suites for each of `dot`, `doh2`, `doh3`, and
`doq`. WebSocket has smoke and comparison suites for H1 cleartext, H1 TLS,
H2, and H3. Cleartext and TLS H1 results are distinct comparison groups.

MASQUE has no executable suite in this bootstrap. Its explicit blocker is the
absence of an approved deterministic tunnel fixture, compatible executor, and
package-backed target role. Creating a suite now would overstate readiness.

## Metrics And Raw Artifacts

Ranking-required metrics are scenario-specific throughput, latency p50, p95,
p99, completed operations, failed operations, timed-out operations, and total
bytes where bytes are material. The contract also declares mean, p75, p90,
connection or handshake contribution, effective concurrency or streams, and
protocol-specific failure counts where relevant. Target and load-generator CPU,
memory, saturation, network bytes, packet counts, and repetition variance are
diagnostic or comparison-gate evidence when available.

Required raw artifacts include `validation.json`, `protocol-proof.json`,
family summaries, normalized `result.json`, and load-tool stdout and stderr.
Protocol traces, qlog, TLS key logs, frame summaries, packet summaries, and
target/load-generator telemetry are optional for smoke and required or strongly
expected for later controlled comparisons. Sensitive key or packet artifacts
must follow the existing artifact and redaction contracts before publication.

## Schema And Fixture Changes

Scenario v1 remains frozen at `schemas/scenario.schema.json`.
`schemas/scenario/v2/scenario.schema.json` owns shared semantic identity,
ordered protocol stack, standards references, TLS, gRPC, DNS, WebSocket,
execution profile, availability, and requested-versus-observed protocol proof.
All activated protocol-family scenarios and representative fixtures use
`schemaVersion: "2.0"`.

Load-profile v1 remains frozen at
`schemas/load-profile/load-profile.schema.json`.
`schemas/load-profile/v2/load-profile.schema.json` owns TLS, gRPC, DNS, and
WebSocket intensity objects. Activated family profiles and fixtures use
`protocol-lab.load-profile.v2`.

`schemas/dns/v1/dns-wire-fixture.schema.json` remains frozen around the
original canonical fixture. `schemas/dns/v2/dns-wire-fixture.schema.json`
defines the expanded semantic and classic-transport contract. The baseline
bare DNS query is 27 bytes with ID zero, no flags, and
`plab.test. A IN`. The canonical authoritative response is 43 bytes with
`QR+AA`, `NOERROR`, TTL zero, and `192.0.2.1`. DoT and DoQ add the
two-octet network-order length prefix; DoH2 and DoH3 carry the bare DNS message
as `application/dns-message`. DoH and DoQ use message ID zero. DoT uses IDs
unique among outstanding queries and normalizes the ID to zero for canonical
hashing. Received responses are parsed and canonical-reserialized before
hashing, so alternate valid name compression does not fail raw-byte equality.
The v2 catalog additionally fixes A, AAAA, CNAME-chain, NXDOMAIN, NODATA, and
large EDNS/DNSSEC-shaped messages, DoH GET base64url identity, classic UDP and
TCP framing, and the single diagnostic UDP-truncation-to-TCP retry path.

`schemas/grpc/v1/service-contract.schema.json` remains frozen around the
initial deterministic descriptor. `schemas/grpc/v2/service-contract.schema.json`
defines the expanded, language-neutral
`protocollab.performance.v1.EchoService` descriptor. Its
request and response messages each contain field `1`, `bytes payload`; the
service exposes all four RPC shapes plus exact trailers-only, deadline,
cancellation, gzip, and fixed-metadata methods. Scenario variants also fix
empty, 1 MiB, and fresh-channel behavior.
Valid and invalid descriptor fixtures prove the shape without adding generated
source.

Representative valid fixtures cover TLS, gRPC/H2, DoH3, RFC 9220, and the DNS
wire contract. Invalid fixtures cover a missing TLS version, missing DoH media
types, and an invalid DNS wire fixture. Repository health is extended so
scenario, suite, load-profile, gRPC descriptor, and DNS wire invalid fixtures
must actually fail their schemas.

No package fixture is added because no new package is authorized. Package
fixtures belong in the future executor and implementation delivery slices.

## Breadth Boundaries And Deferred Families

- WebTransport: retain placeholders only; no canonical executable claim until
  its HTTP mappings are final standards and a deterministic workload is
  approved.
- WebRTC and WebRTC DataChannel: later real-time/media domain.
- Oblivious HTTP: later privacy relay domain.
- gRPC-Web and gRPC over HTTP/3: later binding candidates.
- TLS 1.2 remains a separately identifiable compatibility scenario rather
  than being mixed into TLS 1.3 comparison rows.
- TLS early data remains replay-sensitive and is split into explicit accepted
  and rejected diagnostic outcomes.
- classic DNS over UDP/TCP remains diagnostic calibration only, including the
  exact truncated-UDP-to-TCP retry case.
- MQTT, AMQP, CoAP, database wire protocols, and media protocols: separate
  future domains.

## Delivery Work Explicitly Deferred

1. Produce scenario packages that mirror approved root scenarios.
2. Author lane-scoped executors with exact binding and component-identity proof.
3. Produce target implementation packages without broadening support claims.
4. Add runner protocol tokens, typed materialization, and fail-closed routing.
5. Run a local implementation + executor + scenario-package smoke.
6. Run controlled multi-implementation comparison campaigns.
7. Generate evidence reports and update public presentation.

Each step requires its own repository authority, package hashes and build
attestations where applicable, and explicit approval for publication, live
controller changes, services, or lab infrastructure.

## Recorded Owner Decisions

1. RFC 9846 is the current TLS 1.3 authority; RFC 8446 remains historical
   provenance.
2. TLS 1.3 remains the primary baseline. TLS 1.2, mutual authentication,
   alternate cipher, early data, KeyUpdate, and record coverage are explicit
   additional contracts that executors may capability-gate independently.
3. The canonical DNS fixture set is versioned: v1 remains frozen and v2 adds
   semantic breadth, DoH GET, and classic diagnostic framing with exact
   transport-specific ID and fallback rules.
4. WebSocket/H1 cleartext and TLS are separate scenario identities, suites, and
   comparison groups.
5. `masque.connect-ip-tunnel` is retained as a draft contract identity; the
   two CONNECT-UDP placeholders remain unchanged.
6. New typed fields are delivered through scenario v2, load-profile v2, DNS
   fixture v2, gRPC service v2, and TLS profile v2. Existing v1 schemas and
   consumers remain frozen.
7. WebSocket over HTTP/3, including all five preserved RFC 9220 identities, is
   the first package-delivery candidate after this public-contract bootstrap.
   Package creation and executable support remain separately authorized work.

## Bootstrap Acceptance Gates

The bootstrap is acceptable when:

1. every changed JSON and YAML document parses;
2. every root scenario, suite, and profile validates;
3. valid fixtures pass and invalid fixtures fail;
4. all suite scenario and profile references resolve with matching protocol
   tokens;
5. SpecTrace requirements use one approved uppercase normative keyword and
   resolve to their architecture and planned verification artifacts;
6. standards references are upstream references, not implementation evidence;
7. catalogs distinguish stable, draft, experimental, placeholder, package-only,
   and deferred state;
8. no executable support, benchmark credibility, or publication claim is made;
9. Markdown relative links and schema IDs are valid;
10. forbidden implementation folders and source extensions remain absent;
11. `git diff --check` passes.

## Recommended First Executable Vertical Slice

After owner approval, take WebSocket over HTTP/3 through one package-backed
local smoke using:

- root scenarios `http3.websocket.rfc9220.extended-connect`,
  `.control-frames`, `.text-echo`, `.binary-echo`, and `.close`;
- public suite `http3-websocket-performance-smoke` and profile
  `websocket-smoke`;
- scenario package successor aligned to the root scenario bytes;
- executor package successor to
  `org.protocol-lab.components.executor.aioquic-rfc9220-websocket@0.1.7`;
- one origin-server implementation package that explicitly exposes
  `/websocket-proof` and claims only the validated RFC 9220 scenarios.

The package-local `aioquic-rfc9220-websocket-proof` suite must either be updated
to the public suite-v1 vocabulary or remain diagnostic-only and unselected by
the public run plan. The local proof must preserve package hashes, build
attestations, selected and actual component identities, stdout, stderr,
protocol proof, validation, and explicit unsupported outcomes. It remains
smoke evidence, not a ranking.
