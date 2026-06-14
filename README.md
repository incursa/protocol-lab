# Incursa ProtocolLab

ProtocolLab is the public, language-neutral specification and contract
repository for protocol measurement work. It defines the schemas, fixtures,
scenario definitions, suite definitions, artifact contracts, report contracts,
and governance rules that implementations, adapters, test executors, hosted
labs, telemetry bundles, and public reports must satisfy.

This repository is not a runner, command-line tool, SDK, build system, hosted
lab, package publisher, or implementation repository. Concrete runners and
hosted labs are implementations of these public contracts.

## Start Here

1. Read this README for the repository boundary and public contract map.
2. Read [Repository Layout](docs/repository-layout.md) and
   [Terminology And Policies](docs/terminology-and-policies.md) for the
   shared vocabulary and contract policies.
3. Read [Product Boundaries](docs/protocol-lab/product-boundaries.md) to
   understand the public repository versus implementation-owned systems.
4. Use [Schemas](schemas/README.md), [Fixtures](fixtures/public-contracts/README.md),
   and [Specs](specs/README.md) as the canonical contract indexes.
5. Use the [Contract Coverage Matrix](docs/audits/contract-coverage-matrix.md)
   and [Public Consumption Readiness Review](docs/audits/public-consumption-readiness-review.md)
   to see what is stable today and what remains open.

## Repository Scope

The public repository owns:

- canonical SpecTrace JSON requirements, architecture, work-item, and
  verification artifacts
- JSON Schema contracts for public JSON documents
- OpenAPI/YAML contracts where an HTTP control plane is specified
- declarative fixtures for valid, invalid, and incompatible contract examples
- scenario, suite, and load-profile definitions
- documentation for lifecycle, semantics, public/internal boundaries, and
  participation rules
- repository governance files

The public repository does not own executable source code, runnable automation,
private lab operations, local benchmark execution, package materialization,
cloud uploads, implementation packages, or test-executor binaries.

## Contract Surfaces

ProtocolLab contracts are implementation-neutral. A conforming participant can
be written in any language and can run in any environment as long as it
satisfies the published documents and preserves explicit unsupported or
unavailable states.

Primary contract surfaces include:

- Adapter Contract v1 under `schemas/adapter/v1/`
- Test Executor Contract v1 under `schemas/test-executor/v1/`
- Package v2 schemas under `schemas/package/v2/`
- Run Plan v1 under `schemas/run-plan/v1/`
- Measurement and telemetry contracts under `schemas/measurement/v1/`
- Artifact and redaction contracts under `schemas/artifact/v1/`
- Scenario, suite, load-profile, and public report schemas
- Repository terminology and policy notes under `docs/terminology-and-policies.md`
- SpecTrace artifacts under `specs/`

No generated code, SDK package, local executable, or hosted service is the
source of truth for these contracts.

## Schemas

JSON Schema is the default contract format for JSON documents in this
repository. OpenAPI is allowed when the contract is an HTTP control-plane
surface. Schema paths are stable public references and should be used by
runners, adapters, test executors, report consumers, archive importers, and
package validators.

## Fixtures

Fixtures under `fixtures/public-contracts/` provide declarative examples for
contract readers and validators:

- valid examples show accepted public shapes
- invalid examples show schema failures
- incompatible examples show selector or compatibility failures that are
  structurally valid but not admissible

Fixtures are not runnable implementations. They must not contain scripts,
binaries, generated code, or executable source.

## Scenario And Suite Definitions

Scenarios describe durable protocol test cases. Suites group scenarios for a
specific intent such as conformance or benchmark measurement. Load profiles
describe intensity and measurement shape. These documents are declarative
inputs, not instructions to start a local process, build a container, invoke a
tool, or publish a result.

## Measurement And Artifact Contracts

ProtocolLab separates behavior validity, measurement claims, artifact
provenance, and public-report safety. Report contracts define what a result can
claim and what evidence must be present. Public reports must not infer stronger
claims from throughput, duration, implementation names, or private lab state.

Measurement profiles define the claim level that evidence can support:
`smoke`, `diagnostic`, `regression`, `benchmark`, and `soak`. Normalized
telemetry bundles describe reportable samples, summaries, provenance,
warnings, and optional artifact references. Raw artifacts are preserved by
safe, hash-addressable manifests. Implementation-side telemetry is auxiliary
unless a run plan explicitly requires it, and no telemetry backend is canonical
for ProtocolLab.

## Implementation/Test Executor Participation Model

Runners, hosted labs, adapters, implementations, and test executors participate
by consuming the public contracts and producing contract-compliant artifacts.
They may live in internal, third-party, or product-specific repositories. They
must not require private ProtocolLab state to understand the public schemas and
fixtures.

Internal and third-party runners are possible implementations of these
contracts. They may consume this repository. This repository must not depend on
implementation-owned runners, services, private configuration, or private build
outputs.

Internal or third-party labs may run benchmarks, retain private artifacts,
collect diagnostics, operate dashboards, integrate providers, and publish
reports. Those activities are implementations of the public contracts, not
features implemented by this repository.

## Public/Internal Boundary

The dependency direction is one-way:

- internal and external implementations may consume public contracts
- the public repository must not depend on implementation code, hosted lab
  services, private configuration, or private artifacts

See `docs/protocol-lab/product-boundaries.md` for the detailed boundary model.

## Stability And Open Work

The public contract surfaces indexed by
`docs/audits/contract-coverage-matrix.md` are stable enough for
implementation repositories, hosted labs, validators, report consumers, and
package producers to consume as the current public source of truth.

Current open questions are contract refinement choices, not permission to
replace public contracts with implementation behavior. They include whether to
add OpenAPI documents for adapter and test-executor control-plane routes,
whether package component entry manifests need dedicated schemas, and whether
a future public-report version should rename historical fields with migration
guidance.

## Documentation

- `docs/README.md` indexes supporting documentation.
- `docs/repository-layout.md` summarizes the top-level folder purposes.
- `docs/terminology-and-policies.md` defines shared terminology, versioning,
  compatibility, fixture, schema, and SpecTrace usage policies.
- `docs/lab/package-v2.md` describes the package contract.
- `docs/lab/run-plan-v1.md` describes immutable run-plan documents.
- `docs/protocol-lab/product-boundaries.md` defines the public/internal split.
- `docs/architecture/measurement-model.md` summarizes the measurement,
  telemetry, artifact, redaction, and comparability model.
- `specs/requirements/protocol-lab/measurement-requirements.md` and
  `specs/requirements/protocol-lab/artifact-requirements.md` summarize the
  public measurement and artifact rules.
- `specs/traceability/README.md` explains how SpecTrace links requirements to
  schemas and fixtures.
- `schemas/README.md` and `specs/README.md` index the public schema and
  SpecTrace surfaces.
- `docs/audits/contract-coverage-matrix.md` and
  `docs/audits/contract-completeness-validation.md` summarize current
  contract coverage and remaining questions.
- `docs/audits/public-consumption-readiness-review.md` records the final
  public reader and contract-readiness recommendation.
- `AGENTS.md` gives Codex and other agent contributors safe operating
  instructions for this spec-only repository.
- `CODE_OF_CONDUCT.md`, `SUPPORT.md`,
  `docs/contributor-agreement-automation.md`, and `.github/` describe public
  governance and repository-health automation.

## Contributing

Contributions should change contracts, schemas, fixtures, scenarios, suites,
documentation, or governance files. Implementation code and runnable
automation belong in implementation repositories.

Read `CONTRIBUTING.md`, `CONTRIBUTOR-AGREEMENT.md`,
`CODE_OF_CONDUCT.md`, `SUPPORT.md`, and `SECURITY.md` before opening a pull
request.

## License

Apache-2.0. See `LICENSE`.
