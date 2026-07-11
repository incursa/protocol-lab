<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="./assets/brand/protocol-lab-readme-header-white.svg">
    <img src="./assets/brand/protocol-lab-readme-header.svg" width="430" alt="ProtocolLab">
  </picture>
</p>

# ProtocolLab

ProtocolLab is the public, language-neutral specification and contract
repository for protocol measurement work. It defines the schemas, fixtures,
scenario definitions, suite definitions, artifact contracts, report contracts,
and governance rules that implementations, adapters, test executors, hosted
labs, telemetry bundles, and public reports must satisfy.

This repository is not a runner, command-line tool, SDK, build system, hosted
lab, package publisher, or implementation repository. Concrete runners and
hosted labs are implementations of these public contracts.

The Incursa-hosted lab tester is available at
[lab.incursa.com](https://lab.incursa.com/). That hosted lab is an
implementation of these contracts; this repository remains the public source
of truth for the contracts.

## Documentation And Mirroring

The source documentation for this repository lives under [`docs/`](docs/).
The docs site manifest in [`docs.site.json`](docs.site.json) and the mirror
workflow in [`.github/workflows/sync-docs.yml`](.github/workflows/sync-docs.yml)
copy that tree into the central `incursa-docs` repository and open a pull
request there.

Do not edit the mirrored `incursa-docs` copy directly. Make source changes in
this repository and let the sync workflow publish the mirror update.

## Start Here

1. Read this README for the repository boundary and public contract map.
2. Read [Terminology And Policies](docs/terminology-and-policies.md) for the
   shared vocabulary and contract policies.
3. Read [Product Boundaries](docs/protocol-lab/product-boundaries.md) to
   understand the public repository versus implementation-owned systems.
4. Use [Schemas](schemas/README.md), [Fixtures](fixtures/public-contracts/README.md),
   and [Specs](specs/README.md) as the canonical contract indexes.
5. Use the [Contract Coverage Matrix](docs/contracts/coverage-matrix.md) to
   see the current public contract surface coverage.

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

- Adapter Contract v1 under [`schemas/adapter/v1/`](schemas/adapter/v1/)
- Test Executor Contract v1 under
  [`schemas/test-executor/v1/`](schemas/test-executor/v1/)
- Package v2 schemas under [`schemas/package/v2/`](schemas/package/v2/)
- Run Plan v1 under [`schemas/run-plan/v1/`](schemas/run-plan/v1/)
- Measurement and telemetry contracts under
  [`schemas/measurement/v1/`](schemas/measurement/v1/)
- Artifact and redaction contracts under
  [`schemas/artifact/v1/`](schemas/artifact/v1/)
- Scenario, suite, load-profile, and public report schemas
- Repository terminology and policy notes under
  [`docs/terminology-and-policies.md`](docs/terminology-and-policies.md)
- SpecTrace artifacts under [`specs/`](specs/)

No generated code, SDK package, local executable, or hosted service is the
source of truth for these contracts.

## Release And Versioning

ProtocolLab versioning is surface-specific:

- schema directories encode contract versions such as Adapter Contract v1,
  Test Executor Contract v1, Package v2, Run Plan v1, and Public Report v1
- compatibility changes must update the matching SpecTrace artifacts,
  schemas, fixtures, and coverage matrix together
- this repository does not publish binaries, SDKs, or hosted services

If a change affects contract compatibility, update the relevant public docs
and traceability surfaces in the same change set.

## Schemas

JSON Schema is the default contract format for JSON documents in this
repository. OpenAPI is allowed when the contract is an HTTP control-plane
surface. Schema paths are stable public references and should be used by
runners, adapters, test executors, report consumers, archive importers, and
package validators.

## Fixtures

Fixtures under
[`fixtures/public-contracts/`](fixtures/public-contracts/) provide declarative
examples for contract readers and validators:

- valid examples show accepted public shapes
- invalid examples show schema failures
- incompatible examples show selector or compatibility failures that are
  structurally valid but not admissible

Fixtures are not runnable implementations. They must not contain scripts,
binaries, generated code, or executable source.

## Scenario And Suite Definitions

Scenarios describe durable protocol test cases. Suites group scenarios for a
specific intent such as conformance, benchmark, diagnostic, regression, or
soak evidence. Load profiles describe intensity and measurement shape. These
documents are declarative inputs, not instructions to start a local process,
build a container, invoke a tool, or publish a result.

The public protocol families are indexed in the
[Scenario Catalog](docs/scenarios/catalog.md). Suite selectors are indexed in
the [Suite Catalog](docs/scenarios/suite-catalog.md), and load-profile
definitions are indexed in the
[Load Profile Catalog](docs/scenarios/load-profile-catalog.md).

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

See [`docs/protocol-lab/product-boundaries.md`](docs/protocol-lab/product-boundaries.md) for the detailed boundary model.

## Validation

The committed repository-health workflow in
[`.github/workflows/validate.yml`](.github/workflows/validate.yml) is the
authoritative validation definition for this repository.

For local review, keep at least the same checks green:

```powershell
git diff --check
```

The workflow also parses JSON and YAML, checks repository-local Markdown
links, validates schema IDs, resolves traceability paths, and rejects
implementation files and folders.

## Readiness And Gaps

The public contract surfaces indexed by
[`docs/contracts/coverage-matrix.md`](docs/contracts/coverage-matrix.md)
are stable enough for implementation repositories, hosted labs, validators,
report consumers, and package producers to consume as the current public
source of truth.

Current open questions are contract refinement choices, not permission to
replace public contracts with implementation behavior. They include whether to
add OpenAPI documents for adapter and test-executor control-plane routes,
whether package component entry manifests need dedicated schemas, and whether
a future public-report version should rename historical fields with migration
guidance.

## Documentation

- [`docs/overview.md`](docs/overview.md) explains ProtocolLab's purpose,
  supported work, stable areas, and current open work.
- [`docs/getting-started.md`](docs/getting-started.md) gives a practical
  reader path, repository layout, validation expectations, and docs mirroring
  notes.
- [`docs/benchmark-workflow.md`](docs/benchmark-workflow.md) explains the
  public configure, execute, collect, interpret, and publish workflow for
  benchmark and experiment evidence.
- [`docs/README.md`](docs/README.md) indexes supporting documentation.
- [`docs/terminology-and-policies.md`](docs/terminology-and-policies.md)
  defines shared terminology, versioning, compatibility, fixture, schema, and
  SpecTrace usage policies.
- [`docs/lab/package-v2.md`](docs/lab/package-v2.md) describes the package
  contract.
- [`docs/lab/run-plan-v1.md`](docs/lab/run-plan-v1.md) describes immutable
  run-plan documents.
- [`docs/protocol-lab/product-boundaries.md`](docs/protocol-lab/product-boundaries.md)
  defines the public/internal split.
- [`docs/architecture/measurement-model.md`](docs/architecture/measurement-model.md)
  summarizes the measurement, telemetry, artifact, redaction, and
  comparability model.
- [`specs/requirements/measurement-requirements.md`](specs/requirements/measurement-requirements.md)
  and
  [`specs/requirements/artifact-requirements.md`](specs/requirements/artifact-requirements.md)
  summarize the public measurement and artifact rules.
- [`specs/traceability/README.md`](specs/traceability/README.md) explains how
  SpecTrace links requirements to schemas and fixtures.
- [`schemas/README.md`](schemas/README.md) and
  [`specs/README.md`](specs/README.md) index the public schema and SpecTrace
  surfaces.
- [`docs/contracts/coverage-matrix.md`](docs/contracts/coverage-matrix.md)
  summarizes current contract coverage and remaining questions.
- [`AGENTS.md`](AGENTS.md) gives Codex and other agent contributors safe
  operating instructions for this spec-only repository.
- [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md), [`SUPPORT.md`](SUPPORT.md),
  and [`.github/`](.github/) describe public governance and repository-health
  automation.

## Contributing

Contributions should change contracts, schemas, fixtures, scenarios, suites,
documentation, or governance files. Implementation code and runnable
automation belong in implementation repositories.

Read [`CONTRIBUTING.md`](CONTRIBUTING.md),
[`CONTRIBUTOR-AGREEMENT.md`](CONTRIBUTOR-AGREEMENT.md),
[`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md), [`SUPPORT.md`](SUPPORT.md), and
[`SECURITY.md`](SECURITY.md) before opening a pull request.

## Brand Assets

Finished ProtocolLab marks, lockups, repository artwork, favicons, and design
tokens are under [`assets/brand/`](assets/brand/). Use the standard mark at
64 px and above and the simplified `mark-small` variant at 48 px and below.
The [branding package](docs/branding/README.md) records the approved identity,
asset choices, and usage guidance.

These official name and logo assets are governed separately from the
open-source repository content. See
[`BRAND-ASSET-LICENSE.md`](BRAND-ASSET-LICENSE.md) and
[`TRADEMARKS.md`](TRADEMARKS.md).

## License

The repository's code and documentation are licensed under Apache-2.0. See
[`LICENSE`](LICENSE). The ProtocolLab name, Measurement Gate logo and symbol,
and files under [`assets/brand/`](assets/brand/) are separate proprietary
brand assets and are not licensed under Apache-2.0.
