# Public Consumption Readiness Review

Date: 2026-06-14

Summary recommendation: ready with minor notes.

## Summary

The public ProtocolLab repository is coherent enough for outside readers to
understand it as the public, language-neutral specification and contract
repository. The current tree presents public contracts as the source of truth
for implementations, hosted labs, adapters, test executors, package producers,
telemetry bundles, artifact bundles, and public reports.

The remaining notes are future-contract questions and owner-operated
repository settings, not blockers for public consumption of the current
contract surfaces.

## Files Reviewed

- `README.md`
- `docs/README.md`
- `docs/repository-layout.md`
- `docs/terminology-and-policies.md`
- `docs/protocol-lab/product-boundaries.md`
- `docs/protocol-lab/vision.md`
- `docs/protocol-lab/roadmap.md`
- `docs/protocol-lab/public-repo-readiness.md`
- `docs/lab/package-v2.md`
- `docs/lab/run-plan-v1.md`
- `docs/architecture/*.md`
- `docs/reports/*.md`
- `docs/audits/*.md`
- `specs/README.md`
- `specs/traceability/README.md`
- `specs/requirements/protocol-lab/*.json`
- `specs/architecture/protocol-lab/*.json`
- `specs/verification/protocol-lab/*.json`
- `schemas/README.md`
- `schemas/**/*.schema.json`
- `fixtures/public-contracts/README.md`
- `fixtures/public-contracts/**`
- `scenarios/**`
- `suites/**`
- `load-profiles/**`
- `CONTRIBUTING.md`
- `SECURITY.md`
- `CHANGELOG.md`

## Changes Made

- Added an explicit root README start path for new readers.
- Linked the final readiness review from the root README and docs index.
- Added `producer`, `load generator`, and `publication bundle` to the shared
  terminology table.
- Converted stale audit follow-up wording into open-question wording.
- Normalized schema `$id` values to the
  `https://schemas.incursa.com/protocol-lab/` public schema base.
- Added a schema policy note documenting the public `$id` convention.
- Converted product-boundary related-document entries into working relative
  links.
- Added public governance files: `CODE_OF_CONDUCT.md`, `SUPPORT.md`,
  `CONTRIBUTORS.md`, `AGENTS.md`, `.gitattributes`, and `.github/CODEOWNERS`.
- Added the Incursa contributor agreement workflow using
  `incursa/contributor-agreement-action@v0.1.1` and documented the automation
  in `docs/contributor-agreement-automation.md`.
- Added a lightweight repository-health workflow for public contract hygiene
  only.

## Public Reader Journey Assessment

A new reader can now answer:

- what ProtocolLab is
- what this repository owns
- what this repository intentionally excludes
- how public contracts relate to internal and third-party implementations
- how implementations, adapters, test executors, and package producers
  participate
- where package, scenario, suite, load-profile, run-plan, measurement,
  telemetry, artifact, and report contracts live
- how SpecTrace applies
- what is stable today and what remains open

The root README, docs index, schema index, fixture index, and SpecTrace index
form the primary path.

## Contract Completeness Assessment

The requested public contract surfaces are discoverable through
`docs/audits/contract-coverage-matrix.md`, `schemas/README.md`,
`fixtures/public-contracts/README.md`, and `specs/README.md`.

No major contract area is missing. Open refinements remain for optional OpenAPI
route documents, dedicated schemas for package component entry manifests, and
future public-report field naming.

## README And Docs Assessment

The README is current, concise, public-facing, and language-neutral. Supporting
indexes avoid local execution instructions and point readers to canonical
artifacts rather than implementation-owned behavior.

The docs use `runner`, `hosted lab`, and `implementation` as participant
vocabulary while preserving the boundary that concrete behavior belongs
outside this repository.

## SpecTrace Assessment

Canonical requirements, architecture, work items, and verification records are
JSON artifacts under `specs/`. Markdown files in `specs/` explain model and
usage, but they do not replace the canonical SpecTrace JSON records.

Traceability documentation states that `trace-links.json` is a convenience
index, not a separate traceability convention.

## Schema And Fixture Assessment

Schemas are organized by contract area and version where applicable. Schema
`$id` values now use one public base. Fixtures are declarative, language-neutral
examples and are split into valid, invalid, and incompatible examples where the
distinction matters.

Measurement and artifact fixtures model optional telemetry, producer/source/
scope/provenance separation, redaction state, hash-addressable artifact
references, and public-safe publication constraints.

## Public/Internal Boundary Assessment

The public/internal boundary is clear. This repository owns specifications,
SpecTrace records, schemas, fixtures, scenarios, suites, load profiles, and
public report, measurement, telemetry, artifact, versioning, fixture, schema,
and boundary policies.

Internal or third-party systems own runner implementations, command surfaces,
hosted execution, private diagnostics, runtime collectors, retained artifacts,
package materialization, publication pipelines, provider integrations, and
operational dashboards.

## Remaining Risks

- The contributor agreement workflow requires repository or organization secret
  access to `INCURSA_CONTRIBUTOR_AGREEMENTS_TOKEN` before it can record
  signatures.
- Branch protection or rulesets must be configured by repository owners before
  `Contributor Agreement`, code-owner review, or `Repository Health` can be
  required checks.
- Dedicated schemas for package component entry manifests remain a future
  contract decision.
- Public-report v1 preserves some historical field naming for compatibility.
- Release workflow automation remains intentionally absent because this public
  contract repository does not publish packages, binaries, generated code, or
  public report bundles.

## Remaining Open Questions

1. Should adapter and test-executor HTTP control-plane routes be published as
   OpenAPI documents in addition to JSON Schema payload contracts and Markdown
   route descriptions?
2. Should package component entry manifests receive dedicated schemas?
3. Should a future public-report v2 rename historical fields with explicit
   migration guidance?
4. Which branch rules should require `Contributor Agreement`, code-owner
   review, and `Repository Health` after the workflows have run successfully on
   the protected branch?

## Recommended Next Steps

- Keep this repository as the public source of truth for ProtocolLab
  contracts.
- Require implementation repositories and hosted labs to consume schemas,
  fixtures, and SpecTrace requirements from this repository.
- Track the open contract questions as future specification work.
- Keep validation, runner code, CLI behavior, package materialization, and
  publication automation in implementation repositories.
- Configure `INCURSA_CONTRIBUTOR_AGREEMENTS_TOKEN` and branch protection after
  the governance workflows are merged and proven.
