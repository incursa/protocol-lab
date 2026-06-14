# Public Specification Release Readiness

Date: 2026-06-14

## Summary

The public ProtocolLab repository is ready to be treated as the public
specification and contract repository. The repository narrative now consistently
states that ProtocolLab is language-neutral; that this repository defines
public contracts, schemas, fixtures, scenarios, suites, load profiles,
measurement, telemetry, artifact, and report contracts; and that runners,
hosted labs, adapters, implementations, test executors, collectors, dashboards,
and publication systems are implementations of those contracts outside this
repository.

The public/internal boundary is explicit: internal or third-party labs may run
benchmarks, retain private artifacts, collect diagnostics, operate dashboards,
integrate providers, and publish reports, but those activities are not
implemented in this public repository.

## Files Changed

- `README.md`
- `CHANGELOG.md`
- `CONTRIBUTOR-AGREEMENT.md`
- `SECURITY.md`
- `.github/ISSUE_TEMPLATE/bug_report.md`
- `.github/ISSUE_TEMPLATE/feature_request.md`
- `docs/README.md`
- `docs/terminology-and-policies.md`
- `docs/protocol-lab/product-boundaries.md`
- `docs/protocol-lab/vision.md`
- `docs/migration/code-moved-to-internal.md`
- `docs/scenarios/catalog.md`
- `docs/architecture/test-executor-contract-v1.md`
- `docs/reports/publication-bundle.md`
- `schemas/public-report/v1/evidence-report-v1.schema.json`
- `schemas/load-profile/load-profile.schema.json`
- `schemas/run-plan/v1/run-plan.schema.json`
- `fixtures/public-contracts/run-plans/valid/*.json`
- `load-profiles/regression.yaml`
- `load-profiles/comparison.yaml`
- `scenarios/http/core/*.yaml`
- `scenarios/http/payload/bytes-1kb.yaml`

## Remaining Risks

- Some historical migration and audit documents intentionally mention removed
  implementation assets so readers can understand what moved out of the public
  repository. These are boundary records, not current instructions.
- Existing scenario descriptions use benchmark-oriented language because
  scenarios can support benchmark evidence when selected by an appropriate run
  plan and measurement profile. They remain declarative and do not define local
  execution mechanics.
- Existing public-report v1 fields preserve compatibility with earlier report
  terminology. Future report versions may choose cleaner names with migration
  guidance.

## Remaining Open Questions

1. Decide whether adapter and test-executor HTTP control-plane routes should
   also be published as OpenAPI documents in addition to JSON Schema payloads
   and Markdown route descriptions.
2. Decide whether package component entry manifests need dedicated schemas
   beyond the Package v2 root manifest schema and declarative fixtures.
3. Decide whether public-report v2 should replace historical field names while
   preserving compatibility guidance for report consumers.

## Recommendation

Ready for public specification repository use.

The repository is internally consistent as a spec-only, language-neutral public
contract repository. The remaining open questions are future contract
refinement choices, not blockers for treating the current repository as the
public ProtocolLab specification surface.
