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
HTTP/2, and HTTP/3 application scenarios use separate namespaces such as
`http1.*`, `http2.*`, and `http3.*` instead of a generic `http.*` ID that could
be mistaken for all HTTP versions.

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
