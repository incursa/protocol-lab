---
title: "Specification Coverage Model"
---

# Specification Coverage Model

ProtocolLab relates test workloads to exact statements in versioned protocol
specifications. This model records what a workload was intended to cover and
what a particular run actually observed without turning either into a
certification claim.

The normative contracts are under
[`schemas/specification/v1/`](../../schemas/specification/v1/) and the initial
declarative records are under [`specifications/`](../../specifications/).

## Contract Surfaces

1. A specification document identifies the publisher, canonical source, exact
   draft revision when applicable, and protocol family.
2. A requirement gives a stable ProtocolLab requirement ID and an exact source
   locator. It also records applicability, roles, testability, and review state.
3. A catalog pins a reviewed set of document and requirement records.
4. A scenario mapping relates individual workload checks to requirements using
   an explicit relationship type.
5. A named coverage profile defines an explicit denominator for a particular
   question. It is never a universal protocol score.
6. A coverage-evidence sidecar records run-time outcomes and pins the exact
   catalog, mapping, profile, scenario-package, and artifact bytes used.
7. A run-level coverage index lists every catalog-specific sidecar and pins
   its bytes, allowing multiple independent profiles without merging their
   denominators.

The sidecar is additive to existing Evidence Report v1 documents. Existing
reports remain valid, and older evidence is not reinterpreted through a newer
mapping or profile.

## Mapping Relationships

- `validates`: the named check directly tests the requirement and supplies the
  evidence declared by the mapping.
- `negative-validates`: the named check directly tests required rejection or
  failure behavior.
- `exercises`: the workload traverses behavior related to the requirement, but
  its result is not sufficient to claim validation.
- `observes`: the workload captures relevant behavior or telemetry without
  controlling enough conditions for validation.
- `prerequisite`: the requirement is needed for the workload to run but is not
  the behavior the workload evaluates.

`validates` and `negative-validates` mappings require check IDs and required
evidence. A scenario-level `passed` status cannot be expanded into requirement
passes.

## Review And Claim Boundaries

Document, requirement, mapping, catalog, and profile records carry independent
review states. Proposed records are useful for review and diagnostic evidence,
but they do not support reviewed public findings. Evidence qualification,
comparability, publishability, freshness, and confidence remain independent of
specification coverage.

Run plans may optionally select existing package-provided coverage inputs with
`specificationCoverage`. The selection pins the exact catalog, mapping, and
named-profile snapshots by SHA-256 and declares whether structured outcomes
are required, diagnostic outcomes are acceptable, or mapping metadata alone is
being requested. It does not author mappings or alter evidence eligibility.

Test executors may optionally advertise `structuredValidationOutcomes` with
the exact contract versions and check identities they can emit. The declared
authority (`direct`, `inferred`, or `diagnostic`) and exact unsupported reasons
are discovery metadata only; a run still needs an observed check outcome before
the site can display requirement-level evidence.

See [mapping methodology](mapping-methodology.md) for review rules and
[claim language](claim-language.md) for public wording.
