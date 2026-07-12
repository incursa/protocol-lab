---
title: "Specification Coverage Claim Language"
---

# Specification Coverage Claim Language

Public wording must state the scope, relationship, outcome, and evidence state
that actually exist.

## Acceptable Examples

- "This workload exercised two proposed RFC 9000 requirement mappings."
- "The implementation passed the named check; the mapping is diagnostic and
  does not directly validate the requirement."
- "Three reviewed requirements were directly validated under the named server
  profile."
- "No conclusion is available because the required trace artifact is missing."

## Wording To Avoid

- "RFC 9000 compliant" based on a partial profile.
- "Passed the RFC" or a percentage presented as a universal conformance score.
- "Validated requirement" for an `observes`, `exercises`, or `prerequisite`
  mapping.
- A publishable or authoritative finding derived only from diagnostic evidence.
- A historical finding recalculated using a later catalog or mapping version.

Named profiles may report counts by explicit outcome and relationship. The
profile title, version, denominator, role, protocol version, limitations,
freshness, and evidence qualification must remain visible. Specification
coverage never overrides ProtocolLab comparability or publication rules.
