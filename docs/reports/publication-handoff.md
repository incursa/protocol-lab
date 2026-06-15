# Public Report Handoff Contract

This document describes the public handoff semantics for report consumers. It
does not define a repository-owned upload workflow or storage implementation.
A handoff transfers a publication bundle and its evidence reports to a
consumer; it is not a separate schema family.

## Handoff Inputs

A report handoff should include:

- a public report bundle that conforms to [`schemas/public-report/v1/`](../../schemas/public-report/v1/)
- an artifact index that names public-safe artifacts
- an evidence report that preserves claim level and provenance
- a stable run identifier
- enough package, scenario, suite, load-profile, and executor metadata for a
  consumer to interpret the report without private lab state

## Consumer Responsibilities

Consumers such as public sites or archive importers should validate schemas,
preserve diagnostic labels, keep unsupported and unavailable outcomes visible,
and avoid deriving stronger claims than the evidence permits.

## Producer Responsibilities

Producers should remove private paths, credentials, internal hostnames, local
workspace details, and private operational artifacts before handoff. Producers
must not suppress failed validation, unsupported outcomes, or non-publishable
claim status.
