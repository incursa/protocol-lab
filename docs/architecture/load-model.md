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

Supporting declarative network-profile documents live under
[`../../scenarios/network/profiles/`](../../scenarios/network/profiles/). They
describe clean, RTT, bandwidth, loss, reordering, ECN, and MTU shapes as
public contract data without claiming a required impairment engine or runner.

## Implementation Responsibilities

An implementation chooses how to translate public load intent into traffic and
measurements. It must preserve the requested load profile, record effective
load shape, and report unsupported or unavailable load behavior explicitly.

TLS, gRPC/H2, secure DNS, and WebSocket profiles use load-profile v2
(`schemaVersion: protocol-lab.load-profile.v2`) and carry their own semantic
`version`. Scenarios own payload, protocol deadline, connection lifecycle,
prerequisite, and measured-window semantics. Profiles own pressure and
scheduling: connection or channel capacity, concurrency, operation budget or
rate limit, outer executor timeout, duration, warmup, and repetitions. Suites
select a profile and do not duplicate those values. Load-profile v1 remains
frozen for existing consumers.

Run Plan v2 pins the resolved load-profile snapshot. Test Executor Prepare v2
then carries both that snapshot and the exact resolved load-profile document;
an executor rejects a digest mismatch instead of looking up mutable settings
by ID.

Smoke profiles establish contract validity and are not comparisons. The new
comparison profiles remain non-publishable candidates even when a run succeeds.
