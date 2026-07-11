---
title: "Scenario Model"
---

# Scenario Model

A scenario serializes one public test case. It is implementation-neutral and
does not select packages, runners, adapters, or test executors.

## Scenario Owns

- stable scenario ID
- protocol lane
- behavior description
- request or transport shape
- validation expectations
- artifact expectations
- status and tags

## Scenario Does Not Own

- package references
- implementation IDs
- test-executor IDs
- hostnames or ports
- private controller policy
- fallback behavior
- runtime-specific validation logic

When the behavior is tied to a concrete HTTP protocol version, the scenario ID
and title should include that lane. For example, an HTTP/1-only plaintext
scenario should use an ID such as `http1.core.plaintext`; an HTTP/2 variant
should use a separate `http2.*` ID rather than relying on a generic `http.*`
name.

HTTP/3 uses `http3.*` scenario IDs and the `h3` protocol token. QUIC transport
uses `quic.*` scenario IDs and the `quic` protocol token. These families are
related at the wire level but are separate public scenario families.

QUIC transport scenarios that exercise resumption or 0-RTT should state the
session outcome and replay-safety expectation in validation metadata instead of
relying on runner-local assumptions.

## Status

- `stable`: public contract is expected to remain compatible.
- `experimental`: contract may change.
- `placeholder`: future scenario; no implementation support is implied.

## Schema Version

Scenario v1 documents currently use `schemaVersion: "1.0"`. Keep that value
for v1 compatibility. A future move to a named string such as
`protocol-lab.scenario.v1` should be treated as an explicit compatibility
decision, not a mechanical cleanup.

## Relationship To Other Documents

Suites group scenarios. Load profiles describe intensity. Run plans select
package-pinned components and scenarios or suites. Reports describe evidence
produced by implementations.
