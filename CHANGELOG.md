# Changelog

## public-report-process-capacity - 2026-07-12

- Added optional `logicalProcessorCount` process telemetry so public evidence
  can disclose the processor capacity used by bounded saturation analysis.

## public-spec-repository - 2026-06-14

- Repositioned ProtocolLab as a public, language-neutral specification and
  contract repository.
- Preserved public contract assets for schemas, fixtures, scenarios, suites,
  load profiles, measurement, telemetry, artifacts, public reports, and
  SpecTrace traceability.
- Removed public implementation, runner, script, workflow, package publication,
  and local execution surfaces from the repository boundary.
- Added public contract coverage, schema, fixture, SpecTrace, and governance
  documentation.

## historical-local-operational-state - 2026-05-27

- Earlier public repository snapshots included local operational runner and
  benchmark assets.
- Those assets are no longer part of the public repository boundary. Runner,
  hosted lab, diagnostics, package materialization, retained artifact, and
  publication behavior now belongs in implementation repositories.
