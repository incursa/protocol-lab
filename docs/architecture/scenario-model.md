---
title: "Scenario Model"
---

# Scenario Model

A scenario is the public catalog artifact for one durable protocol test case.
It describes behavior, protocol lane, traffic shape, validation expectations,
and artifact expectations without naming a concrete implementation or runner.

## Responsibilities

Scenarios define:

- stable scenario identity
- protocol family
- role and endpoint expectations
- request and response semantics
- validation expectations
- artifact expectations

Protocol-specific behavior should be visible in the scenario identity. HTTP/1,
HTTP/2, HTTP/3, and QUIC scenarios use separate namespaces such as `http1.*`,
`http2.*`, `http3.*`, and `quic.*` instead of a generic `http.*` ID that could
be mistaken for all HTTP versions.

HTTP/3 protocol scenarios use `http3.protocol.*` IDs with the `h3` protocol
token. QUIC transport scenarios use `quic.transport.*` IDs with the `quic`
protocol token. Public contracts must keep these lanes distinct even when an
implementation supports both.

QUIC transport scenarios that exercise resumption or 0-RTT should state the
session outcome and replay-safety expectation in validation metadata instead of
relying on runner-local assumptions.

Scenarios do not define:

- implementation IDs
- test-executor IDs
- package hashes
- local hostnames or ports
- private controller policy
- executable validation logic

## Relationship To Suites And Run Plans

Suites group scenarios. Load profiles define intensity. Run plans select
package-pinned components and reference scenarios or suites. Implementations
consume these documents, but the documents remain declarative public
contracts.

## Protocol Family Extensions

Protocol-family extensions use Scenario v2 with `schemaVersion: "2.0"` and
the versioned schema at
[`schemas/scenario/v2/scenario.schema.json`](../../schemas/scenario/v2/scenario.schema.json).
Scenario v1 remains frozen at its existing schema and version value.

Binding-specific v2 scenarios may share a `semanticTemplateId` when they perform
the same operation, but their protocol-specific IDs remain the provenance and
comparison boundary. `protocolStack` records the ordered application-to-
transport stack. Typed `tls`, `grpc`, `dns`, and `websocket` objects hold
family semantics. `executionProfile` records cleartext or TLS mode, protocol
variant, and target role as implementation-neutral comparison metadata.

Activated family scenarios also declare explicit unsupported, unavailable, and
binding-mismatch outcomes. They do not require an operating system, language,
server, client, container runtime, or load generator.
