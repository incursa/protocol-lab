# Test Case And Run Plan Model

This document defines the public vocabulary for test cases, scenarios, suites,
load profiles, packages, and run plans. It is contract guidance, not an
implementation workflow.

## Core Terms

| Term | Meaning | Does Not Own |
| --- | --- | --- |
| Test case | The conceptual behavior to observe. | Package selection, endpoints, runtime policy. |
| Scenario | The public catalog document that serializes one test case. | Implementation IDs, executor IDs, package hashes. |
| Suite | A named selection of scenarios for a protocol lane or result intent. | Package SHA pins, implementation versions, claim upgrades. |
| Load profile | A named load-shape contract. | Behavior semantics or executor implementation. |
| Implementation package | A package that advertises target capabilities. | Test selection policy or executor behavior. |
| Test-executor package | A package that advertises tests it can perform. | Target implementation behavior. |
| Toolchain package | A package that advertises shared capabilities. | Scenarios, implementations, or test executors. |
| Run plan | An immutable selector document that pins packages and selects IDs. | New scenario behavior, private flags, or fallback policy. |

## Contract Flow

```text
Test case
  -> scenario
  -> suite
  -> load profile
  -> package metadata
  -> run plan
  -> implementation-owned execution
  -> artifacts and public report
```

The public repository owns the contract documents in this flow. Execution and
artifact production happen in implementation repositories or hosted labs.

## Scenario Rules

Scenarios should define observable behavior and validation expectations. They
must not contain hostnames, ports, private paths, implementation IDs,
test-executor IDs, package versions, package hashes, controller policy, or
fallback policy.

Scenario IDs should be stable and named by behavior, not by product, runtime,
or environment. When a behavior is protocol-specific, the protocol lane should
be explicit in the ID and title, such as `http1.core.plaintext` or
`http2.core.plaintext`.

## Suite Rules

Suites group scenarios. A suite may declare protocol lane, result intent,
compatible test-executor IDs, and labels. It must not pin package hashes or
imply a specific runner implementation.

## Load Profile Rules

Load profiles describe intensity and repetition shape. They do not change the
behavior being tested. A smoke profile and a regression profile can exercise
the same scenario; only the intensity changes.

## Package Rules

Package manifests declare what a package provides. Implementation packages,
test-executor packages, scenario-pack packages, and toolchain packages are
compatible only when their declared IDs, protocols, scenarios, suites, and
capabilities satisfy the selected run plan.

## Run Plan Rules

A run plan pins exact package identities and SHA-256 values, then selects
implementation IDs, test-executor IDs, suite or scenario IDs, protocols, load
profile, and required capabilities.

A run plan must not define new test behavior. It must not silently substitute
another implementation, test executor, scenario, suite, protocol lane, package
version, or load profile.

## Outcomes

- `valid`: selected behavior was satisfied.
- `invalid`: selected behavior was attempted and failed validation.
- `unsupported`: a selected package understands the request shape but does not
  claim the selected behavior.
- `unavailable`: a selected dependency, package, capability, endpoint, or
  resource is absent.
- `infrastructure failed`: execution infrastructure failed independently of
  target correctness.

Unsupported and unavailable outcomes are first-class results, not permission to
fall back.
