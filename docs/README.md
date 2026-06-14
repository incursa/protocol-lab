# ProtocolLab Documentation

This documentation supports the public ProtocolLab contract repository. The
canonical normative artifacts are SpecTrace JSON under `specs/`, JSON Schemas
under `schemas/`, OpenAPI/YAML control-plane contracts where present, and
declarative fixtures under `fixtures/public-contracts/`.

## Project Direction

- `protocol-lab/product-boundaries.md` - public contract layer versus
  implementation layer
- `protocol-lab/vision.md` - project purpose and principles
- `protocol-lab/roadmap.md` - contract-surface roadmap
- `protocol-lab/first-public-release-checklist.md` - contract repository
  readiness checklist
- `protocol-lab/public-repo-readiness.md` - public repository readiness state
- `repository-layout.md` - top-level public repository layout and folder
  purposes
- `terminology-and-policies.md` - shared terminology, versioning,
  compatibility, schema, fixture, SpecTrace, and neutrality policies

## New Reader Map

- What ProtocolLab is: `../README.md` and `protocol-lab/vision.md`
- What this public repository contains: `repository-layout.md`
- What this public repository intentionally excludes:
  `protocol-lab/product-boundaries.md`
- Implementation and test-executor participation:
  `architecture/adapter-contract-v1.md` and
  `architecture/test-executor-contract-v1.md`
- Packages and run plans: `lab/package-v2.md` and `lab/run-plan-v1.md`
- Scenarios, suites, and load profiles: `architecture/scenario-model.md`,
  `architecture/test-case-run-plan-model.md`, and `architecture/load-model.md`
- Measurement profiles, telemetry bundles, artifact bundles, and redaction:
  `architecture/measurement-model.md` and `architecture/artifact-model.md`
- Evidence and public reports: `architecture/report-model.md` and `reports/`
- Versioning, compatibility, schemas, fixtures, and SpecTrace:
  `terminology-and-policies.md`, `../schemas/README.md`, and
  `../specs/README.md`
- Current stable surfaces and open questions:
  `audits/contract-coverage-matrix.md` and
  `audits/public-consumption-readiness-review.md`

## Architecture And Contracts

- `architecture/overview.md`
- `architecture/lab-roles.md`
- `architecture/test-case-run-plan-model.md`
- `architecture/scenario-model.md`
- `architecture/adapter-contract-v1.md`
- `architecture/test-executor-contract-v1.md`
- `architecture/load-model.md`
- `architecture/artifact-model.md`
- `architecture/measurement-model.md`
- `architecture/report-model.md`
- `lab/package-v2.md`
- `lab/run-plan-v1.md`
- `../specs/requirements/protocol-lab/measurement-requirements.md`
- `../specs/requirements/protocol-lab/artifact-requirements.md`
- `../specs/architecture/protocol-lab/measurement-and-telemetry-model.md`
- `../specs/verification/protocol-lab/measurement-contract-verification.md`
- `../schemas/README.md`
- `../specs/README.md`

## Scenarios And Fixtures

- `scenarios/authoring-guide.md`
- `scenarios/catalog.md`
- `scenarios/scenario-model.md`
- `../fixtures/public-contracts/README.md`
- `../fixtures/public-contracts/measurement/valid/`
- `../fixtures/public-contracts/measurement/invalid/`
- `../fixtures/public-contracts/artifacts/valid/`
- `../fixtures/public-contracts/artifacts/invalid/`

## Reports

- `reports/publication-bundle.md`
- `reports/publication-handoff.md`
- `reports/public-report-safety.md`

## Traceability

- `../specs/README.md`
- `../specs/traceability/README.md`
- `../specs/traceability/trace-links.json`

## Governance

- `../CONTRIBUTING.md`
- `../CONTRIBUTOR-AGREEMENT.md`
- `contributor-agreement-automation.md`
- `../CODE_OF_CONDUCT.md`
- `../SUPPORT.md`
- `../AGENTS.md`
- `../SECURITY.md`

## Audits

- `audits/public-repository-structure-audit.md`
- `audits/contract-coverage-matrix.md`
- `audits/contract-completeness-validation.md`
- `audits/public-consumption-readiness-review.md`
- `audits/public-spec-release-readiness.md`

Implementation code, executable validation commands, local benchmark workflows,
and hosted lab operations belong outside this public repository.
