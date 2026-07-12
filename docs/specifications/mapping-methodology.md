---
title: "Specification Mapping Methodology"
---

# Specification Mapping Methodology

Specification mapping begins with an exact source statement, not a scenario
name or a passing report. Every requirement record preserves its specification
document, section, source unit, canonical URL, roles, applicability, and review
state.

## Review Procedure

1. Identify the exact normative statement and preserve its stable requirement
   ID and source locator.
2. Decide whether the statement is testable and under which endpoint role and
   protocol conditions.
3. Identify the smallest workload check that supplies evidence about that
   statement.
4. Select the weakest accurate relationship: `prerequisite`, `observes`,
   `exercises`, `negative-validates`, or `validates`.
5. For direct validation, enumerate required artifacts and the conditions that
   make the check decisive.
6. Record limitations and review the mapping separately from the scenario and
   requirement records.

Mappings must not treat an entire RFC section as validated when a check only
tests one statement or condition. A check that merely confirms handshake
completion normally exercises many requirements; it validates none of them
unless it isolates their required behavior.

## Run-Time Outcomes

Mapping intent and run outcome are separate. A run can report `passed`,
`failed`, `unsupported`, `unavailable`, `not-applicable`, `not-run`,
`inconclusive`, or `infrastructure-failed` for a mapped check. The relationship
still controls the strongest permitted claim. For example, a passed
`exercises` outcome means that related behavior was exercised; it does not mean
the requirement was validated.

Every historical sidecar pins SHA-256 digests for its catalog, mappings,
profiles, scenario package, and referenced artifacts. Corrections create new
versions. They do not rewrite the meaning of an older run.
