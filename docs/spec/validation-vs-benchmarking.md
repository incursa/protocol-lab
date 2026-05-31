# Validation vs Benchmarking

## Purpose

ProtocolLab separates correctness validation from performance benchmarking.
Validation establishes that a target is acceptable for a scenario.
Benchmarking measures load after validation passes.

## Rules

1. Validation runs first. If validation fails, benchmark data must be rejected.
2. Validation checks reachability, status, body, headers, protocol
   negotiation, and stream behavior where applicable.
3. Protocol proof is validation, not benchmarking.
4. Managed-lab H3 load generation is local measurement, not external-reference
   evidence.
5. Docker h2load can produce external-reference-local evidence when the image
   proves `--h3`.
6. Local results are not publishable benchmark evidence.
7. Unsupported or unavailable load-tool combinations must be reported honestly
   instead of fabricating benchmark data.

## Evidence Classes

ProtocolLab uses evidence classes so reports do not blur validation and
benchmarking outcomes:

- `local-smoke`
- `local-lab`
- `external-reference-local`
- `isolated-host`
- `publishable`

Evidence class is a per-result concept. It describes the environment and
collection context of a cell.

## Report Claim Levels

Reports also carry a claim level:

- `DiagnosticOnly`
- `Validation`
- `Regression`
- `Benchmark`
- `Verified`

Claim level is a report-level concept. It describes what a report is allowed
to assert after results, repetitions, evidence, and provenance are taken into
account.

Public/community runs must not fabricate controlled provenance or publishable
status. The gating logic is explicit: a report only becomes publishable when
the underlying execution profile, repetitions, and metadata satisfy the claim
derivation rules. In the current public derivation path, `Benchmark` is the
publishable claim that can be emitted; `Verified` remains reserved for future
controlled/private attestation.

## Related

- [Measurement Model](../architecture/measurement-model.md) - execution profile, effective load shape, and provenance boundaries
- [Report Model](../architecture/report-model.md) - claim levels and evidence interpretation
- [Load Tools](load-tools.md) - load generator behavior and capability checks
- [Fairness Rules](fairness-rules.md) - load-shape and measurement fairness constraints
