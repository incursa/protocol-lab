---
title: "Run Plan v1"
---

# Run Plan v1

Run Plan v1 is the public contract for repeatable ProtocolLab work selection.
It pins exact package identities and selects existing package-provided
components. It is not a scenario language, test definition format, executor
extension point, launch format, or hosted controller implementation.

The canonical JSON Schema is [`schemas/run-plan/v1/run-plan.schema.json`](../../schemas/run-plan/v1/run-plan.schema.json).

## Contract Version

Every v1 run plan uses `schemaVersion: "protocol-lab-run-plan-v1"`.

The schema version identifies the public payload shape. Package manifests keep
using `protocol-lab-package-v2`; test executors keep using `test-executor-v1`.

## Required Shape

A run plan includes:

- `runPlanId` and `runPlanVersion`
- `packages`, where every entry pins `packageId`, `packageVersion`, and
  `sha256`
- `implementationIds`
- `testExecutorIds`
- `suiteIds` or `scenarioIds`
- `protocols`
- `loadProfileId`
- `targetMode`
- `targetNetworkMode`
- `requiredCapabilities`

The selected IDs must refer to entries provided by the pinned packages. The run
plan itself does not define or override scenario behavior, tests, load shapes,
executor flags, endpoint details, or implementation behavior.

## Package References

Package references are immutable provenance anchors. Implementations that
resolve a run plan must resolve components from exactly those package
identities and SHA-256 values. They must not silently substitute another
package, implementation, test executor, suite, scenario, protocol, or load
profile.

`packageId` values identify package bytes selected by the plan, but they do
not prove who owns a publisher, namespace, trademark, or package-index prefix.
The lab or package index that admits packages is responsible for namespace
reservation and publisher verification before run plans can safely select
multi-publisher packages.

## Selection Fields

`implementationIds`, `testExecutorIds`, `protocols`, and either `suiteIds` or
`scenarioIds` are explicit selector lists. An implementation may expand suites
into their package-defined scenarios, but it must not infer missing selectors
from names or compatibility guesses.

`loadProfileId` selects an existing load profile. It does not inline load
settings.

`targetMode` describes how the selected target is resolved at the contract
level. The v1 values are `implementation-resolved` and `external`.
`targetNetworkMode` describes endpoint publication at the contract level. The
v1 values are `published-endpoint` and `implementation-defined`. These fields
are routing metadata; they do not prescribe how an implementation is launched,
hosted, isolated, or connected internally.

## Optional Metadata

Run plans may include `displayName`, `repetitions`, `cellOrder`, `comparisonGroups`,
`publicationIntent`, `labels`, `traceReferences`, and `notes`. These fields
describe execution policy or provenance. They do not create new scenario or
test behavior.

## Deterministic Cell Ordering

`cellOrder` selects a deterministic scheduling policy without changing any
scenario, load profile, or comparison-group semantics. `legacy` preserves the
runner's stable implementation-grouped order. `round-robin` interleaves
implementations by repetition within stable comparison groups so temporal drift
is visible instead of being confounded with implementation order.

When `cellOrder` is present, runners must use the declared policy and retain it
in run-plan and per-cell evidence. A runner that does not support the declared
policy must reject the plan explicitly rather than silently substitute another
order.

## Optional Telemetry Requirements

Telemetry export is optional unless a run plan explicitly requires it through
`requiredCapabilities` or another future run-plan contract field. If telemetry
is not explicitly required, adapter, implementation, and test-executor
telemetry export failures are diagnostic. They may reduce evidence quality or
comparability, but they do not change conformance pass/fail status after the
fact.

## Unsupported And Unavailable Outcomes

Run plans select intended work. They do not turn unsupported or unavailable
package combinations into runnable cells. If the selected package set cannot
provide a requested implementation, test executor, suite, scenario, protocol,
endpoint binding, load shape, or worker capability, the implementation must
reject the plan or produce an explicit unsupported or unavailable outcome with
a reason.

Unsupported means a selected package understands the request shape but does
not claim the requested behavior. Unavailable means a required package,
capability, dependency, collector, endpoint, or worker resource is missing at
execution time. Neither outcome authorizes fallback to another implementation,
executor, protocol lane, scenario, package version, or load profile.

Public examples live under [`fixtures/public-contracts/run-plans/valid`](../../fixtures/public-contracts/run-plans/valid/),
[`fixtures/public-contracts/run-plans/invalid`](../../fixtures/public-contracts/run-plans/invalid/), and
[`fixtures/public-contracts/run-plans/incompatible`](../../fixtures/public-contracts/run-plans/incompatible/).
The valid examples include HTTP/1, HTTP/2, HTTP/3, and QUIC selector plans.
