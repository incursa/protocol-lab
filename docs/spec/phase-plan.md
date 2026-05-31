# Phase Plan

## Phase 0: Planning and Traceability

Goal: convert the project vision into staged, reviewable documents without writing implementation code.

Deliverables:

- `AGENTS.md`
- `PLANS.md`
- `docs/vision.md`
- `docs/architecture.md`
- `docs/spec/requirements-trace.md`
- `docs/spec/phase-plan.md`
- `docs/spec/non-goals.md`
- `docs/spec/open-questions.md`

Exit criteria:

- Major requirements from the vision have stable IDs.
- Implementation-neutrality is explicit.
- Incursa's preferred target position is explicit.
- Phase 1 scope is bounded to the smallest useful vertical slice.
- Deferred work is recorded instead of implied.

## Phase 1: Minimal HTTP Vertical Slice

Goal: prove the smallest useful end-to-end path without raw QUIC or Incursa integration.

Deliverables:

- `Incursa.ProtocolLab.sln`
- central target framework property
- `src/Incursa.ProtocolLab.Model/`
- `src/Incursa.ProtocolLab.Cli/`
- YAML scenario parsing
- YAML implementation manifest parsing
- scenario matrix expansion
- artifact path generation
- result JSON model
- markdown summary model
- `scenarios/http/core/plaintext.yaml`
- `scenarios/http/core/json.yaml`
- `implementations/kestrel-http3.yaml`
- `servers/KestrelBenchServer/` with `/plaintext` and `/json`
- basic `validate` command
- basic `run` command that invokes a load tool if present
- focused unit tests

Explicit exclusions:

- raw QUIC benchmarks
- Incursa protocol integration unless already trivially available
- nginx, Caddy, quic-go implementation work
- WebTransport
- MASQUE
- database benchmarks
- network simulation beyond provider `none`
- fabricated metrics or placeholder successful benchmark results

Exit criteria:

- `dotnet build` succeeds.
- `dotnet test` succeeds for focused unit tests.
- Sample scenarios and manifests parse.
- Matrix expansion and artifact path generation are deterministic.
- Result serialization round-trips.
- Validation failure prevents benchmark acceptance.

## Phase 2: HTTP Coverage and Load Tool Hardening

Goal: broaden HTTP application scenarios and improve load-tool support.

Deliverables:

- remaining HTTP core, payload, headers, upload scenario files
- expanded Kestrel HTTP endpoint implementation
- `h2load` adapter if available
- `oha` adapter if practical
- parser tests for implemented adapters
- explicit unavailable-tool behavior
- raw stdout/stderr preservation tests

### Phase 2A: Managed Target Orchestration Smoke

Goal: make one complete HTTP benchmark execution path real before expanding
protocol coverage.

Current exit state:

- `kestrel-http3` can be started from its manifest as a local process when
  `--base-url` is omitted.
- `--base-url` still selects external/pre-started target mode and does not
  start a process.
- Managed startup waits for `/plaintext` readiness, exposes the resolved base
  URL to validation and load-tool execution, captures target stdout/stderr,
  stops the target, and writes target execution metadata.
- `http.core.plaintext` and `http.core.json` validate against managed Kestrel
  over HTTP/1.1.
- `run` preserves validation artifacts and reports `load-tool-unavailable`
  honestly when `h2load` is not on `PATH`; no benchmark metrics are fabricated.
- Best-effort `h2load` parsing exists for common request, latency, and
  throughput fields when the tool is available.

Deferred from Phase 2A:

- Docker target startup.
- HTTP/3 transport enablement for Kestrel.
- Incursa HTTP/3 runnable image/startup.
- Raw QUIC, WebTransport, MASQUE, database workloads, nginx, Caddy, quic-go,
  and network impairment execution.

### Phase 2B: Load-Tool Provisioning and Baseline

Goal: make benchmark execution real and repeatable for the Kestrel HTTP
baseline without expanding protocol coverage.

Current exit state:

- Load tools are described by YAML manifests under `load-tools/`.
- `h2load` and `oha` can be selected by `--load-tool`.
- `--load-tool-mode process|docker` is accepted. Process mode is executable;
  Docker mode is modeled and returns a structured unsupported result.
- If no load tool is explicitly requested, the runner selects a compatible
  available process tool by protocol and scenario family.
- Process mode records version output, raw stdout/stderr, and
  `load-tool-execution.json` for every benchmark cell.
- `oha` is provisionable through `winget` on Windows and was used for the
  local Kestrel HTTP/1 plaintext/json baseline.
- `h2load` text parsing and `oha` JSON parsing are best-effort and never
  fabricate missing metrics.
- Aggregate JSON and markdown summaries include load tool, mode, benchmark
  execution status, parsed metric availability, request rate, p50/p95/p99,
  mean latency, throughput, and failure reason.

Deferred from Phase 2B:

- Docker load-tool execution.
- HTTP/3 transport benchmark proof.
- Incursa HTTP/3 target execution.
- Raw QUIC, WebTransport, MASQUE, database workloads, nginx, Caddy, quic-go,
  and network impairment execution.

### Phase 2C: Benchmark Semantics and Fairness Controls

Goal: make the HTTP/1 baseline harder to misread before adding HTTP/3 proof.

Current exit state:

- Each benchmark result records requested load shape, effective load shape, and
  load-shape semantics.
- HTTP/1.1 `streamsPerConnection` requests are recorded as ignored/not
  applicable and the effective stream count is `1`.
- `oha` is documented and modeled as a concurrency-style process load tool.
- `h2load` is documented and modeled as a clients plus max-streams load tool
  for HTTP/2/HTTP/3-capable runs.
- Result JSON records load tool id/name through the selected tool id, mode,
  version, parser id, command line, working directory, requested/effective
  load shape, and load-shape warnings.
- Aggregate JSON and markdown summaries surface warning text, effective
  concurrency/streams, p50/p95/p99, and single-run sample language.
- `check` reports known load tools, process availability, versions when
  available, supported protocols/families, Docker availability, and install
  notes.

Deferred from Phase 2C:

- Kestrel HTTP/3 transport proof.
- Incursa HTTP/3 target execution.
- Raw QUIC, WebTransport, MASQUE, database workloads, nginx, Caddy, quic-go,
  Docker execution, and network impairment execution.

### Phase 2D: Isolated Kestrel HTTP/3 Proof

Goal: prove the harness can distinguish Kestrel HTTP/1 endpoint success from
real Kestrel HTTP/3 negotiation before integrating Incursa.

Current exit state:

- `KestrelBenchServer` can start with explicit HTTP/1 and HTTPS endpoints from
  manifest-controlled environment variables.
- `kestrel-http3` records protocol-specific base URLs for H1 and H3.
- H3 validation uses curl `--http3-only` as protocol proof and writes
  `protocol-proof.stdout.txt` and `protocol-proof.stderr.txt`.
- `validation.json`, `result.json`, aggregate JSON, and markdown summaries can
  record requested protocol, proven protocol, proof method, certificate mode,
  fallback status, and proof errors/warnings.
- `check` reports curl H3 proof support plus h2load/oha H3 capability signals.
- H1 validation remains on the existing HTTP baseline path.

Deferred from Phase 2D:

- Incursa HTTP/3 target execution.
- H3 benchmark metrics unless a local H3-capable load tool is installed.
- Raw QUIC, WebTransport, MASQUE, database workloads, nginx, Caddy, quic-go,
  Docker execution, and network impairment execution.

### Phase 2D.1: Managed HTTP/3 Proof Fallback

Goal: unblock local Kestrel HTTP/3 validation when curl lacks
`--http3-only`, while still refusing protocol fallback and fake benchmark data.

Current exit state:

- H3 validation tries curl `--http3-only` first when the installed curl build
  actually supports it.
- If curl is unavailable or lacks real `--http3-only` support, H3 validation
  falls back to `managed-httpclient-h3-exact`.
- The managed proof client uses .NET `HttpClient`, request version `3.0`, and
  `RequestVersionExact`; HTTP/1.1 or HTTP/2 fallback fails validation.
- Managed proof records `protocol-proof.json`,
  `protocol-proof.stdout.txt`, `protocol-proof.stderr.txt`, response version,
  proof client, certificate mode, and fallback status.
- Loopback development-certificate bypass is allowed only for local proof and
  is recorded in the proof result.
- H3 benchmark execution remains separate and still requires an H3-capable load
  tool such as `h2load --h3`.

Deferred from Phase 2D.1:

- Incursa HTTP/3 target execution.
- H3 benchmark metrics when no H3-capable load tool is installed.
- Raw QUIC, WebTransport, MASQUE, database workloads, nginx, Caddy, quic-go,
  Docker execution, and network impairment execution.

### Phase 2E: Incursa HTTP/3 Target Integration

Goal: make Incursa HTTP/3 a real runnable validation target for the existing
HTTP core scenarios without coupling the neutral runner to Incursa assemblies.

Current exit state:

- `incursa-http3` starts the repo-owned Incursa HTTP/3 endpoint project as a
  local process from its manifest.
- The manifest declares the H3 base URL, UDP port, startup command, working
  directory, process-start readiness strategy, and loopback self-signed
  certificate mode.
- Incursa validates `http.core.plaintext` and `http.core.json` over proven
  HTTP/3 using the Phase 2D.1 `managed-httpclient-h3-exact` proof path.
- ProtocolLab remains implementation-neutral: it starts `dotnet`, captures
  stdout/stderr, writes artifacts, and validates through generic scenario and
  protocol-proof logic.
- H3 benchmark metrics remain unavailable unless an accepted H3-capable load
  generator is installed and proven.

Deferred from Phase 2E:

- Incursa container image packaging.
- Incursa qlog, SSL key log, and protocol metric artifact export.
- Incursa H3 benchmark metrics when no accepted H3 load generator is available.
- Raw QUIC, WebTransport, MASQUE, database workloads, nginx, Caddy, quic-go,
  Docker execution, and network impairment execution.

### Phase 2F: HTTP/3 Benchmark Enablement with Managed Fallback

Goal: produce the first honest Kestrel vs Incursa HTTP/3 benchmark path for
HTTP core scenarios without requiring local `h2load --h3`.

Current exit state:

- `managed-httpclient-h3-load` is a manifest-backed load tool under
  `load-tools/` with category `managed-lab`.
- The managed load tool runs inside ProtocolLab, requests HTTP/3 exactly with
  `RequestVersionExact`, rejects fallback, and records local certificate mode.
- The benchmark flow still validates and proves HTTP/3 before invoking any load
  tool. The managed proof client is not reused as benchmark load.
- Managed H3 load records real request counts, status-code counts,
  success/failure counts, bytes received, throughput, and latency percentiles
  from in-memory samples.
- Reports and aggregate JSON include load-tool category so managed-lab results
  are not confused with external-reference h2load benchmarks.
- `h2load --h3` remains the preferred external-reference path when installed
  and proven. `oha` H3 remains experimental.

Deferred from Phase 2F:

- External-reference H3 benchmark runs while `h2load --h3` is unavailable.
- Histogram-backed latency collection for larger campaigns.
- Incursa qlog, SSL key log, and protocol metric artifact export.
- Raw QUIC, WebTransport, MASQUE, database workloads, nginx, Caddy, quic-go,
  Docker execution, and network impairment execution.

### Phase 2G: External-Reference HTTP/3 Benchmarking with h2load

Goal: add a reproducible external-reference HTTP/3 load-generator path using
`h2load --h3`, preferring Docker load-tool provisioning while preserving local
process support.

Current exit state:

- Load-tool manifests can describe Docker execution fields for a tool image,
  command, arguments, environment, auto-pull behavior, host rewrite, and SNI.
- Docker mode is executable for load tools. It is not target Docker execution.
- The `h2load` manifest records external-reference category, process mode, and
  Docker mode using the configured image and `h2load` command.
- The runner probes process h2load and Docker h2load separately and accepts H3
  execution only when `--h3` support is proven from help output.
- Docker h2load runs rewrite loopback target URLs to `host.docker.internal`
  for host-process targets and record requested URL, effective URL, host
  rewrite, SNI, certificate mode, Docker command line, stdout/stderr, and
  optional `h2load-output.json`.
- h2load parsing prefers JSON output when available and falls back to the
  existing text parser.
- Managed-lab and external-reference results remain separate through the
  load-tool category field in result and report output.

Deferred from Phase 2G:

- Target Docker execution.
- A pinned, proven h2load H3 Docker image when the configured image is not
  available in the local environment.
- h2load qlog enablement beyond capability modeling.
- Certificate-authority-preserving SNI/connect-to refinement for future
  external-reference campaigns.
- Raw QUIC, WebTransport, MASQUE, database workloads, nginx, Caddy, quic-go,
  target Docker execution, and network impairment execution.

### Phase 2H: Repo-Owned h2load HTTP/3 Image

Goal: build and use ProtocolLab's own HTTP/3-capable h2load Docker image for
external-reference H3 benchmarks.

Current exit state:

- `tools/h2load-http3/` contains the Dockerfile and README for the repo-owned
  image.
- `scripts/build/Build-H2LoadHttp3Image.ps1` builds the image and proves
  `h2load --version`, `--h3`, `--output-file`, `--qlog-file-base`,
  `--connect-to`, and `--sni`.
- `load-tools/h2load.yaml` points Docker mode at
  `incursa/protocol-lab-h2load-http3:local` instead of the inaccessible public
  image.
- Docker h2load command construction records the Docker image, effective URL,
  connect target, host rewrite, SNI, certificate mode, output JSON path, and
  qlog base path.
- External-reference results remain separate from managed-lab results.

Deferred from Phase 2H:

- Target Docker execution.
- Strict certificate-authority preservation for local self-signed target
  certificates if h2load rejects them.
- Raw QUIC, WebTransport, MASQUE, database workloads, nginx, Caddy, quic-go,
  target Docker execution, and network impairment execution.

### Phase 2I: Benchmark Hardening and Evidence Quality

Goal: make the Phase 2H Kestrel vs Incursa H3 numbers auditable and harder to
misread without optimizing Incursa or claiming publishable evidence.

Current exit state:

- Benchmark results record evidence class, comparability status, warning
  reasons, and the difference between managed-lab and external-reference
  categories.
- Result and aggregate reports surface machine metadata, Docker backend,
  target command line, target process metadata, qlog availability, and target
  process metrics where practical.
- Docker h2load host-process runs record requested URL, effective URL, connect
  target, SNI, host rewrite mode, certificate mode, Docker image provenance,
  and qlog directory.
- Managed-lab results remain local-lab measurements, and Docker h2load
  external-reference results remain external-reference-local measurements.
- Single-repetition, localhost/shared-host, and host-rewrite runs are clearly
  marked as comparability warnings.
- Publishable benchmark evidence is not assigned automatically.

Deferred from Phase 2I:

- True isolated-host publication criteria enforcement.
- qlog parsing and protocol-counter analysis.
- Load-generator saturation measurement beyond warnings.
- Deeper load-generator CPU capture and saturation profiling.

### Phase 2J: Incursa H3 Performance Evidence Review

Goal: review the Phase 2I artifacts without optimizing Incursa and identify
the next missing measurement.

Current exit state:

- `docs/analysis/incursa-h3-phase2j-performance-review.md` compares Kestrel
  and Incursa H3 external-reference-local h2load results and managed-lab
  results.
- The review concludes the throughput gap is real enough to investigate but
  the bottleneck category remains inconclusive.
- The review identifies a measurement defect: target process metrics may refer
  to the `dotnet run` wrapper instead of the actual server process.

Deferred from Phase 2J:

- Incursa optimization.
- qlog parsing.
- New benchmark families or altered benchmark classification.

### Phase 2K: Actual Target Process Resolution and Runtime Counters

Goal: capture trustworthy runtime counters for the actual Kestrel and Incursa
server processes during existing H3 h2load benchmark cells.

Current exit state:

- Process targets launched through `dotnet` project manifests prefer direct
  `dotnet exec <Target.dll>` startup after resolving MSBuild `TargetPath`.
- Results record `diagnostic-target.json` with root process ID, resolved
  diagnostic process ID, resolution strategy, command line, working directory,
  confidence, warnings, and errors.
- `--capture-counters` optionally starts `dotnet-counters collect` against the
  resolved diagnostic process and preserves raw stdout, stderr, raw JSON/CSV,
  and `counters-summary.json`.
- A repo-local .NET tool manifest provisions `dotnet-counters` so the capture
  path does not require a global machine install.
- Result JSON and aggregate reports include counter capture status, diagnostic
  confidence, CPU mean/max, allocation rate, GC deltas, thread-pool queue max,
  and exception rate where parsing can recover them.
- Missing `dotnet-counters`, unresolved diagnostic targets, and parse gaps are
  warnings; they do not fabricate metrics or invalidate otherwise valid
  benchmarks.

Deferred from Phase 2K:

- Incursa optimization.
- Raw qlog analysis.
- Load-generator CPU capture.
- Isolated-host publishable benchmark classification.
- New benchmark families, target Docker execution, network impairment, raw
  QUIC, WebTransport, MASQUE, database workloads, nginx, Caddy, and quic-go
  target execution.

### Phase 2L: ProtocolLab v1 Stabilization and Acceptance Suite

Goal: make the existing local H3 harness reproducible from a clean checkout
without continuing Incursa performance investigation.

Current exit state:

- `docs/v1-definition-of-done.md` defines the ProtocolLab v1 local acceptance
  bar and deferred scope.
- `scripts/bootstrap/Initialize-ProtocolLab.ps1` verifies .NET, restores
  repo-local tools, verifies Docker, optionally builds the repo-owned h2load
  HTTP/3 image, builds the solution, and runs `check`.
- `scripts/acceptance/Invoke-ProtocolLabAcceptance.ps1` runs the v1 local
  acceptance flow: build, test, check, Kestrel H3 validation, Incursa H3
  validation, managed-lab H3 comparison, Docker h2load H3 comparison, and
  counter-enabled Docker h2load H3 comparison when `dotnet-counters` is
  available.
- `suites/h3-local-v1.yaml` records the suite definition used by the
  acceptance script without introducing a generalized workflow engine.
- `scripts/analysis/New-ProtocolLabRunIndex.ps1` writes a compact run index
  linking validation summaries, benchmark summaries, aggregate JSON, evidence
  classes, comparability status, and warnings.
- `check` reports .NET SDK/runtime, local tool restore state,
  `dotnet-counters`, Docker, h2load image/capability state, h2load option
  proof, oha, curl, managed H3 proof/load, required manifests, and remediation
  warnings.

Deferred from Phase 2L:

- Incursa performance optimization.
- Raw QUIC, WebTransport, MASQUE, database workloads, nginx, Caddy, quic-go,
  target Docker execution, and network impairment.
- Publishable isolated-host benchmark automation.

### Phase 3A: Docker Target Execution

Goal: add Docker-based target execution while preserving the existing process
and external target paths.

Current exit state:

- `--target-mode process|docker|external` selects target execution mode while
  process mode remains the default.
- Docker target mode can start the Kestrel benchmark server from the local
  `incursa/protocol-lab-kestrel-bench-server:local` image.
- `scripts/build/Build-KestrelBenchServerImage.ps1` builds the Kestrel target
  image from `servers/KestrelBenchServer/Dockerfile`.
- Published-port networking is supported: host validation uses
  `https://127.0.0.1:5443`, and Docker h2load reaches the same host-published
  target through `host.docker.internal`.
- Docker target runs capture `target.stdout.txt`, `target.stderr.txt`,
  `target-docker-command.txt`, `target-docker-inspect.json`, and
  `target-execution.json`.
- Result and report output records target mode, image, container name, network
  mode, published/internal ports, and local Docker-target evidence warnings.
- Docker target readiness for H3 requires a managed exact HTTP/3 readiness
  probe before validation and benchmarking continue.
- `suites/h3-local-v1-docker-target.yaml` documents the Phase 3A Kestrel H3
  Docker target suite without adding a generalized suite engine.

Deferred from Phase 3A:

- Incursa Docker target execution until the sample has a container
  build/startup contract.
- shared Docker network execution beyond modeling/documentation.
- nginx, Caddy, quic-go target execution.
- Raw QUIC, WebTransport, MASQUE, database workloads, network impairment,
  publishable isolated-host automation, and Incursa optimization.

### Phase 3B: Incursa Docker Target Contract

Goal: add the minimal Docker build/startup contract needed for ProtocolLab to
run the repo-owned Incursa HTTP/3 endpoint project as a Docker target.

Current exit state:

- `src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Dockerfile` in the repository
  builds and runs the endpoint project directly.
- The sample supports deterministic container startup through
  `PROTOCOL_LAB_H3_PORT`, `PORT`, or `--port`, keeps the loopback self-signed
  certificate behavior, and writes logs to stdout/stderr.
- `scripts/build/Build-IncursaHttp3BenchServerImage.ps1` builds the local
  `incursa/protocol-lab-incursa-http3-bench-server:local` image from the
  repository root.
- `implementations/incursa-http3.yaml` preserves the process target path and
  adds Docker image, Dockerfile, build context, environment, command, URL, and
  published UDP port contract fields for the endpoint project.
- Docker target validation uses host-published UDP `5444` and exact managed H3
  proof before benchmark data is accepted.
- Docker h2load reaches the Incursa Docker target through
  `host.docker.internal` rewriting with SNI `localhost`.
- Docker-target acceptance can run Kestrel and Incursa validation and h2load
  smoke benchmarks in the same local workflow.

Deferred from Phase 3B:

- shared Docker network execution until Phase 3C.
- nginx, Caddy, quic-go target execution.
- Docker target resource isolation and publishable benchmark automation.
- Raw QUIC, WebTransport, MASQUE, database workloads, network impairment, qlog
  parsing, and Incursa optimization.

### Phase 3C: Docker Target Isolation and Shared-Network Execution

Goal: add optional shared Docker network execution so Docker target containers
and Docker h2load containers can communicate inside a generated local Docker
network without `host.docker.internal` for benchmark traffic.

Current exit state:

- `--target-network-mode published-port|shared-docker-network` selects the
  Docker target networking mode. Published-port remains the default.
- Shared-network mode creates a generated `protocol-lab-{runId}` Docker
  network, attaches Docker target containers with stable aliases, and attaches
  Docker h2load containers to the same network.
- Managed exact H3 proof can still run from the host through the published
  validation port; benchmark traffic uses h2load `--connect-to` plus SNI
  `localhost` to preserve local certificate compatibility while routing to the
  target container alias.
- Results record target network mode, network name/id, aliases, effective
  load-tool URL, connect target, SNI, host rewrite mode, network cleanup status,
  and network warnings.
- Shared-network cells capture Docker network command, inspect, target-network
  inspect, and cleanup artifacts.
- `suites/h3-local-v1-docker-target-shared-network.yaml` documents the local
  shared-network Kestrel/Incursa H3 smoke shape.

Deferred from Phase 3C:

- target/container CPU and memory isolation controls.
- Docker Compose or multi-host benchmark orchestration.
- nginx, Caddy, quic-go target execution.
- Raw QUIC, WebTransport, MASQUE, database workloads, network impairment,
  publishable isolated-host automation, qlog parsing, and Incursa optimization.

### Phase 3D: Docker Resource Controls and Cleanup Hardening

Goal: add optional Docker CPU and memory controls for Docker target and
load-tool containers, improve cleanup reporting, and keep evidence warnings
honest.

Current exit state:

- Docker target manifests and load-tool manifests can declare optional Docker
  resource limits; CLI options can override CPU, memory, cpuset, memory-swap,
  and pids-limit values.
- Docker target and h2load Docker command construction applies requested
  limits through Docker run arguments and records the exact command line.
- Result JSON records requested and effective target/load-tool Docker resource
  limits. Effective values are parsed from Docker inspect where available.
- Evidence warnings are conditional: missing-limit warnings remain when no
  limits are applied, applied-limit reasons are recorded when limits are
  present, and CPU isolation warnings remain unless cpuset isolation is used.
- Cleanup reporting records target container, load-tool container, and
  generated-network cleanup status in result JSON, aggregate JSON, markdown
  summary, and `docker-cleanup.json`.
- `scripts/cleanup/Clear-ProtocolLabDockerResources.ps1` removes only
  ProtocolLab-labeled containers and networks, with `-WhatIf` support.

Deferred from Phase 3D:

- publishable isolated-host automation.
- Docker Compose or multi-host benchmark orchestration.
- nginx, Caddy, quic-go target execution.
- Raw QUIC, WebTransport, MASQUE, database workloads, network impairment,
  qlog parsing, and Incursa optimization.

### Phase 3E: Docker Load-Generator Metrics and Saturation Telemetry

Goal: capture Docker load-tool container CPU, memory, network, and lifecycle
metrics during benchmark execution and report load-generator saturation
warnings honestly.

Current exit state:

- Docker h2load runs can opt into `--capture-load-tool-metrics` with a
  configurable `--load-tool-metrics-interval`.
- Metrics capture samples `docker stats --no-stream --format "{{json .}}"`
  while the load-tool container is running and preserves raw, JSONL, and
  summary artifacts.
- Result JSON records load-tool container identity, metric availability,
  summarized CPU/memory/network/PID data, saturation status, saturation
  warnings, and metrics artifact paths.
- Aggregate JSON and markdown summaries include compact load-generator
  diagnostics.
- Evidence reasons replace `load-generator-cpu-not-captured` only when Docker
  stats samples were actually captured. Missing or partial stats remain
  warnings and never fabricate metrics.

Deferred from Phase 3E:

- process-mode load-generator metrics.
- publishable isolated-host automation.
- Docker Compose or multi-host benchmark orchestration.
- nginx, Caddy, quic-go target execution.
- Raw QUIC, WebTransport, MASQUE, database workloads, network impairment,
  qlog parsing, and Incursa optimization.

### Phase 3F: Docker Target Metrics Parity and Diagnostics Hardening

Goal: capture Docker target container CPU, memory, network, and lifecycle
metrics during Docker target benchmark execution using the same
raw-artifact-first model as load-generator metrics.

Current exit state:

- Docker target runs can opt into `--capture-target-container-metrics` with a
  configurable `--target-container-metrics-interval`.
- Metrics capture samples `docker stats --no-stream --format "{{json .}}"`
  while benchmark load is running and preserves target raw, JSONL, and summary
  artifacts.
- Result JSON records target Docker metric availability, summarized
  CPU/memory/network/PID data, target saturation status, saturation warnings,
  and metrics artifact paths.
- Aggregate JSON and markdown summaries include compact target-container
  diagnostics next to load-generator diagnostics.
- Evidence reasons replace broad unresolved target-resource warnings only when
  Docker target stats samples were actually captured. Missing or partial stats
  remain warnings and never fabricate metrics.

Deferred from Phase 3F:

- process-mode load-generator metrics.
- container-aware `dotnet-counters` capture.
- publishable isolated-host automation.
- Docker Compose or multi-host benchmark orchestration.
- Caddy, nginx, and quic-go target execution until explicitly scheduled.
- Raw QUIC, WebTransport, MASQUE, database workloads, network impairment,
  qlog parsing, and Incursa optimization.

### Phase 3G: Caddy HTTP/3 Target Onboarding

Goal: add Caddy as the first non-.NET production-style HTTP/3 Docker target
using the existing Docker target, shared-network, resource-control,
target-metrics, and load-generator-metrics infrastructure.

Current exit state:

- `servers/caddy/` contains a Caddy Dockerfile, Caddyfile, and target README.
- `scripts/build/Build-CaddyBenchServerImage.ps1` builds the local
  `incursa/protocol-lab-caddy-bench-server:local` image.
- `implementations/caddy-http3.yaml` is a Docker-only H3 target manifest with
  `/plaintext` and `/json` support, internal `8443/tcp+udp`, host validation
  port `5445`, shared-network alias `caddy-http3`, and Caddy `tls internal`
  certificate mode.
- Caddy validation uses the existing managed exact HTTP/3 proof and endpoint
  validation path. HTTP/1.1 or HTTP/2 fallback remains a validation failure.
- Docker h2load reaches Caddy in shared-network mode with SNI `localhost` and
  `--connect-to` toward the target network alias.
- Optional acceptance includes Caddy only when `-IncludeCaddy` is supplied.
- Caddy results use the same local Docker evidence and comparability warnings
  as Kestrel and Incursa; they are not publishable benchmark data.

Deferred from Phase 3G:

- quic-go target execution.
- raw QUIC, WebTransport, MASQUE, database workloads, network impairment,
  publishable isolated-host automation, and Incursa optimization.
- Caddy-specific qlog, SSL key log, or protocol metric exports.

### Phase 3H: nginx HTTP/3 Target Onboarding

Goal: add nginx as an optional Docker-only HTTP/3 target using the existing
Docker target, shared-network, resource-control, target-metrics,
load-generator-metrics, exact H3 proof, and Docker h2load infrastructure.

Current exit state:

- `servers/nginx/` contains a Dockerfile, nginx configuration, runtime
  certificate-generation entrypoint, and target README.
- `scripts/build/Build-NginxBenchServerImage.ps1` builds the local
  `incursa/protocol-lab-nginx-bench-server:local` image and proves `nginx -V`
  advertises `--with-http_v3_module`.
- `implementations/nginx-http3.yaml` is a Docker-only H3 target manifest with
  `/plaintext` and `/json` support, internal `8446/tcp+udp`, host validation
  port `5446`, shared-network alias `nginx-http3`, self-signed localhost
  certificate mode, and required target capability proof metadata.
- nginx validation uses the existing managed exact HTTP/3 proof and endpoint
  validation path after the nginx HTTP/3 module proof passes. HTTP/1.1 or
  HTTP/2 fallback remains a validation failure.
- Docker h2load reaches nginx in shared-network mode with SNI `localhost` and
  `--connect-to` toward the target network alias.
- Optional acceptance includes nginx validation and Docker h2load stages only
  when `-IncludeNginx` is supplied; nginx is excluded from the managed-lab
  comparison because Phase 3H's required load path is Docker h2load.
- nginx results use the same local Docker evidence and comparability warnings
  as Kestrel, Incursa, and Caddy; they are not publishable benchmark data.

Deferred from Phase 3H:

- quic-go target execution.
- raw QUIC, WebTransport, MASQUE, database workloads, network impairment,
  publishable isolated-host automation, and Incursa optimization.
- nginx-specific qlog, SSL key log, or protocol metric exports.

## Phase 3: Reports, Fairness, and Metadata

Goal: make results more reportable and auditable.

Deliverables:

- aggregate JSON writer
- richer markdown summary
- fairness-rules documentation
- host and Docker metadata capture where practical
- saturation warning surface
- repetition-aware median, best, and worst reporting

## Phase 4: Incursa HTTP/3 Target Integration

Goal: benchmark Incursa HTTP/3 through manifests and containers once an image exists.

Deliverables:

- `implementations/incursa-http3.yaml`
- `servers/IncursaBenchServer.Placeholder/` updated with integration handoff
- Incursa image readiness and artifact export contract
- optional Incursa protocol metric import through generic artifact parsing

Constraint: the runner remains neutral and does not reference Incursa protocol code directly.

Current exit state: Phase 2E made the local-process Incursa validation target
real for HTTP core over H3. Phase 4 remains the later packaging and benchmark
hardening track for container images, Incursa artifact exports, and accepted H3
load generation.

## Phase 5: H3 Protocol Workloads

Goal: add HTTP/3-specific behavior scenarios.

Deliverables:

- QPACK repeated headers scenario
- cancel-mid-response scenario
- multiplex-100-streams scenario
- H3-specific validation and metric modeling

Initial scope: define and parse the scenario inventory first. Validation and
benchmark execution remain unsupported until an H3-capable validator and load
generator are scheduled.

Current exit state: the H3 scenario inventory, H3 protocol model, unsupported
validation behavior, and optional protocol metric serialization are present.
Runnable H3 protocol benchmarking remains blocked until an H3-capable validator
and load generator are scheduled.

## Phase 6: Raw QUIC Transport Workloads

Goal: add raw QUIC transport validation and benchmarking without fake data.

Deliverables:

- handshake-cold scenario execution
- stream-throughput scenario execution
- multiplex, churn, and duplex stream scenario execution
- custom QUIC client/load-generator adapter or container contract
- QUIC metric ingestion where real metrics exist

Initial scope: define and parse the raw QUIC scenario inventory first.
Validation and benchmark execution remain unsupported until a raw QUIC validator
and load generator are scheduled.

Current exit state: raw QUIC transport scenarios, raw QUIC scenario fields,
unsupported validation behavior, optional QUIC protocol metric serialization,
and the load-generator activation contract are present. Runnable raw QUIC
benchmarking remains blocked until a raw QUIC validator and load generator are
scheduled.

## Phase 7: Network Profiles

Goal: execute scenarios under controlled network profiles.

Deliverables:

- network profile files
- provider abstraction
- provider `none`
- later `docker-tc` or `ns3-simulator` provider evaluation

Initial scope: load profile definitions and model provider support. Only
provider `none` is executable; impaired profiles remain unsupported until a
provider implementation is scheduled.

Current exit state: network profile files, profile catalog loading, matrix
selection, provider support modeling, and unsupported validation for impaired
providers are present. Actual `docker-tc` and `ns3-simulator` execution remains
future work.

## Phase 8: Additional Implementations and Future Families

Goal: broaden implementation and protocol-family coverage after the runner contract is stable.

Deliverables:

- quic-go manifests/configs
- WebTransport model and scenarios
- MASQUE model and scenarios
- database workload decision record if pursued

Initial scope: add WebTransport and MASQUE scenario/model stubs only.
Validation and benchmark execution remain unsupported until validators, load
generators, and target implementations are scheduled.

Current exit state: quic-go placeholder manifests/config folders are present;
Caddy moved into optional Phase 3G Docker-only HTTP/3 target support; nginx
moved into optional Phase 3H Docker-only HTTP/3 target support with required
HTTP/3 module proof; WebTransport and MASQUE scenario/model stubs are present;
future-family validation returns explicit unsupported outcomes; and database
workloads are deferred in a decision record.
