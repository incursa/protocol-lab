# ProtocolLab - Roadmap

**Status:** Current (living document; phases reflect the public/community roadmap and current implementation state)

## Phase Summary

| Phase | Name | Status | Key Deliverables |
|-------|------|--------|------------------|
| 0 | Planning and Traceability | Implemented | Vision, architecture, requirements trace, phase plan, non-goals, open questions |
| 1 | Minimal HTTP Vertical Slice | Implemented | Solution, model, CLI, scenario and load-profile parsing, matrix expansion, artifact paths, Kestrel baseline, unit tests |
| 2 | Validation Hardening and Load Tool Adapters | Implemented | H3 protocol proof, h2load/oha/managed adapters, evidence classification, comparability gates, Docker h2load, `dotnet-counters`, acceptance workflow, adapter conformance suite |
| 3 | Docker Targets, Caddy, nginx | Implemented | Docker target execution, shared-network mode, resource controls, container metrics, Caddy and nginx optional HTTP/3 targets |
| 4 | Incursa HTTP/3 Integration | Implemented | Incursa HTTP/3 runnable through the existing manifest contract; custom metric ingestion remains deferred |
| 5 | H3 Protocol Scenarios | Modeled | QPACK, cancellation, and multiplexing scenarios with H3-specific metrics; validators and load generators remain deferred |
| 6 | Raw QUIC Transport | Implemented - fixture only | Fixture QUIC adapters and scenarios for handshake, stream, multiplexing, churn, and duplex coverage; real QUIC traffic remains deferred |
| 7 | Network Simulation | Deferred | Network profile execution through impairment providers such as `docker-tc` and `ns3-simulator` |
| 8 | Additional Implementations and Future Families | Proposed | quic-go, WebTransport, MASQUE, and database workloads |

## Phase 0: Planning and Traceability (Implemented)

- Project vision, architecture description, repository conventions
- Requirements trace matrix with stable requirement ID groups
- Phase plan with explicit exit criteria and stop conditions
- Non-goals and open design questions documented

## Phase 1: Minimal HTTP Vertical Slice (Implemented)

- `Incursa.ProtocolLab.sln` with central `Directory.Build.props`
- Core model: manifests, scenarios, load profiles, matrix expansion, artifact layout, results
- CLI: `list`, `check`, `validate`, `run` commands
- `KestrelBenchServer` with `/plaintext` and `/json` endpoints
- Focused unit tests for parsing, expansion, paths, serialization

## Phase 2: Validation Hardening and Load Tool Adapters (Implemented)

- HTTP/3 protocol proof via curl `--http3-only` and managed `HttpClient`
- `h2load`, `oha`, and managed `HttpClient` H3 load tool adapters
- Docker h2load execution with repo-owned image
- Best-effort output parsing with `parsedMetricsAvailable` flag
- Evidence classification (local-smoke through publishable)
- Comparability gates with explicit warnings
- Runtime counter capture via `dotnet-counters`
- Adapter control plane v1 contract and conformance suite
- v1 acceptance workflow and run index generation

## Phase 3: Docker Targets, Caddy, nginx (Implemented)

- Docker target execution for Kestrel and Incursa
- Shared Docker network mode with `--connect-to` routing
- Docker resource limits (CPU, memory)
- Docker container metrics capture (load-generator and target)
- Caddy as optional Docker-only HTTP/3 target
- nginx as optional Docker-only HTTP/3 target with module proof via `nginx -V`

## Phase 4: Incursa HTTP/3 Integration (Implemented)

- Incursa HTTP/3 runs through the shared manifest and adapter contracts
- Custom metric artifact ingestion through manifest contracts remains deferred
- No special-casing in runner logic; Incursa stays behind the manifest boundary

## Phase 5: H3 Protocol Scenarios (Modeled)

- QPACK encoding/decoding scenarios
- Stream cancellation and reset behavior scenarios
- Multiplexing and stream concurrency scenarios
- H3-specific result metrics where load tools can report them
- Validators and H3-capable load generators remain deferred

## Phase 6: Raw QUIC Transport (Implemented - fixture only)

- Incursa Raw QUIC Adapter v1 and MSQuic/.NET Adapter v1
- Fixture QUIC adapters, scenario definitions, and deterministic load-tool proofs
- Handshake latency and throughput scenarios
- Stream throughput, multiplexing, and churn scenarios
- Datagram and flow-control scenarios remain deferred
- No synthetic metrics without real collection
- Real QUIC traffic and benchmark claims remain deferred

## Phase 7: Network Simulation (Deferred)

- `docker-tc` provider for latency, loss, and bandwidth
- `ns3-simulator` provider for advanced topologies
- Network profile catalog is already separate from the scenario catalog; execution remains deferred

## Phase 8: Additional Implementations and Future Families (Proposed)

- quic-go as a runnable Docker target
- WebTransport session scenarios with real validators and load generators
- MASQUE CONNECT-UDP tunnel scenarios
- Database workload families remain deferred unless explicitly scheduled

## Stop Conditions

These apply at every phase:

- Stop before implementation if requirements are unclear enough that a
  contract would be speculative.
- Stop before benchmarking if validation does not pass.
- Stop before publishing reports if raw artifacts are missing or results are
  not attributable to real load-tool output.

## Future Horizons (Proposed)

Beyond the phased plan, the following capabilities are discussed but not
scheduled for the public/community repo:

- **CI execution profile**: pre-configured matrix, artifact retention policy,
  and pipeline-friendly exit codes so ProtocolLab can run in GitHub Actions
  and similar CI systems.
- **Hosted execution**: a controlled environment backend with attested
  provenance, retained artifact archives, and environment attestation.
- **Dashboards and analysis**: web-based result browsing, trend analysis,
  regression detection, and comparative visualization.
- **Extended scenarios**: DNS over QUIC, HTTP/3 datagrams, connection
  migration, 0-RTT behavior.
- **Private/internal CI and diagnostic tooling**: deeper analysis of
  protocol-level traces, custom metric dashboards, and integration with
  private artifact stores.

These are recorded here for direction only. They are not commitments of the
public/community repo. If they are built, they should land in the internal
repository or a separate service boundary, not as hidden public behavior.
