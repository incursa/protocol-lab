# ADR-0003: Report Claim Levels

## Status

Accepted

## Context

ProtocolLab produces reports that combine functional validation results with
performance measurements. Consumers of these reports have different needs:

- A developer fixing a bug wants diagnostic data — "did the target crash
  under load?"
- A team tracking performance over releases wants regression data — "is this
  release faster or slower than the last one?"
- An organization making technology decisions wants benchmark data — "how
  does implementation X compare to implementation Y?"
- An external audience reviewing claims wants verified data — "can I trust
  these numbers?"

The runner now produces a `summary.md` and `aggregate-results.json` for
every run, and it derives a report-level `ReportClaimLevel` from execution
profile, repetition count, evidence completeness, and load-profile purpose.
Evidence classification (`BenchmarkEvidenceAssessment`) still labels
individual results, but the report now also states what the report is allowed
to claim as a whole.

A report from a 5-second smoke run on a developer laptop carries the same
structural format as a report from a controlled lab benchmark. Without a
claim level, consumers must read evidence classifications and comparability
warnings on every cell to understand what the report is worth.

## Decision

Introduce a `ReportClaimLevel` enum to `Incursa.ProtocolLab.Model` with
these values:

- `DiagnosticOnly` — The report is for debugging or development. No
  performance claims should be made from this data. Any run that produced
  output qualifies.
- `Validation` — The target passed functional validation for the scenario.
  Correctness claims are valid; performance claims are not. Requires
  validation status `passed`.
- `Regression` — Measurements are suitable for detecting performance changes
  between runs. Requires same execution profile across the runs being
  compared, multiple repetitions, and stable environment metadata.
- `Benchmark` — Measurements are suitable for absolute performance claims.
  Requires controlled environment, known resource constraints, attested
  metadata, and multiple stable repetitions.
- `Verified` — Measurements have been independently verified and attested.
  Requires controlled lab execution, retained artifacts, signed metadata,
  and external review.

Rules:

1. The claim level is computed from the run's execution profile, load-profile
   purpose, repetition count, evidence classification, and metadata
   completeness.

2. The claim level is reported prominently — in the markdown summary header,
   the aggregate JSON root, and any future dashboard view.

3. A claim level constrains what a consumer should believe. A
   `DiagnosticOnly` report that happens to show 500K req/s should not be
   cited as a benchmark; the claim level explicitly says it is not one.

4. The claim level of a report can only increase, never decrease, as the
   underlying infrastructure improves. A `Regression` claim cannot be
   downgraded to `DiagnosticOnly` unless the data is found to be invalid.

5. `Verified` is the highest claim level and is reserved for future
   controlled/private attestation. It requires:
   - Execution profile `DedicatedLabBareMetal` or `DedicatedLabContainer`
   - Load profile purpose `publishable-benchmark`
   - Complete environment manifest
   - Retained and accessible artifacts
   - Stable scenario catalog version
   - Recorded runner version
   - Recorded target image digest
   - Multiple stable repetitions
   - External attestation (signature, review record)

6. `Benchmark` is the publishable claim level emitted by the current public
   derivation path. `Verified` remains modeled for future internal/private
   attestation.

### Derivation Logic (Implemented)

```
function DeriveClaimLevel(report):
    if not all results have validation passed:
        return DiagnosticOnly

    if executionProfile is LocalProcess or LocalDockerBridge
       or loadProfilePurpose is Smoke:
        maxClaim = Validation
    elif executionProfile is CiContainer or RemoteProcess
         or loadProfilePurpose is Comparison:
        maxClaim = Regression
    elif executionProfile is DedicatedLabBareMetal or DedicatedLabContainer
         and loadProfilePurpose is PublishableBenchmark:
        maxClaim = Benchmark

    if repetitions == 1 and maxClaim >= Regression:
        maxClaim = Validation  // single run cannot show stability

    return maxClaim
```

## Consequences

### Positive

- Reports self-describe their trustworthiness. A consumer does not need to
  understand evidence classification to know what the report claims.
- The gap between "I ran it on my laptop" and "this is a verified benchmark"
  is enforced by the type system, not just documentation.
- The private/internal layer has a clear boundary: `Verified` is the claim
  level that requires hosted infrastructure, retained artifacts, and
  attestation.
- Report rendering can adapt to claim level. `DiagnosticOnly` reports can be
  terse; `Verified` reports can include full artifact links and attestation
  details.

### Negative

- A derivation function must be implemented, tested, and maintained.
- Edge cases exist: a `DedicatedLabBareMetal` run with `Smoke` profile should
  not produce `Benchmark`. The derivation function must handle these.
- Adds another concept for consumers to learn, on top of evidence class and
  comparability status.

### Neutral

- `ReportClaimLevel` does not replace `BenchmarkEvidenceAssessment`. Evidence
  class and comparability operate at the per-result level; claim level
  operates at the report level. Both are necessary.
- The derivation function is intentionally conservative and can become more
  permissive only when the controlled provenance actually exists.

## Related

- [ADR-0001: Measurement Provenance](ADR-0001-measurement-provenance.md)
- [ADR-0002: Execution Profiles](ADR-0002-execution-profiles.md)
- [Measurement Model](../architecture/measurement-model.md)
- [Report Model](../architecture/report-model.md)
