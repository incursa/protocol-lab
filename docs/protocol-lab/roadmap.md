---
title: "ProtocolLab Roadmap"
---

# ProtocolLab Roadmap

This roadmap tracks public contract work only. Implementation roadmaps belong
in implementation repositories.

## Current Focus

- Keep public schemas stable and language-neutral.
- Maintain valid, invalid, and incompatible public fixtures.
- Keep scenario, suite, and load-profile definitions declarative.
- Preserve explicit unsupported and unavailable states.
- Keep public report contracts honest about evidence and claim levels.
- Maintain SpecTrace JSON requirements, architecture, work items, and
  verification records.
- Make HTTP/2 comparison topology unambiguous before comparison executors are
  admitted.
- Establish TLS 1.3 full-handshake smoke contracts before adding resumed
  handshakes or record-throughput work.

## Contract Work Areas

- Adapter Contract v1
- Test Executor Contract v1
- Package v2
- Run Plan v1
- Scenario and suite model
- Measurement and artifact contracts
- Public report bundle and index contracts
- Traceability and fixture coverage
- Secure DNS transport-lane isolation: DoT, DoH over HTTP/2, DoH over HTTP/3,
  and DoQ are independent lanes even when they reuse one deterministic DNS
  query and answer model.

## Sequencing Notes

The first TLS vertical is `tls.handshake.full` using the public
`plab-single-leaf-p256-v1` certificate profile. Its handshake latency boundary
defines measurement start and end points; it does not impose an acceptance
number. TLS 1.2, session resumption, 0-RTT, and record throughput remain later
contract slices.

A deterministic DNS wire fixture means the exact DNS query and response
message bytes, identifiers, names, types, classes, flags, response code, and
answer data are fixed. It is not a choice among transports. The same DNS
semantic fixture may be bound independently to DoT, DoH/2, DoH/3, and DoQ;
each binding requires its own scenario identity, protocol proof, load profile,
executor capability, and comparison group. Detailed DNS scenarios and
executor work remain deferred to a dedicated secure-DNS contract slice.

## Out Of Scope For This Repository

- runner implementation
- command-line tooling
- source packages and binaries
- local benchmark execution
- hosted lab operations
- publication automation
- cloud storage upload workflows
- private diagnostics
