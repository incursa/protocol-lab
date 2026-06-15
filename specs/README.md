# ProtocolLab Specs

ProtocolLab uses SpecTrace as the canonical authored specification and
traceability model.

## Artifact Families

- [`requirements/protocol-lab/`](requirements/protocol-lab/) contains canonical `specification` artifacts.
- [`architecture/protocol-lab/`](architecture/protocol-lab/) contains canonical [`architecture`](architecture/) artifacts and
  supporting architecture notes.
- [`work-items/protocol-lab/`](work-items/protocol-lab/) contains canonical `work_item` artifacts.
- [`verification/protocol-lab/`](verification/protocol-lab/) contains canonical [`verification`](verification/) artifacts and
  supporting verification notes.
- [`traceability/`](traceability/) contains a SpecTrace usage guide and a convenience index.

## Source Of Truth

Canonical requirements, architecture decisions, work items, and verification
records are JSON artifacts that follow the SpecTrace model. Markdown in this
tree may explain intent or lifecycle, but it is not a replacement for the
canonical JSON artifacts.

Core public contract coverage is recorded in:

- [`requirements/protocol-lab/SPEC-PL-CORE-CONTRACTS.json`](requirements/protocol-lab/SPEC-PL-CORE-CONTRACTS.json)
- [`architecture/protocol-lab/ARC-PL-CORE-CONTRACTS.json`](architecture/protocol-lab/ARC-PL-CORE-CONTRACTS.json)
- [`verification/protocol-lab/VER-PL-CONTRACT-COVERAGE-0001.json`](verification/protocol-lab/VER-PL-CONTRACT-COVERAGE-0001.json)

## Trace Relationships

Requirements should use stable IDs and approved normative keywords. Trace
relationships should follow SpecTrace conventions such as `Satisfied By`,
`Implemented By`, `Verified By`, `Derived From`, `Supersedes`, `Upstream Refs`,
and `Related`.
