---
title: "Getting Started"
---

# Getting Started

This guide is for developers, evaluators, and documentation readers who need
to understand ProtocolLab without assuming a specific runner or hosted lab.

## First Reading Path

1. Start with the [overview](overview.md) for the repository purpose and
   boundary.
2. Read [product boundaries](protocol-lab/product-boundaries.md) before
   interpreting any runtime or benchmark result.
3. Use [terminology and policies](terminology-and-policies.md) for shared
   meanings such as scenario, suite, load profile, measurement profile,
   producer, source, scope, provenance, benchmark, diagnostic, and soak.
4. Read the [architecture overview](architecture/overview.md) and
   [lab roles](architecture/lab-roles.md) for the major components.
5. Use [scenario authoring](scenarios/authoring-guide.md), the
   [scenario catalog](scenarios/catalog.md), the
   [suite catalog](scenarios/suite-catalog.md), and the
   [load-profile catalog](scenarios/load-profile-catalog.md) to understand
   selected work.
6. Read [run plan v1](lab/run-plan-v1.md) and
   [package v2](lab/package-v2.md) for package-backed selection and
   provenance.
7. Use [measurement model](architecture/measurement-model.md),
   [artifact model](architecture/artifact-model.md), and
   [report model](architecture/report-model.md) when interpreting evidence.
8. Use [traceability](../specs/traceability/README.md) when a change needs to
   connect requirements, architecture, verification, schemas, and fixtures.

## Repository Layout

| Path | Purpose |
| --- | --- |
| `docs/` | Public documentation and explanatory guides. This folder is mirrored to the central Incursa docs repository. |
| `docs/architecture/` | Architecture notes for public contract components and evidence models. |
| `docs/lab/` | Package and run-plan contract documentation. |
| `docs/protocol-lab/` | Product boundary, vision, and roadmap docs. |
| `docs/reports/` | Publication bundle, public report, and safety documentation. |
| `docs/scenarios/` | Scenario, suite, and load-profile authoring and catalog docs. |
| `specs/` | Canonical SpecTrace JSON requirements, architecture, work items, verification, and traceability indexes. |
| `schemas/` | JSON Schema contract definitions for public payloads. |
| `fixtures/public-contracts/` | Declarative valid, invalid, and incompatible examples. |
| `scenarios/` | Public scenario definitions. |
| `suites/` | Public suite definitions. |
| `load-profiles/` | Public load-profile definitions. |
| `.github/workflows/validate.yml` | Repository-health validation for JSON, YAML, links, schemas, traceability, and public-repository boundaries. |
| `.github/workflows/sync-docs.yml` | Documentation mirroring into the central `incursa-docs` repository. |

## Validation Expectations

The committed repository-health workflow is the authoritative automation for
this repository. It parses JSON and YAML, checks local Markdown links, checks
schema identifiers, resolves traceability paths, validates representative
fixtures, and prevents implementation folders or source files from being
introduced.

For local review, use the same expectations even when you do not run the full
GitHub Actions workflow: keep JSON and YAML parseable, keep repository-local
links resolvable, keep fixtures declarative, and keep implementation behavior
outside this repository.

## Documentation Mirroring

ProtocolLab remains the authoritative source for ProtocolLab documentation.
Changes under `docs/`, `README.md`, `docs.site.json`, or the sync workflow can
trigger the mirror workflow on `main`. The workflow copies `docs/` into the
central `incursa-docs` repository and opens a pull request there. It does not
publish production documentation directly.
