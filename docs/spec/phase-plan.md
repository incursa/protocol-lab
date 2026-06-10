# Phase Plan

This plan supersedes the older target-specific roadmap. ProtocolLab is now a
contract-first public repository: product implementations and production test
executors are package producers, not public runner-owned adapter projects.

## Phase 0: Public Contracts

Goal: make the public repository the canonical place for neutral contracts.

Deliverables:

- Adapter Contract v1 under `/protocol-lab/adapter/v1`.
- Test Executor Contract v1 under `/protocol-lab/test-executor/v1`.
- Package v2 schemas and builder validation for `implementation`,
  `test-executor`, `scenario-pack`, and `toolchain`.
- Shared concepts for protocol families, scenario IDs, test IDs,
  capabilities, endpoint bindings, metrics, artifacts, unsupported outcomes,
  and provenance.

Exit criteria:

- Schema tests cover adapter v1, test executor v1, and package v2.
- Public docs describe lab roles, package authoring, compatibility resolution,
  and conformance expectations.
- Public source does not depend on production implementation libraries or
  private infrastructure.

## Phase 1: Public Conformance Harnesses

Goal: let external package authors prove contract compatibility before using a
controller.

Deliverables:

- Adapter conformance suite.
- Test executor conformance suite.
- Fake/reference fixtures for lifecycle, unsupported outcomes, malformed
  responses, metrics, artifacts, and cleanup.
- Architecture guardrails that prevent public runner code from referencing
  concrete product implementation assemblies.

Exit criteria:

- Focused conformance tests run without private repositories.
- Public manifests with local paths resolve inside the public checkout.
- Removed production adapter workflows are not advertised by entry docs.

## Phase 2: Neutral Public Catalog

Goal: keep the public catalog useful without making it the home of production
implementations.

Deliverables:

- Kestrel, Caddy, nginx, and quic-go public reference target manifests.
- Scenario and suite metadata for HTTP and the narrow raw QUIC lane.
- Raw QUIC scenarios limited to `quic.transport.multiplex.100x64kb` and
  `quic.transport.duplex-streams` until validator and artifact gates expand.

Exit criteria:

- Public suite definitions parse and do not select removed production
  implementations.
- Raw QUIC package tooling emits separate test-executor and scenario-pack
  packages.

## Phase 3: Controller/Worker Consumption

Goal: internal controller and worker execution consume public contracts instead
of defining private package semantics.

Deliverables:

- Public package v2 validation in the internal package store.
- Explicit execution-cell materialization from package-provided suites,
  scenarios, implementations, and test executors.
- Dependency-closure validation for explicitly submitted package sets.
- No silent fallback to bundled load tools for package-backed jobs.

Exit criteria:

- Submitted implementation, test-executor, and scenario-pack packages are
  recorded in job provenance.
- Missing executor packages are rejected before scheduling or worker fallback.

## Phase 4: Package Producers

Goal: product repositories own their package templates and proof expectations.

Deliverables:

- `quic-dotnet-dev` HTTP/3 implementation package template.
- `quic-dotnet-raw-dev` raw QUIC implementation package template.
- Package build dry-run tests and controller submission proof.

Exit criteria:

- HTTP/3 and raw QUIC package targets are separate.
- Raw QUIC never falls back to managed HTTP/3 execution.
