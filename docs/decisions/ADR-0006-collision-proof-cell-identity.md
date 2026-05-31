# ADR-0006: Collision-Proof Cell Identity

## Status

Accepted

## Context

ProtocolLab artifacts are organized by run cell. A run cell must be uniquely
identifiable across implementations, scenarios, protocols, execution
profiles, network profiles, load profiles, connection counts, stream counts,
and repetitions. If that identity is incomplete or ambiguous, artifact paths
can collide and report grouping can merge distinct cells.

## Decision

Introduce `RunCellIdentity` as the canonical cell identity type. It records:

- implementation id
- scenario id
- normalized protocol id
- execution profile id
- network profile
- load profile id
- connections
- streams per connection
- repetition

Identity values are sanitized into deterministic path segments and combined
into a stable slug and key. Artifact layout, report grouping, and cell
identifiers all use the same identity source of truth.

No separate `cell.json` artifact is required to establish identity. The
identity is encoded in the path and in the cell/result payloads.

## Consequences

### Positive

- Artifact paths are stable and collision-resistant.
- Grouping logic can use one canonical identity instead of ad hoc field lists.
- Changing a scenario or load profile can no longer accidentally overwrite a
  different cell.

### Negative

- More fields participate in path generation, so artifact paths are longer.
- Any future identity change becomes a deliberate migration decision.

### Neutral

- The identity is still human-readable. It is not an opaque GUID.

## Alternatives Considered

- **Opaque GUID per cell.** Rejected because the path would stop being
  inspectable and debuggable.
- **Rely on result JSON only.** Rejected because path-level uniqueness is part
  of the artifact contract.

## Related

- [ADR-0005: Effective Load Shape](ADR-0005-effective-load-shape.md)
- [Artifact Model](../architecture/artifact-model.md)
- [Runner Model](../architecture/runner-model.md)
