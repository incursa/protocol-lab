# Database Workloads Decision

Phase 8 does not add database workload scenarios.

## Decision

Database workloads remain deferred until the protocol runner has real target
orchestration, load-generator contracts, and artifact parsing for the transport
and HTTP families already modeled.

## Rationale

Database benchmarks introduce workload semantics that are not primarily
transport-protocol behavior:

- schema and dataset setup
- query mix selection
- transaction isolation and consistency expectations
- driver and connection-pool behavior
- storage and host I/O effects
- cleanup and reproducibility requirements

Adding those concerns now would blur the current runner boundary and make it
too easy to report numbers that are not comparable to the HTTP, H3, QUIC,
WebTransport, or MASQUE scenarios.

## Revisit Gate

Database workloads can be reconsidered only after a future planning slice
defines:

- the database family name and scenario ID conventions
- target implementations and driver versions
- dataset setup and teardown contracts
- validation rules that must pass before benchmarking
- load-generator or client contracts
- raw stdout, stderr, and database diagnostic artifact requirements
- metric ownership and parser behavior

Until then, database workload support is documentation-only and must not appear
as runnable scenario YAML.
