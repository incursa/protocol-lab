# Load Tool Manifests

Phase 2B makes load tools explicit repository data instead of a hardcoded
`h2load` assumption.

## Current Tools

- `load-tools/h2load.yaml`
- `load-tools/oha.yaml`
- `load-tools/managed-httpclient-h3-load.yaml`

Both tools are selected through generic manifest fields:

- `id`
- `name`
- `description`
- `kind`
- `category`
- `supportedProtocols`
- `supportedScenarioFamilies`
- `executable`
- `dockerImage`
- `dockerCommand`
- `dockerArguments`
- `dockerEnvironment`
- `dockerAutoPull`
- `dockerHostRewrite`
- `sni`
- `defaultArguments`
- `versionCommand`
- `availabilityCheck`
- `outputParserId`
- `notes`

## Process Mode

Process mode is executable in Phase 2B and carries explicit load-shape
semantics in Phase 2C.

The runner resolves the manifest executable from `PATH`, captures version
output when available, writes raw stdout/stderr, writes
`load-tool-execution.json`, and parses metrics only through a named parser.

If no compatible process tool is available, the run records
`load-tool-unavailable` and preserves the validation artifacts.

## Managed Lab Mode

`managed-httpclient-h3-load` is a ProtocolLab-managed load tool. It runs inside
the CLI process and uses .NET `HttpClient` with:

- requested HTTP version `3.0`
- `RequestVersionExact`
- local loopback certificate bypass only when the target manifest declares a
  local/development/loopback/self-signed certificate mode

The managed tool is a load generator, not the protocol proof validator.
Validation still runs first through `managed-httpclient-h3-exact` or curl
`--http3-only`; benchmark metrics are accepted only after validation passes.

Managed-lab output records request counts, success/failure counts, status-code
counts, bytes received, throughput, and latency percentiles computed from
in-memory samples. That is acceptable for local lab runs, but larger or
publishable campaigns should move to a bounded histogram or an external
reference tool.

## Requested vs Effective Load Shape

Every result records both the requested load shape and the effective load shape.

Requested load shape is the user or scenario input:

- connections
- concurrency
- streams per connection
- request count, when applicable
- duration
- warmup
- repetitions

Effective load shape is what the selected tool/protocol combination actually
ran. This matters because different protocols and load tools do not share the
same concurrency model.

HTTP/1.1 does not have multiplexed streams. If `--streams` or
`--streams-per-connection` is supplied for HTTP/1.1, the requested value is
recorded but the effective stream count is `1`, with a warning.

`oha` is treated as a concurrency-style generator in the current harness:

- `connections` maps to `oha -c`
- `durationSeconds` maps to `oha -z`
- protocol selection maps to `--http-version`
- `streamsPerConnection` is not mapped to a parallel stream setting

`h2load` is treated as a clients plus max-streams generator:

- `connections` maps to `h2load -c`
- `durationSeconds` maps to `h2load -D`
- `streamsPerConnection` maps to `h2load -m` when HTTP/2 or HTTP/3 semantics
  are in scope

`managed-httpclient-h3-load` is treated as a concurrency-style managed H3
generator:

- `concurrency` maps to the number of concurrent request loops
- `durationSeconds` and `warmupSeconds` bound the measured run
- every request uses exact HTTP/3 and rejects fallback
- `connections` and `streamsPerConnection` are recorded as requested, but exact
  connection count and exact stream-per-connection control are not guaranteed

The actual command line is recorded in `result.json` and
`load-tool-execution.json`.

## HTTP/3 Capability Checks

Tool availability is not the same as HTTP/3 capability.

Phase 2D and 2D.1 checks:

- curl availability and whether the installed build advertises HTTP/3 plus
  `--http3-only`, and whether it accepts the option at runtime
- managed .NET `HttpClient` H3 proof attemptability
- `h2load` availability and whether help output advertises `--h3`
- `oha` availability and whether help output appears to advertise HTTP/3

HTTP/3 validation first uses curl `--http3-only` when available. If curl is not
usable, validation falls back to `managed-httpclient-h3-exact`, which requests
HTTP/3 with exact version policy and rejects fallback. HTTP/3 benchmark
execution still prefers `h2load --h3` when that capability is actually present.
If h2load H3 is unavailable, `managed-httpclient-h3-load` can produce real
local-lab H3 measurements. The managed proof client and the managed load tool
are separate code paths. `oha` remains the proven HTTP/1 baseline path in this
harness; oha H3 remains experimental unless a later phase explicitly accepts
its command and capability contract.

## Docker Mode

Phase 2G enables Docker mode for load tools only. It does not start benchmark
targets in Docker.

The initial Docker contract is for `h2load` HTTP/3 external-reference runs. The
runner resolves Docker from `PATH`, checks the configured image, optionally
pulls it when the manifest allows auto-pull, probes the containerized `h2load`
help/version output, and accepts the tool for H3 only when `--h3` is advertised.

Phase 2H replaces the inaccessible public `nghttp2/nghttp2` image with a
repo-owned local image:

```text
incursa/protocol-lab-h2load-http3:local
```

Build it with:

```powershell
.\scripts\build\Build-H2LoadHttp3Image.ps1
```

The Dockerfile lives under `tools/h2load-http3/` and builds pinned aws-lc,
nghttp3, ngtcp2, and nghttp2 sources. The build script proves `h2load --version`
and requires `--h3`, `--output-file`, `--qlog-file-base`, `--connect-to`, and
`--sni` to appear in `h2load --help`.

When the target is a local Windows host process, `127.0.0.1` inside the
container points at the container, not the host. For Docker load-tool mode the
`h2load` manifest rewrites loopback URLs to `host.docker.internal` and records:

- requested target URL
- effective load-tool URL
- host rewrite mode
- SNI value, when supplied by the manifest
- certificate mode
- Docker command line

The harness sends `--sni` when the manifest supplies an SNI value and writes
QUIC qlogs under the per-cell `qlog/` directory when `--qlog-file-base` is
available. The local Kestrel and Incursa targets use development or loopback
certificates. If h2load blocks on TLS verification, the run must report that
exact failure; ProtocolLab does not silently weaken TLS validation without
recording the command and certificate mode.

## Benchmark Evidence and Comparability

Phase 2I classifies benchmark evidence so reports do not blur smoke results,
local managed-lab results, and local external-reference results.

- `managed-httpclient-h3-load` defaults to `local-lab`.
- Docker `h2load --h3` against a host-process target defaults to
  `external-reference-local`.
- `local-smoke` is reserved for failed or unavailable benchmark attempts.
- `isolated-host` and `publishable` are not assigned automatically by the
  current harness.

Reports also surface comparability status:

- `comparable-local`
- `comparable-with-warnings`
- `not-comparable`
- `invalid`

`managed-lab` and `external-reference-local` results must not be directly
ranked together. Host rewrite, localhost/shared-host execution, single
repetition, missing target metrics, and missing load-generator saturation
checks are comparability warnings, not publication proof.

Publishable evidence still needs stronger conditions than Phase 2I provides:
stable repeated runs, isolated resources, the same effective load shape, and
reviewed protocol/qlog evidence where applicable.

Qlog support is recorded when the load tool and image prove `--qlog-file-base`.
Phase 2I retains the qlog directory and file count, but qlog parsing is still
deferred.

Target process CPU, memory, thread, and handle metrics are captured
best-effort for process targets. When capture fails, the report must say so;
the benchmark is still valid, but the evidence quality is lower.

Runtime counter capture is a separate opt-in diagnostic path. It uses
`dotnet-counters` against the resolved target process when
`--capture-counters` is supplied. Raw counter stdout, stderr, and JSON/CSV
output are preserved under the benchmark cell directory. ProtocolLab includes a
repo-local .NET tool manifest for `dotnet-counters`; run `dotnet tool restore`
on a fresh checkout. If `dotnet-counters` is unavailable, `check` reports that
fact and counter-enabled runs record a counter warning instead of fabricating
CPU, allocation, or GC values.

## v1 Bootstrap and Acceptance

Phase 2L makes the operator workflow explicit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\bootstrap\Initialize-ProtocolLab.ps1 -BuildH2LoadImage
```

The bootstrap script restores repo-local .NET tools, verifies Docker, builds
the solution, optionally builds and proves the repo-owned h2load HTTP/3 image,
and runs `check`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 `
  -RunIdPrefix local-v1-acceptance `
  -DurationSeconds 5 `
  -WarmupSeconds 1 `
  -Repetitions 1
```

For v1 acceptance, Docker h2load is required unless `-SkipExternal` is
specified. Missing `dotnet-counters` skips only the counter-enabled stage with a
clear message. Missing Docker h2load support fails the external-reference stage
instead of recording fake metrics.

## Docker Targets and Load Tools

Phase 3A adds Docker target execution separately from Docker load-tool
execution. The `--target-mode docker` option starts the benchmark target as a
container. The existing `--load-tool-mode docker` option starts the load
generator as a container.

For the Kestrel and Incursa Docker target paths, each target container publishes
its H3 UDP port to the host. Host validation proves `https://127.0.0.1:5443`
for Kestrel and `https://127.0.0.1:5444` for Incursa, while Docker h2load
reaches the same targets through the existing `host.docker.internal` rewrite.
Result JSON records target mode, target image, container name, target network
mode, published ports, internal ports, target command, and target inspect path.

Phase 3C adds `--target-network-mode shared-docker-network` for Docker target
plus Docker h2load cells. In that mode, ProtocolLab creates a generated
`protocol-lab-{runId}` network, attaches the target container using a stable
implementation alias, and runs Docker h2load on the same network. The h2load
URL keeps `localhost` and SNI `localhost` for local certificate compatibility,
while `--connect-to` routes the connection to the target alias and internal
port. Host-published ports may still be used by the host-side managed exact H3
proof before the benchmark starts.

Docker target execution still produces local evidence only. The evidence model
adds Docker target warnings for local tags, host-published ports, shared Docker
networks, generated local networks, missing container resource limits,
non-isolated CPU, non-isolated memory, and local certificate mode.

## Parsers

- `h2load`: best-effort JSON parser when `--output-file` produces JSON, with a
  text parser fallback for request rate, request counts, latency, and
  throughput fields when present.
- `oha-json`: JSON parser for request rate, request count, success/failure
  count, p50/p95/p99 latency, mean latency, and throughput.
- `managed-httpclient-h3-json`: JSON parser for managed-lab request rate,
  request counts, status-code counts, byte counts, throughput, and p50/p75/p90/
  p95/p99 latency fields.

If parsing fails, raw output remains authoritative and
`parsedMetricsAvailable` stays `false`.

## Interpreting Warnings

Warnings are not benchmark failures. They flag comparability limits, such as:

- HTTP/1.1 streams being ignored
- a localhost target
- client and server sharing host resources
- a single-repetition sample
- parser failure or unavailable parser
- missing target CPU/memory metrics
- managed-lab H3 results not being equivalent to external-reference h2load
  results

Single-run localhost numbers are useful smoke baselines, not publishable
benchmark claims. Use multiple repetitions, isolated resources, stable machine
metadata, and the same effective load shape before comparing implementations.

## Commands

Kestrel H3 managed-lab benchmark:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3 `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool managed-httpclient-h3-load `
  --concurrency 16 `
  --duration 10 `
  --warmup 2 `
  --repetitions 3 `
  --output .artifacts\runs `
  --run-id local-kestrel-h3-managed-phase2f
```

Incursa H3 managed-lab benchmark:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations incursa-http3 `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool managed-httpclient-h3-load `
  --concurrency 16 `
  --duration 10 `
  --warmup 2 `
  --repetitions 3 `
  --output .artifacts\runs `
  --run-id local-incursa-h3-managed-phase2f
```

Combined local comparison:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3,incursa-http3 `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool managed-httpclient-h3-load `
  --concurrency 16 `
  --duration 10 `
  --warmup 2 `
  --repetitions 3 `
  --output .artifacts\runs `
  --run-id local-h3-kestrel-incursa-phase2f
```

External-reference h2load H3 attempt through Docker:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3,incursa-http3 `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool h2load `
  --load-tool-mode docker `
  --connections 16 `
  --streams-per-connection 10 `
  --duration 10 `
  --warmup 2 `
  --repetitions 3 `
  --output .artifacts\runs `
  --run-id local-h3-kestrel-incursa-h2load-phase2g
```

This command is accepted only when Docker can run the repo-owned `h2load` image
and `h2load --help` proves `--h3` support. If the image is missing, cannot be
built, cannot reach the host target, fails TLS/SNI negotiation, or lacks HTTP/3
support, the run records the concrete failure and does not fabricate metrics.

External-reference h2load H3 with runtime counters:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3,incursa-http3 `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool h2load `
  --load-tool-mode docker `
  --connections 16 `
  --streams-per-connection 10 `
  --duration 10 `
  --warmup 2 `
  --repetitions 3 `
  --capture-counters `
  --counter-refresh-interval 1 `
  --output .artifacts\runs `
  --run-id local-h3-kestrel-incursa-counters-phase2k
```

Resource-limited Docker h2load can be requested with CLI overrides:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3,incursa-http3 `
  --target-mode docker `
  --target-network-mode shared-docker-network `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool h2load `
  --load-tool-mode docker `
  --connections 16 `
  --streams-per-connection 10 `
  --duration 5 `
  --warmup 1 `
  --repetitions 1 `
  --target-cpus 2 `
  --target-memory 1g `
  --load-tool-cpus 2 `
  --load-tool-memory 1g `
  --output .artifacts\runs `
  --run-id local-h3-kestrel-incursa-shared-network-limited
```

ProtocolLab records requested/effective Docker limits and cleanup state, but
resource-limited local Docker h2load remains `external-reference-local`, not
publishable benchmark evidence.

Load-generator Docker metrics can be enabled for Docker h2load:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3,incursa-http3 `
  --target-mode docker `
  --target-network-mode shared-docker-network `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool h2load `
  --load-tool-mode docker `
  --capture-load-tool-metrics `
  --load-tool-metrics-interval 1 `
  --output .artifacts\runs `
  --run-id local-h3-kestrel-incursa-loadgen-metrics
```

The runner preserves raw Docker stats output and parses CPU, memory, network,
block I/O, and PID samples best-effort. Missing or partial stats are warnings;
metrics are never invented.
