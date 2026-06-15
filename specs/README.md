# ProtocolLab Specs

ProtocolLab uses SpecTrace as the canonical authored specification and
traceability model.

## Artifact Families

- [`requirements/`](requirements/) contains canonical `specification` artifacts.
- [`architecture/`](architecture/) contains canonical [`architecture`](architecture/) artifacts and
  supporting architecture notes.
- [`work-items/`](work-items/) contains canonical `work_item` artifacts.
- [`verification/`](verification/) contains canonical [`verification`](verification/) artifacts and
  supporting verification notes.
- [`traceability/`](traceability/) contains a SpecTrace usage guide and a convenience index.

## Source Of Truth

Canonical requirements, architecture decisions, work items, and verification
records are JSON artifacts that follow the SpecTrace model. Markdown in this
tree may explain intent or lifecycle, but it is not a replacement for the
canonical JSON artifacts.

Core public contract coverage is recorded in:

- [`requirements/SPEC-PL-CORE-CONTRACTS.json`](requirements/SPEC-PL-CORE-CONTRACTS.json)
- [`architecture/ARC-PL-CORE-CONTRACTS.json`](architecture/ARC-PL-CORE-CONTRACTS.json)
- [`verification/VER-PL-CONTRACT-COVERAGE-0001.json`](verification/VER-PL-CONTRACT-COVERAGE-0001.json)

## Trace Relationships

Requirements should use stable IDs and approved normative keywords. Trace
relationships should follow SpecTrace conventions such as `Satisfied By`,
`Implemented By`, `Verified By`, `Derived From`, `Supersedes`, `Upstream Refs`,
and `Related`.
