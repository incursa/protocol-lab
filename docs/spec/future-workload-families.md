# Future Workload Families

Phase 8 starts WebTransport and MASQUE as modeled future families only.

## WebTransport

Current scenario:

- `webtransport.session-bidi-echo`

Activation requires a WebTransport-capable target, validator, load generator,
certificate policy, qlog or equivalent protocol evidence, and raw stdout/stderr
preservation.

## MASQUE

Current scenario:

- `masque.connect-udp-tunnel`

Activation requires a MASQUE-capable target, validator, load generator,
CONNECT-UDP tunnel policy, datagram evidence, and raw stdout/stderr
preservation.

## Boundary

Both families must remain explicit unsupported outcomes until real validators
and load generators exist. The neutral runner must not contain
implementation-specific protocol branches for either family.
