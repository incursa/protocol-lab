---
title: "Validation Check Outcomes"
---

# Validation Check Outcomes

`validation-check-outcomes.json` is an optional per-cell artifact for producers
that can report exact scenario check outcomes. Its canonical schema is
[`schemas/validation/v1/check-outcomes.schema.json`](../../schemas/validation/v1/check-outcomes.schema.json).

The artifact retains the overall scenario status for context, but it never
derives check rows from that status. Every row identifies a check, outcome,
reason, message, diagnostic state, and any hash-addressed proof artifacts.
Missing rows are unavailable evidence; they are not passes.

Specification coverage producers join these exact rows to a pinned scenario
mapping. The mapping relationship still limits the claim: a passed `observes`
or `exercises` check cannot become a validated requirement. Existing
`validation.json`, result, report, cell, and artifact consumers remain valid
when the optional artifact is absent.
