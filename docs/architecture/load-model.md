# Architecture — Load Model

**Status:** Implemented (h2load, oha, managed HttpClient adapters complete; custom H3 and QUIC load generators deferred)

## Purpose

The load model defines how ProtocolLab generates protocol traffic for
benchmarking. Load tools are external or in-process programs that produce
request load, measure performance, and emit raw metrics. The load model
abstracts over different tools, shapes, and execution modes so the runner
can invoke them uniformly.

## Assembly

- **Project:** `src/Incursa.ProtocolLab.Model`
- **Namespace:** `Incursa.ProtocolLab.Model`
- **Key types:** `LoadToolManifest`, `LoadToolDefinition`, `LoadToolKind`,
  `LoadToolPurpose`, `LoadToolExecutionResult`, `LoadToolInvoker`,
  `RequestedLoadShape`, `EffectiveLoadShape`, `LoadShapeSemantics`,
  `LoadProfileDefinition`, `ManagedHttp3LoadGenerator`

## Load Tool Manifests

Load tools are described by YAML manifests under `load-tools/`. Each manifest
(`LoadToolManifest`) defines:

| Field | Type | Purpose |
|-------|------|---------|
| Id | string | Unique identifier (e.g., `h2load`) |
| Name | string | Human-readable name |
| Kind | LoadToolKind | `Managed`, `Process`, or `Docker` |
| Purpose | LoadToolPurpose | `Validation`, `Benchmark`, `Profile`, `Diagnostic` |
| Protocols | string[] | Supported protocols (h3, h2, h1, quic) |
| TrafficShapes | TrafficShape[] | Supported traffic patterns |
| Execution | object | Process command, Docker image, arguments |
| Parser | object | Output parser kind and configuration |
| Capabilities | string[] | Feature flags |
| Categories | string[] | `managed-lab`, `external-reference`, `experimental` |

### Current Load Tools

| Tool | Kind | Category | Protocols |
|------|------|----------|-----------|
| `h2load` | Process / Docker | external-reference | h3, h2, h1 |
| `oha` | Process | external-reference | h3, h2, h1 |
| `managed-httpclient-h3-load` | Managed | managed-lab | h3 |

### Docker Load Tool Execution

Docker-mode load tools run in containers:
- The repo-owned `incursa/protocol-lab-h2load-http3:local` image is built
  from pinned source tags.
- Docker h2load must separately prove `--h3` support before execution.
- Shared-network mode uses `--connect-to` and SNI `localhost` to route
  traffic to the target container alias without `host.docker.internal`.
- Docker resource limits (CPU, memory) can be applied.
- Docker container metrics (CPU, memory, network) can be captured for
  saturation analysis.

## Load Shapes

A load shape describes the intensity and pattern of generated traffic.

### RequestedLoadShape

What the user or scenario requests:
- Connection count
- Concurrency (streams per connection)
- Duration (seconds)
- Warmup duration (seconds)
- Traffic shape (unidirectional, bidirectional, request-response)

### EffectiveLoadShape

What the load tool actually delivers after mapping:
- The load tool may ignore, derive, or constrain certain fields based on
  its capabilities.
- `LoadShapeSemantics` documents which fields are supported, ignored, or
  derived for each tool.

### Load Profile

A `LoadProfileDefinition` (under `load-profiles/`) is a named preset of
load shape defaults:

| Profile | Purpose |
|---------|---------|
| `smoke` | Quick functional check (1 connection, 1 stream, 5s) |
| `local-comparison` | Moderate load for local A/B comparison |
| `local-regression` | Longer duration, multiple connections for regression |

Profiles are applied as defaults; explicit CLI arguments override them.

## Load Tool Invocation

`LoadToolInvoker` manages the full invocation lifecycle:

1. **Resolution:** Select the best available load tool for the cell's
   protocol, traffic shape, and execution mode.
2. **Capability detection:** Verify the tool supports the requested
   protocol and shape. For Docker tools, verify the image supports
   required flags (e.g., `h2load --h3`).
3. **Command construction:** Build the command line or Docker arguments
   from the load shape and tool manifest.
4. **Resource control:** Apply Docker resource limits if configured.
5. **Execution:** Run the tool as a process or Docker container.
6. **Capture:** Preserve raw stdout and stderr as artifacts.
7. **Parsing:** Parse output best-effort into `HttpMetrics`.
8. **Result:** Emit `LoadToolExecutionResult` with status, metrics, and
   artifact paths.

### Execution Status

| Status | Meaning |
|--------|---------|
| `Succeeded` | Tool ran and produced output |
| `Failed` | Tool ran but returned non-zero exit code |
| `Unavailable` | Tool is not installed or image not built |
| `Skipped` | Validation failed or cell was incompatible |
| `Unsupported` | Tool does not support the requested protocol or shape |

## Managed H3 Load Generator

`ManagedHttp3LoadGenerator` is an in-process HTTP/3 load generator using
.NET `HttpClient` with exact HTTP/3 version policy. It runs inside the
runner process and produces:

- Request count, success/failure counts
- Bytes sent and received
- Latency samples (min, max, mean, percentiles)
- Connect time and TTFB

Results are marked `managed-lab`. They are useful local baselines and
should not be directly ranked against external-reference tools (h2load,
oha).

## Best-Effort Parsing

All load-tool output is parsed best-effort. Parsers exist for:
- `h2load` JSON and text output
- `oha` JSON output
- Managed H3 JSON output

If parsing fails for any reason:
- Raw stdout and stderr are preserved as artifacts.
- `parsedMetricsAvailable` is set to `false`.
- The result contains a parsing error message and raw artifact paths.
- Parsing failure does not invalidate the raw data; it only means automated
  extraction was unsuccessful.

## Proposed Extensions

- **Custom H3 load generator:** A specialized H3 load tool capable of
  QPACK-specific, cancellation, and multiplexing scenarios.
- **Raw QUIC load generator:** A QUIC transport load tool for handshake,
  stream, datagram, and churn scenarios.
- **Load tool registry:** A discoverable registry so tools can be added
  without modifying runner code.
- **Streaming metrics:** Real-time metric emission for long-running or
  adaptive load scenarios.

## Related Documents

- [Runner Model](runner-model.md) — how load tools are invoked
- [Scenario Model](scenario-model.md) — how scenarios define load shapes
- [Artifact Model](artifact-model.md) — how load-tool output is stored
- [Architecture Overview](overview.md)
