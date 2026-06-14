# Public Repository Structure Audit

Date: 2026-06-14

## Summary

The public ProtocolLab repository was reviewed as a specification-only,
language-neutral contract repository. The current structure is organized around
documentation, SpecTrace JSON artifacts, JSON Schemas, declarative fixtures,
scenario definitions, suite definitions, and load profile definitions.

This pass added missing repository indexes and policy documentation, clarified
folder purposes, removed stale runtime-oriented contract fields from Package v2
fixtures and schemas, and normalized terminology that implied local tools or
execution mechanics.

## Repository Structure Reviewed

| Path | Purpose |
| --- | --- |
| `README.md` | Public entry point, repository scope, contract surface index, and public/internal boundary. |
| `LICENSE` | Repository license. |
| `SECURITY.md` | Security reporting policy for public contract artifacts. |
| `CONTRIBUTING.md` | Contribution guidance for public specifications, schemas, fixtures, and documentation. |
| `CONTRIBUTOR-AGREEMENT.md` | Contributor agreement. |
| `CHANGELOG.md` | Public contract change history. |
| `.editorconfig` | Neutral editor formatting guidance. |
| `.gitignore` | Repository hygiene for local/editor artifacts. |
| `.github/` | Issue and pull request templates only; no validation or build workflows. |
| `docs/` | Support documentation, architecture notes, policies, reports, migration notes, and audits. |
| `specs/` | Canonical SpecTrace JSON artifacts and traceability documentation. |
| `schemas/` | JSON Schema and OpenAPI/YAML contract definitions. |
| `fixtures/public-contracts/` | Declarative valid, invalid, and incompatible examples for public contracts. |
| `scenarios/` | Declarative scenario definitions and scenario-family examples. |
| `suites/` | Declarative suite selections and suite metadata. |
| `load-profiles/` | Declarative load profile examples. |

No `src/`, `tests/`, `servers/`, `tools/`, `scripts/`, `runner/`, `cli/`, or
similar implementation/runtime folder remains in the public repository.

## Files And Folders Removed Or Moved

No additional implementation folder had to be removed during this pass. The
earlier conversion removed implementation/runtime assets and documented that
boundary in `docs/migration/code-moved-to-internal.md`.

This cleanup did make contract-surface changes:

- Removed runtime environment and executable entrypoint requirements from the
  Package v2 schema and public package fixtures.
- Rewrote package fixture component manifests as metadata-only implementation,
  test-executor, scenario-pack, and toolchain examples.
- Normalized legacy load-generation terminology to `load-generator` where it
  described public contract evidence.
- Normalized run-plan target metadata from execution-flavored values to
  contract-level values such as `implementation-resolved`,
  `published-endpoint`, and `implementation-defined`.
- Added `specs/README.md`, `schemas/README.md`, and
  `docs/terminology-and-policies.md`.
- Updated `README.md`, `docs/README.md`, `docs/repository-layout.md`,
  `fixtures/public-contracts/README.md`, and run-plan/adapter documentation.

## Contract Areas Confirmed

| Contract area | Confirmed location |
| --- | --- |
| Repository scope and public/internal boundary | `README.md`, `docs/protocol-lab/product-boundaries.md` |
| ProtocolLab terminology | `docs/terminology-and-policies.md` |
| Package contracts | `docs/lab/package-v2.md`, `schemas/package/v2/`, `specs/requirements/protocol-lab/SPEC-PL-LAB-PACKAGE-V2.json` |
| Implementation and adapter participation model | `docs/architecture/adapter-model.md`, `docs/architecture/adapter-contract-v1.md`, `schemas/adapter/v1/` |
| Test executor participation model | `docs/architecture/test-executor-contract-v1.md`, `schemas/test-executor/v1/` |
| Scenario definitions | `docs/scenarios/`, `scenarios/`, `schemas/scenario.schema.json` |
| Suite definitions | `suites/`, `schemas/suite/v1/` |
| Load profile definitions | `docs/architecture/load-model.md`, `load-profiles/`, `schemas/load-profile/load-profile.schema.json` |
| Run plan definitions | `docs/lab/run-plan-v1.md`, `schemas/run-plan/v1/` |
| Result, evidence, and report contracts | `docs/architecture/report-model.md`, `docs/reports/`, `schemas/public-report/v1/`, `specs/requirements/protocol-lab/SPEC-PL-REPORT.json` |
| Measurement and telemetry contracts | `docs/architecture/measurement-model.md`, `schemas/measurement/v1/`, `specs/requirements/protocol-lab/measurement-requirements.md`, `specs/requirements/protocol-lab/SPEC-PL-MEASUREMENT-TELEMETRY.json` |
| Artifact bundle contracts | `docs/architecture/artifact-model.md`, `schemas/artifact/v1/`, `specs/requirements/protocol-lab/artifact-requirements.md`, `specs/requirements/protocol-lab/SPEC-PL-ARTIFACTS.json` |
| Redaction and sanitization states | `schemas/artifact/v1/redaction-state.schema.json`, `docs/terminology-and-policies.md` |
| Comparability classes | `schemas/measurement/v1/comparability-class.schema.json`, `docs/terminology-and-policies.md` |
| Versioning and compatibility policy | `docs/terminology-and-policies.md`, `specs/README.md`, `schemas/README.md` |
| Fixture policy | `fixtures/public-contracts/README.md`, `docs/terminology-and-policies.md` |
| Schema policy | `schemas/README.md`, `docs/terminology-and-policies.md` |
| SpecTrace usage policy | `specs/README.md`, `specs/traceability/README.md`, `specs/traceability/trace-links.json` |

## Gaps Fixed

- Added a documentation index for canonical SpecTrace artifacts in
  `specs/README.md`.
- Added a schema organization and compatibility index in `schemas/README.md`.
- Added shared terminology, versioning, compatibility, schema, fixture,
  SpecTrace, and neutrality policy guidance in
  `docs/terminology-and-policies.md`.
- Updated public indexes so measurement, telemetry, artifact, schema, fixture,
  and traceability contracts are discoverable from the root README and docs
  index.
- Clarified that package fixtures and package schemas are declarative contract
  metadata, not executable package manifests.
- Clarified that run-plan target fields are routing and selection metadata, not
  instructions for a runner to launch or host anything.
- Clarified that adapter execution backends are implementation-owned and do not
  change the public adapter contract.

## Remaining Open Questions

- Package component manifests are represented as declarative YAML fixtures.
  A future contract pass may decide whether those component manifest shapes
  need dedicated JSON Schema coverage.
- Public report fields that historically used legacy load-generation
  terminology were normalized to `load-generator`. If external consumers have
  already adopted the older names, a future compatibility note may be useful.
