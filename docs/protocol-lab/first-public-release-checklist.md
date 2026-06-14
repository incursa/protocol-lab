# First Public Release Checklist

This checklist applies to the public contract repository.

## Required

- [x] README describes ProtocolLab as a language-neutral public specification
  and contract repository.
- [x] Public/internal boundary documentation states that runners and hosted
  labs are implementations of public contracts.
- [x] Implementation source, scripts, build files, and workflow automation are
  absent from the public tree.
- [x] Schemas remain under `schemas/`.
- [x] Public fixtures remain under `fixtures/public-contracts/`.
- [x] Scenario definitions remain under `scenarios/`.
- [x] Suite definitions remain under `suites/`.
- [x] Load-profile definitions remain under `load-profiles/`.
- [x] Canonical requirements, architecture, work-item, and verification records
  are authored as SpecTrace JSON.
- [x] Traceability support points back to SpecTrace artifacts instead of
  defining a separate trace model.

## Must Stay True

- Public contracts are language-neutral.
- The public repository does not imply a canonical runner implementation.
- Internal implementations may consume public contracts.
- Public contracts must not depend on internal code, services, configuration,
  or artifacts.
- Unsupported and unavailable outcomes remain explicit.
- Raw QUIC and HTTP/3 lanes remain separate in contracts, fixtures, and report
  semantics.
