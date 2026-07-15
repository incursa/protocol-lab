---
title: "WebTransport And MASQUE Component-Surface Audit"
---

# WebTransport And MASQUE Component-Surface Audit

Audit date: 2026-07-14 (America/Denver)

This is a dated, non-normative repository audit. It records what the public
contract repository and the sibling component repository expose at the
snapshots below. It does not add, amend, or interpret normative authority in
SpecTrace or JSON Schema, and it does not claim implementation support.

## Snapshot And Method

- Public contracts: `protocol-lab` commit
  `c0475b05cb80362760ac57e58ecfa1610a766c10`, before this documentation-only
  change.
- Component surfaces: `protocol-lab-components` commit
  `d284db654702e5840645d90c1e5b85700a629cea` plus its current working tree.
  The sibling working tree was already dirty; this audit made no changes to it.
- Inspected public scenarios, suites, load profiles, schemas, fixtures,
  SpecTrace surfaces, scenario catalogs, and roadmap material.
- Inspected all 82 source component package manifests under `implementations/`,
  `executors/`, and `scenarios/`. None declares a WebTransport or MASQUE
  package, implementation, executor, scenario, or suite. Textual mentions that
  explicitly say `webtransport` is unsupported were not counted as support.

## Result At A Glance

| Family | Public scenarios | Public suites | Component packages | Current state |
| --- | ---: | ---: | ---: | --- |
| WebTransport | 2 | 0 | 0 | Design-only placeholders; not runnable |
| MASQUE | 3 | 0 | 0 | Two CONNECT-UDP placeholders and one explicitly deferred CONNECT-IP draft; not runnable |

HTTP/3 support is a prerequisite for both families, but an HTTP/3 origin,
client, or generic executor is not evidence of WebTransport or MASQUE support.
No current HTTP/3 package is counted by substitution in this audit.

## WebTransport

### Public Surface

| Scenario ID | Public role | Contract detail | Runnable state |
| --- | --- | --- | --- |
| [`webtransport.session-open`](../../scenarios/webtransport/session-open.yaml) | `server` target required; an initiating client/executor role is not contracted | Schema `1.0`; `status: placeholder`; session establishment over `h3`; `validation.required: false` | Design-only |
| [`webtransport.session-bidi-echo`](../../scenarios/webtransport/session-bidi-echo.yaml) | `server` target required; an initiating client/executor role is not contracted | Schema `1.0`; `status: placeholder`; one bidirectional stream at `/webtransport/echo` with a 65,536-byte bidirectional payload; `validation.required: false` | Design-only |

Exact public suite IDs: **none**. Exact public load-profile IDs: **none**.
There is no WebTransport-specific public fixture or dedicated WebTransport
SpecTrace requirement, architecture, or verification family. The v1 scenario
schema exposes `webTransport` only as a generic object; it does not type or
require the fields used by `webtransport.session-bidi-echo`.

### Component Surface

Exact WebTransport component package IDs and versions: **none**. There is no
WebTransport target package, client test-executor package, scenario package, or
suite package in `protocol-lab-components`.

The component inventory identifies two possible ecosystems, but neither is a
WebTransport component surface:

- `webtransport-go` is recorded as an interop-image candidate using
  `martenseemann/webtransport-go-interop:latest`. Its package list is empty,
  its public catalog entry is planned, and its inventory says contracts and
  scheduling are prerequisites.
- `ngtcp2-nghttp3` records that an upstream WebTransport branch image exists.
  The existing package
  `org.protocol-lab.components.implementation.ngtcp2-http3@0.1.4` is only an
  HTTP/3 wrapper and must not be counted as WebTransport support.

### Missing Pieces

1. A typed public WebTransport contract with deterministic session, stream,
   payload, failure, and acceptance semantics.
2. Required validation and fixture-backed proof for the 65,536-byte echo.
3. An explicit initiating client/test-executor role and executor contract.
4. A root suite and a load profile that select an executable comparison lane.
5. A package-local scenario/suite surface and two target packages whose
   manifests explicitly claim the same WebTransport scenario.
6. Live lab proof before either candidate is shown as runnable evidence.

### Narrowest Honest Two-Implementation Slice

Promote only `webtransport.session-bidi-echo` first, with session establishment
as a required prerequisite check rather than a separately ranked result. Keep
the existing one-stream, 65,536-byte shape; add a deterministic payload and
hash/byte-count acceptance proof; add one client test executor, one scenario
package, one root suite, and one conservative smoke load profile. Package
`webtransport-go` and the ngtcp2 WebTransport branch as two independent server
targets only after both expose the exact same contract. This is a delivery
candidate, not a current support claim.

## MASQUE

### Public Surface

| Scenario ID | Public role | Contract detail | Runnable state |
| --- | --- | --- | --- |
| [`masque.connect-udp`](../../scenarios/masque/connect-udp.yaml) | `server`; an initiating client and explicit proxy topology are not contracted | Schema `1.0`; `status: placeholder`; CONNECT-UDP establishment and observable datagram forwarding; `validation.required: false` | Design-only |
| [`masque.connect-udp-tunnel`](../../scenarios/masque/connect-udp-tunnel.yaml) | `server`; an initiating client and explicit proxy topology are not contracted | Schema `1.0`; `status: placeholder`; target `example.invalid:443`; datagrams required; `validation.required: false` | Design-only; the target is not a runnable fixture |
| [`masque.connect-ip-tunnel`](../../scenarios/masque/connect-ip-tunnel.yaml) | `client` and `proxy` | Schema `2.0`; version `2.0.0`; `status: draft`; explicitly deferred pending a deterministic packet fixture, compatible executor, package-backed proxy target, and executable suite | Design-only and explicitly non-executable |

Exact public suite IDs: **none**. Exact public load-profile IDs: **none**.
There is no MASQUE-specific public packet/datagram fixture or dedicated MASQUE
SpecTrace requirement, architecture, or verification family. The v1 scenario
schema exposes `masque` only as a generic object; the v2 schema adds no typed
MASQUE constraints of its own.

### Component Surface

Exact MASQUE component package IDs and versions: **none**. There is no MASQUE
proxy target package, client test-executor package, scenario package, or suite
package in `protocol-lab-components`. The component inventory also does not
name two MASQUE proxy implementation candidates. Existing HTTP/3 origin and
client packages do not contract a proxy role and are not substitutes.

### Missing Pieces

1. A typed CONNECT-UDP contract with explicit client, proxy, and UDP echo-target
   roles and controlled topology.
2. A local deterministic UDP target fixture to replace `example.invalid:443`,
   including payload, datagram-count, and integrity acceptance semantics.
3. A compatible CONNECT-UDP client/test executor.
4. Two independently implemented proxy target packages with explicit
   CONNECT-UDP claims; no two candidates are currently identified in the
   repositories.
5. A package-local scenario, root suite, conservative load profile, and live
   lab proof.
6. For CONNECT-IP later: every blocker already recorded in the draft scenario,
   especially the approved deterministic packet fixture and package-backed
   proxy target.

### Narrowest Honest Two-Implementation Slice

Start with `masque.connect-udp-tunnel`; treat tunnel establishment as a required
prerequisite rather than a separately ranked result. Define a controlled
client-to-proxy-to-local-UDP-echo topology, deterministic datagrams, and
byte/count/hash acceptance checks. Then add one client test executor, one
scenario package, one root suite, one conservative smoke load profile, and two
independent proxy packages that claim that exact contract. The repository does
not currently identify two honest proxy candidates, so implementation
selection is a discovery blocker. CONNECT-IP remains outside this first slice.

## Delivery Boundary

Neither family can produce comparable public observations from current
repository surfaces. WebTransport can proceed to contract maturation and
candidate-package spikes using the two inventoried ecosystems. MASQUE first
needs a proxy implementation inventory and topology decision. In both cases,
ranking remains premature until the public comparison policy defines cohort
and topology controls and the lab retains repeated, package-pinned evidence.

This audit deliberately leaves the existing scenarios, schemas, contracts,
component manifests, and implementation-coverage wishlist unchanged.
