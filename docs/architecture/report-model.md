# Report Model

Public reports are contract-governed summaries of implementation-produced
evidence. The public repository defines report schemas and claim semantics.

## Claim Boundaries

- `Diagnostic` evidence is useful for troubleshooting.
- `Smoke` evidence is useful for quick behavior checks.
- `Benchmark` evidence requires the provenance and controls claimed by the
  report.
- `Regression` evidence requires consistent profile, environment, scenario,
  and load-shape context.
- `Soak` evidence supports long-duration stability, degradation, memory, and
  error-trend claims.
- `Verified` evidence requires independent attestation and must not be implied
  by ordinary public report publication.

## Public Safety

Reports must preserve validation failures, unsupported outcomes, unavailable
outcomes, diagnostic labels, and non-publishable claim status. They must not
include private paths, credentials, internal hostnames, or private operational
state.

Report schemas live under `schemas/public-report/v1/`.

Measurement profiles and comparability statements come from the measurement
contract layer under `schemas/measurement/v1/`. Raw or derived evidence is
referenced through artifact manifests under `schemas/artifact/v1/`.
