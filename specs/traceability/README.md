# ProtocolLab Traceability

ProtocolLab uses SpecTrace as the canonical traceability model. Requirements
live in `specification` JSON artifacts. Architecture, work-item, and
verification artifacts link back to those requirement IDs through SpecTrace
fields.

This directory does not define a separate trace model. [`trace-links.json`](trace-links.json) is a
repository index that points readers to the canonical SpecTrace artifacts and
to the schemas and fixtures that support selected requirements.

## Trace Relationship Labels

SpecTrace relationships use the project-standard labels:

- `Satisfied By`
- `Implemented By`
- `Verified By`
- `Derived From`
- `Supersedes`
- `Upstream Refs`
- `Related`

In JSON artifacts in this repository, these labels appear through the
corresponding SpecTrace trace fields, such as `satisfied_by`,
`implemented_by`, `verified_by`, `derived_from`, `supersedes`,
`upstream_refs`, and `related`.

## Reading Contract Coverage

Start with [`specs/requirements/`](../requirements/), then follow each requirement's
trace fields to architecture and verification artifacts. Use
[`trace-links.json`](trace-links.json) as a convenience index from requirements to the schemas and
fixtures that illustrate the public contract.
