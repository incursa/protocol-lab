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

## Contract Work Areas

- Adapter Contract v1
- Test Executor Contract v1
- Package v2
- Run Plan v1
- Scenario and suite model
- Measurement and artifact contracts
- Public report bundle and index contracts
- Traceability and fixture coverage
- TLS 1.2/1.3, gRPC/H2, secure and classic diagnostic DNS, and
  binding-specific WebSocket performance
  contract maturation

## Deferred Protocol Families

- WebTransport until its HTTP mappings are final standards
- WebRTC and WebRTC DataChannel
- Oblivious HTTP
- gRPC-Web and gRPC over HTTP/3
- Executable MASQUE CONNECT-IP until a deterministic packet fixture and
  package-backed proxy role are approved
- MQTT, AMQP, CoAP, database wire protocols, and media protocols

## Out Of Scope For This Repository

- runner implementation
- command-line tooling
- source packages and binaries
- local benchmark execution
- hosted lab operations
- publication automation
- cloud storage upload workflows
- private diagnostics
