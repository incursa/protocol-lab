# Public Contract Fixtures

These fixtures are declarative examples for contract readers and validators.
They are not production adapters, production test executors, benchmark targets,
scripts, binaries, or runnable packages.

## Package Fixtures

Package fixtures under `packages/` show the expected Package v2 manifest
layout:

- neutral implementation metadata
- neutral test-executor metadata
- neutral scenario-pack metadata
- neutral toolchain metadata
- invalid package metadata that must fail admission

The fixture package directories intentionally contain metadata and contract
documents only. They do not declare executable entrypoints, local launch
commands, runtime prerequisites, generated SDKs, or implementation-owned
collection mechanics.

## Run Plan Fixtures

Run plan examples live under:

- `run-plans/valid`
- `run-plans/invalid`
- `run-plans/incompatible`

Valid plans pin package bytes and select package-provided IDs. Schema-invalid
plans omit required work selection, omit package hashes, or inline scenario
behavior. Incompatible plans are structurally valid but select components that
the referenced package set cannot satisfy.

## Core Contract Fixtures

Focused core contract examples live under:

- `adapter/valid` and `adapter/invalid`
- `test-executor/valid` and `test-executor/invalid`
- `scenarios/valid` and `scenarios/invalid`
- `suites/valid` and `suites/invalid`
- `load-profiles/valid` and `load-profiles/invalid`

These fixtures are intentionally small. Each invalid fixture demonstrates one
clear validation failure such as a missing required identity, selector, purpose,
or scenario list.

## Measurement And Artifact Fixtures

Measurement examples live under:

- `measurement/valid`
- `measurement/invalid`

Artifact examples live under:

- `artifacts/valid`
- `artifacts/invalid`

Valid measurement fixtures show runner-only smoke evidence, benchmark latency
and load-generator summaries, implementation-provided custom telemetry, and an
optional external telemetry producer example. Invalid measurement fixtures show
missing contract versions, missing metric names, missing source/scope/provenance,
undisclosed high-overhead benchmark telemetry, and telemetry bundles that try
to claim conformance status.

Valid artifact fixtures show hash-addressable raw artifact references and
sanitized public report artifacts. Invalid artifact fixtures show missing
hashes and contradictory redaction/sensitivity declarations.

## Public Report Fixtures

Public report examples live under:

- `public-reports/valid`
- `public-reports/invalid`

Valid public report fixtures show the minimal shareable evidence-report shape.
Invalid public report fixtures demonstrate required contract-version failures.

## Protocol Family Fixtures

The HTTP/1 fixture set has separate run plans for `conformance-smoke` and
`benchmark-smoke`. Both select public test cases, but they carry different
suite/result metadata.

The HTTP/2 fixture set follows the same spec-first shape: scenarios are
defined before any controller, site, or producer-package behavior can select
them.
