# ADR-0007: Canonical Protocol Identifiers

## Status

Accepted

## Context

ProtocolLab scenario, load-tool, report, and artifact surfaces all need a
stable set of protocol identifiers. The project already accepts common
aliases such as `http1`, `http2`, and `http3`, but the model needs one
canonical identifier per protocol family so data can be grouped and compared
reliably.

## Decision

Use the following canonical protocol identifiers in the public model:

- `h1`
- `h2`
- `h3`
- `quic`
- `webtransport`
- `masque`

Recognize common aliases when parsing input:

- `http1` and `http/1.1` normalize to `h1`
- `http2` and `http/2` normalize to `h2`
- `http3` and `http/3` normalize to `h3`
- `wt` normalizes to `webtransport`

Protocols outside this set may still appear in placeholder or experimental
scenarios, but they are not canonical ids until the model explicitly adds
them. WebSocket remains a placeholder scenario family rather than a canonical
protocol id in the current public model.

## Consequences

### Positive

- Artifact paths and reports use a consistent protocol vocabulary.
- Public docs can refer to aliases for readability while preserving canonical
  ids in the data model.
- Scenario and load-tool compatibility checks become simpler.

### Negative

- Some user-facing docs must distinguish between aliases and canonical ids.
- Adding a new canonical protocol family becomes a deliberate schema change.

### Neutral

- The canonical ids are short and stable, but they do not have to match the
  human-friendly alias that a user typed at the CLI.

## Alternatives Considered

- **Use `http1` / `http2` / `http3` as the canonical ids.** Rejected because
  the model already uses shorter normalized ids and the short form is more
  stable for artifact grouping.
- **Allow every caller to invent ids.** Rejected because it breaks grouping,
  comparison, and artifact path stability.

## Related

- [ADR-0006: Collision-Proof Cell Identity](ADR-0006-collision-proof-cell-identity.md)
- [Scenario Model](../architecture/scenario-model.md)
- [Load Model](../architecture/load-model.md)
- [Artifact Model](../architecture/artifact-model.md)
