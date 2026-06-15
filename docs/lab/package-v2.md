# ProtocolLab Component Package v2

Component Package v2 is the public manifest contract for separately versioned
implementation packages, test-executor packages, scenario-pack packages, and
toolchain packages. The contract identifies package bytes and the public
catalog entries they provide. It does not make this repository a package
producer or package runner.

The canonical JSON Schema is [`schemas/package/v2/package.schema.json`](../../schemas/package/v2/package.schema.json).

## Component Kinds

| Kind | Purpose | Required provided metadata |
| --- | --- | --- |
| `implementation` | Describes one or more target implementations. | `providedImplementations` |
| `test-executor` | Describes one or more test executors. | `providedTestExecutors` |
| `scenario-pack` | Provides scenarios and/or suites. | `providedScenarios` or `providedSuites` |
| `toolchain` | Declares shared capabilities required by other packages. | `dependencies.requiredCapabilities` |

Package manifests are declarative. A concrete package archive may contain
runnable payloads in an implementation repository or package store, but this
public repository contains only contract examples.

Package manifests in this repository describe component inventory and
compatibility metadata. They do not define executable entrypoints, local
launch commands, runtime collectors, or package execution mechanics.

## Identity And Paths

Package IDs, implementation IDs, test-executor IDs, scenario IDs, suite IDs,
and capability names are stable public contract keys. Entry manifest paths are
package-relative and must not be absolute, drive-qualified, or path traversal
paths.

Package IDs are identity keys, not ownership proof. A manifest that uses a
familiar company, project, lab, product, or package-index prefix does not by
itself establish authority to publish under that name.

Packages intended for publication outside a single controlled lab should use
an owner-scoped ID that the publisher can defend, such as a verified DNS name,
organization account, or lab-reserved prefix. Labs and package indexes that
admit packages from more than one publisher need their own namespace
reservation or publisher-verification policy before accepting reserved,
first-party, organizational, or otherwise confusing prefixes.

The `protocol-lab-` prefix is reserved for ProtocolLab-owned package examples
and contract fixtures in this repository. Neutral fixture IDs are examples
only; they are not registered public package names.

Entry manifest layout is part of the contract:

- implementation packages use `implementations/`
- test-executor packages use `test-executors/`
- scenario-pack packages use [`scenarios/`](../../scenarios/) and/or [`suites/`](../../suites/)

The package manifest declares what a package provides. A run plan declares
which package-provided components should be used for a repeatable run.

## Compatibility Semantics

A package-backed job is compatible only when the selected inputs can be
satisfied by the referenced packages:

- the selected implementation ID is provided by an implementation package
- the selected test-executor ID is provided by a test-executor package
- the selected suite or scenario is provided by a scenario-pack package
- selected protocols intersect across implementation, executor, suite, and
  scenario declarations
- required capabilities are available to the runner or hosted lab

Implementations must reject or report incompatible selections explicitly. They
must not silently substitute another implementation, test executor, protocol
lane, suite, scenario, package version, or load profile.

## Declarative Fixtures

Neutral examples live under [`fixtures/public-contracts/packages/`](../../fixtures/public-contracts/packages/). They show
manifest shape and compatibility metadata. They do not contain source code,
scripts, binaries, or runnable package payloads.

Invalid package fixtures live under [`fixtures/public-contracts/packages/invalid`](../../fixtures/public-contracts/packages/invalid/).
They exist to show metadata that must not be admitted by an implementation.

## Relationship To Run Plans

Package v2 defines component inventory. Run Plan v1 references exact package
identities and SHA-256 values, then selects implementation IDs, test-executor
IDs, suite or scenario IDs, protocols, load profiles, and execution policy.

Do not encode run-plan semantics inside a package manifest. Do not encode
package implementation details inside a public run plan.
