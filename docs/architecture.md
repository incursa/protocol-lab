# Architecture

## Scope

This document is the top-level architecture summary for the public/community
ProtocolLab repo. The detailed model pages under `docs/architecture/` are the
authoritative source for implementation shape. This summary highlights the
current public implementation rather than the older phase-plan language.

## Components

### Runner

The runner loads implementation manifests and scenario definitions, expands a
scenario matrix, performs validation, executes benchmarks only after
validation passes, captures artifacts, and writes results.

The runner must not reference Incursa protocol assemblies directly. Incursa
integrations enter through the same manifest, command, container, port,
environment, and artifact contracts used for every other implementation.

### Model

The model defines stable records for:

- implementation manifests
- scenario definitions
- workload families
- load profiles
- validation rules
- benchmark load shape
- execution profile
- requested and effective load shape
- network profiles
- collision-proof run-cell identity
- artifact paths
- validation outcomes
- benchmark results
- report claim levels
- parsed metrics and raw artifacts

The model currently covers HTTP application scenarios, modeled H3/QUIC
scenario families, and placeholder WebTransport/MASQUE surfaces. It also
captures canonical protocol IDs, which normalize aliases such as
`http1`/`http/1.1` to `h1`, `http2`/`http/2` to `h2`, and
`http3`/`http/3` to `h3`.

### Manifests

Implementation manifests describe runnable targets. They define identity,
supported roles, supported protocols, supported workload families, ports,
environment variables, command arguments, readiness checks, shutdown
behavior, capability flags, artifact exports, qlog support, SSL key log
support, and notes.

Unsupported scenarios are resolved through manifest capabilities and scenario
requirements. A scenario that is unsupported must produce a clear unsupported
outcome with a reason.

### Scenarios

Scenarios describe what should be validated and benchmarked. They include
identity, family, protocol, implementation role, endpoint or transport
action, validation rules, benchmark load shape, repetitions, warmup,
duration, network profile, required metrics, artifact requirements, and tags.

HTTP scenarios use endpoint-oriented fields. QUIC transport scenarios use
transport action fields. H3 protocol scenarios use protocol-behavior fields.
WebTransport and MASQUE are modeled as placeholders for future execution
surfaces.

### Validation

Validation is a gate. It checks server startup, readiness, endpoint or
protocol reachability, expected status, headers, body, stream behavior, and
explicit unsupported outcomes.

Validation results are not performance results. If validation fails,
benchmark data for that implementation and scenario is invalid and must not
be accepted.

### Load Tools

Load tools are pluggable adapters. The implemented load tools are `h2load`,
`oha`, and the managed HTTP/3 `HttpClient` load path. Custom H3 and QUIC
load tools remain deferred.

### Adapter Control Planes

Adapter control planes are documented separately in
[`docs/architecture/adapter-contract-v1.md`](architecture/adapter-contract-v1.md).
They are not protocol endpoints. They are HTTP/1.1 JSON control services that
prepare, start, observe, and dispose separate protocol endpoints.

The adapter conformance suite in
[`docs/runner/adapter-conformance.md`](runner/adapter-conformance.md) is the
accepted pre-runner proof surface for future adapters. It exists so adapter
implementations can be verified before the runner is taught to consume them.

The runner talks to the adapter control plane through a generic execution
backend. The adapter may run locally, in Docker, on an external host, or in a
future bare-metal/LXC backend without changing the contract. Protocol traffic
still goes to the adapter-reported test endpoint, not to the control plane.

Every load-tool run preserves raw stdout and stderr. Parsed metrics are
best-effort. If parsing fails, the result remains useful as an artifact
bundle and sets `parsedMetricsAvailable=false`.

### Benchmark Execution

The intended flow is:

1. Load implementation manifests.
2. Load scenario definitions.
3. Expand the scenario matrix.
4. Start the implementation server or target process/container.
5. Wait for readiness.
6. Run validation.
7. If validation passes, invoke the selected load tool.
8. Capture client stdout and stderr.
9. Capture server logs and configured exports.
10. Capture qlog, SSL key logs, pcap, and runner metadata where available.
11. Emit one JSON result per run cell.
12. Emit markdown summary and aggregate JSON.

### Artifacts

Artifacts are written under `.artifacts/runs/{runId}`. Each run cell has a
deterministic path that includes implementation ID, scenario ID, protocol ID,
execution profile, network profile, load profile, connection count, stream
count, and repetition.

### Reporting

Reports summarize validation outcomes, benchmark metrics, parse status,
errors, warnings, claim level, and artifact paths. Repetition-aware reports
show median, best, and worst values.

### Server Implementations

`KestrelBenchServer` is the first baseline server because it can provide
HTTP/1.1, HTTP/2, and HTTP/3 coverage using ASP.NET Core.

`Kestrel Adapter v1` is the first real adapter control plane for that
server. It runs as a separate HTTP/1.1 JSON process, starts the benchmark
server as a child endpoint process, and returns the protocol endpoint URL to
the runner. The adapter documentation lives in
[`docs/runner/kestrel-adapter.md`](runner/kestrel-adapter.md).

`IncursaBenchServer.Placeholder` documents the Incursa HTTP/3 integration
point used by the runnable sample manifest. Incursa integrations can also
provide raw QUIC transport server behavior, raw QUIC load-generation
behavior, and custom protocol metric exports as those surfaces are
scheduled.
The Incursa HTTP/3 target contract is documented in
`docs/spec/incursa-http3-target-contract.md`; the runner remains neutral and
the target stays behind the same manifest contract as every other
implementation.

Caddy is the first optional non-.NET Docker HTTP/3 target and is documented
in `docs/spec/caddy-http3-target.md`. nginx is the next optional Docker-only
HTTP/3 target and is documented in `docs/spec/nginx-http3-target.md`; its
image must prove HTTP/3 module support before validation or benchmarking.
quic-go now lives as a runnable Go-based Docker HTTP/3 target in the adapter
source tree so the expanded local comparison suite can include it without
relying on placeholder-only manifests. Placeholder manifests must not
advertise protocols or capabilities before support is real.

### Network Profiles

Network profile modeling starts early, but execution begins with provider
`none`. Future providers may include `docker-tc` and `ns3-simulator`.
Network profile YAML is loaded through a separate catalog from scenario YAML
so profile definitions do not become runnable scenario cells.

### Boundaries

- The runner owns orchestration, validation flow, load-tool invocation,
  artifact capture, and reporting.
- The runner owns the orchestration logic that consumes the generic adapter
  control-plane contract and must not confuse it with the protocol endpoint
  under test.
- The adapter control plane is separate from the protocol endpoint; the
  control plane manages session lifecycle and the endpoint carries the actual
  protocol traffic.
- Implementations own protocol stacks, server behavior,
  implementation-specific optimization, and custom metrics.
- Load tools own request generation and raw metric output.
- Result parsing owns best-effort conversion from raw tool output into
  normalized metrics.
- Network impairment providers own latency, loss, bandwidth, and simulation
  setup; unsupported providers must remain explicit non-execution outcomes.
