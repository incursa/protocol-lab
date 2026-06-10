# ProtocolLab Lab Roles

ProtocolLab separates the lab from the protocol implementations and from the tools that exercise them.

## Roles

| Role | Owned by | Contract |
| --- | --- | --- |
| Lab controller | Hosted/private lab | Job API and package ingestion. |
| Lab worker | Hosted/private lab | Materializes packages, launches selected components, collects artifacts. |
| Implementation adapter | Implementation package producer | Adapter Contract v1. |
| Test executor | Test package producer | Test Executor Contract v1. |
| Scenario pack | Scenario package producer | Package v2 catalog metadata and scenario/suite manifests. |
| Toolchain package | Lab/operator package producer | Package v2 dependency metadata; execution support is deferred. |

The public repository defines contracts and schemas. It does not need to own production implementations, production test executors, or hosted worker infrastructure.

## Compatibility Inputs

A package-backed job becomes executable only after the controller resolves all of these inputs:

- implementation id
- test executor id
- suite id or scenario id
- protocol family
- required endpoint bindings
- package environment and worker capabilities

If any selected input cannot be satisfied by the submitted packages plus the base catalog, the job is rejected or the cell is marked unsupported. ProtocolLab must not silently choose a different implementation, test executor, or protocol lane.

## Public ID And Selector Rules

ProtocolLab may reserve common IDs for broadly useful scenarios and tests.
Package authors may define namespaced IDs for specialized capabilities.

Public IDs should be stable ASCII tokens that match the package v2 token
shape: start with a letter or digit and then use only letters, digits,
underscore, period, colon, or hyphen. Scenario IDs should be namespaced by
family and behavior, for example `quic.transport.duplex-streams`. Test IDs
name executor-owned checks. A test ID may equal a scenario ID for a one-to-one
executor, but that is an explicit declaration, not an implicit rule.

Selectors must declare what they match. Use `scenario-id` for scenario IDs and
`test-id` for test IDs. Prefixes, wildcards, tags, or custom selector
expressions are metadata that the selected package and lab must both
understand. They must not be inferred from language runtime, implementation
brand, executable name, or hardcoded protocol knowledge in the lab.
