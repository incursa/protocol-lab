# ADR-0005: Effective Load Shape

## Status

Accepted

## Context

ProtocolLab load profiles describe the shape a user wants, but individual load
tools do not all interpret that shape the same way. A scenario may request a
connection count, stream count, duration, warmup, and repetition count, yet a
particular tool may ignore, derive, or constrain some of those fields.

Without an explicit effective shape, reports can hide important differences
between the requested load and the load that actually ran.

## Decision

Introduce two related model types:

1. `RequestedLoadShape` records the user-requested shape.
2. `EffectiveLoadShape` records what the tool actually executed.

Also introduce `LoadShapeSemantics` to record, per protocol and load tool:

- supported fields
- ignored fields
- derived fields
- unsupported fields
- warnings

The runner and report pipeline must preserve both requested and effective
shape values instead of assuming they are identical.

## Consequences

### Positive

- Reports can explain when a load tool ran a shape different from the one that
  was requested.
- Comparability warnings become more precise because they can point at the
  actual field mismatch.
- Different tools can share the same load profile name while still exposing
  their semantic differences explicitly.

### Negative

- Load-tool invocation and report code must carry more shape metadata.
- Consumers need to read requested and effective shape independently.

### Neutral

- The model does not force all tools to support all shape fields. It only
  makes the differences visible and machine-readable.

## Alternatives Considered

- **Treat requested shape as effective shape.** Rejected because it hides
  tool-specific interpretation.
- **Store tool-specific shape semantics only in docs.** Rejected because the
  differences need to be available in artifacts and reports.

## Related

- [ADR-0002: Execution Profiles](ADR-0002-execution-profiles.md)
- [Load Model](../architecture/load-model.md)
- [Measurement Model](../architecture/measurement-model.md)
- [Report Model](../architecture/report-model.md)
