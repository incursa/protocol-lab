# Raw QUIC Load Generator Contract

Phase 6 defines raw QUIC transport scenarios without pretending a runnable
load generator exists.

## Current State

- Scenario family: `quic.transport`
- Scenario files: `scenarios/quic/transport/*.yaml`
- Validator behavior: explicit `unsupported` result until a raw QUIC validator
  and load generator are implemented
- Metrics model: optional protocol metric bag only

## Activation Gate

Raw QUIC benchmark execution can be enabled only after a scheduled slice adds:

- a concrete load-generator executable or container image
- target process/container contract
- TLS/ALPN and certificate handling
- scenario-specific validation for handshake, stream throughput, multiplexing,
  connection churn, and duplex streams
- raw stdout and stderr preservation
- qlog or equivalent protocol evidence for accepted benchmark results
- parser behavior that keeps `parsedMetricsAvailable=false` when parsing fails

## Runner Boundary

The runner must keep raw QUIC support behind generic manifest, validation,
load-tool, artifact, and parser contracts. It must not reference Incursa QUIC
assemblies directly, and it must not emit successful raw QUIC benchmark data
unless validation passed for the selected implementation and scenario.
