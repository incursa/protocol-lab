# Incursa Protocol Lab

Incursa Protocol Lab is a local validation and benchmarking harness for modern
HTTP and transport protocol implementations. The runner is implementation
neutral: implementations are described through manifests and started as
processes, Docker targets, or external targets, and runner assemblies do not
reference Incursa protocol assemblies.

## v1 Support

ProtocolLab v1 is local operational v1. It supports:

- Kestrel HTTP/1 validation.
- Kestrel HTTP/3 validation.
- Incursa HTTP/3 validation through the repo-owned adapter project and endpoint target.
- Managed-lab HTTP/3 comparison with `managed-httpclient-h3-load`.
- External-reference HTTP/3 comparison with the repo-owned Docker
  `h2load --h3` image.
- Optional `dotnet-counters` runtime diagnostics for counter-enabled H3 runs.
- qlog capture paths for Docker h2load when the image proves
  `--qlog-file-base`.
- target process metrics, evidence classification, comparability warnings,
  markdown summaries, and aggregate JSON.

Phase 3A-3C also supports Kestrel and Incursa HTTP/3 Docker target execution
for local validation and h2load benchmarking while keeping process mode as the
default. Docker target mode supports both host published-port traffic and an
optional shared Docker network for Docker h2load benchmark traffic.
Phase 3D adds optional Docker CPU and memory limits for Docker target and
Docker load-tool containers, plus cleanup reporting for labeled ProtocolLab
containers and networks.
Phase 3E adds optional Docker stats capture for Docker h2load load-generator
containers, including CPU, memory, network deltas, and conservative saturation
warnings.
Phase 3F adds optional Docker stats capture for Docker target containers, so
Docker target runs can report target CPU, memory, network deltas, and
target-side saturation warnings alongside load-generator telemetry.
Phase 3G adds Caddy as an optional Docker-only HTTP/3 target using the same
shared-network Docker h2load, resource-control, target metrics, and
load-generator metrics path as Kestrel and Incursa.
Phase 3H adds nginx as an optional Docker-only HTTP/3 target. nginx support
requires an image whose `nginx -V` output proves the HTTP/3 module is present
before validation or benchmarking.

Local results are not publishable benchmark evidence. They are shared-host
smoke and regression data unless an explicit later phase adds isolated-host
controls.

## Deferred

Raw QUIC workloads, WebTransport, MASQUE, database workloads, quic-go
execution, network impairment, publishable isolated-host automation, and
Incursa optimization work are deferred beyond the current local operational
scope. Caddy and nginx are available only as optional local Docker HTTP/3
evidence.

## Quick Bootstrap

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\bootstrap\Initialize-ProtocolLab.ps1 -BuildH2LoadImage
```

This restores repo-local .NET tools, verifies Docker, builds the solution,
optionally builds/proves the repo-owned h2load HTTP/3 image, and runs
`protocol-lab check`.

Docker Desktop is required for the external-reference h2load acceptance path.
Add `-BuildTargetImages -BuildIncursaTargetImage` to also build the local
Kestrel and Incursa Docker target images.
Build the optional Caddy target image separately with
`scripts\build\Build-CaddyBenchServerImage.ps1`.
Build the optional nginx target image separately with
`scripts\build\Build-NginxBenchServerImage.ps1`.

## Quick Acceptance

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 `
  -RunIdPrefix local-v1-acceptance `
  -DurationSeconds 5 `
  -WarmupSeconds 1 `
  -Repetitions 1
```

Artifacts are written under `.artifacts\runs\{runId}`. Benchmark runs produce
`summary.md`, `aggregate-results.json`, and per-cell raw stdout/stderr files.
The acceptance script also writes `.artifacts\runs\index.md`.

The Incursa HTTP/3 stages are optional. Build the repo-owned Incursa endpoint
image with `scripts\build\Build-IncursaHttp3BenchServerImage.ps1` and use the
`incursa-http3` manifests.

## CLI

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- list implementations
dotnet run --project src\Incursa.ProtocolLab.Cli -- list scenarios
dotnet run --project src\Incursa.ProtocolLab.Cli -- check
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate --implementations kestrel-http3 --scenarios http.core.plaintext --protocol h3
dotnet run --project src\Incursa.ProtocolLab.Cli -- run --implementations kestrel-http3,incursa-http3 --scenarios http.core.plaintext,http.core.json --protocol h3 --load-tool h2load --load-tool-mode docker
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate --implementations kestrel-http3,incursa-http3 --target-mode docker --scenarios http.core.plaintext,http.core.json --protocol h3
dotnet run --project src\Incursa.ProtocolLab.Cli -- run --implementations kestrel-http3,incursa-http3 --target-mode docker --target-network-mode shared-docker-network --scenarios http.core.plaintext,http.core.json --protocol h3 --load-tool h2load --load-tool-mode docker
dotnet run --project src\Incursa.ProtocolLab.Cli -- run --implementations kestrel-http3,incursa-http3 --target-mode docker --target-network-mode shared-docker-network --scenarios http.core.plaintext,http.core.json --protocol h3 --load-tool h2load --load-tool-mode docker --target-cpus 2 --target-memory 1g --load-tool-cpus 2 --load-tool-memory 1g
dotnet run --project src\Incursa.ProtocolLab.Cli -- run --implementations kestrel-http3,incursa-http3 --target-mode docker --target-network-mode shared-docker-network --scenarios http.core.plaintext,http.core.json --protocol h3 --load-tool h2load --load-tool-mode docker --capture-load-tool-metrics
dotnet run --project src\Incursa.ProtocolLab.Cli -- run --implementations kestrel-http3,incursa-http3 --target-mode docker --target-network-mode shared-docker-network --scenarios http.core.plaintext,http.core.json --protocol h3 --load-tool h2load --load-tool-mode docker --capture-load-tool-metrics --capture-target-container-metrics
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate --implementations caddy-http3 --target-mode docker --target-network-mode shared-docker-network --scenarios http.core.plaintext,http.core.json --protocol h3
dotnet run --project src\Incursa.ProtocolLab.Cli -- run --implementations kestrel-http3,incursa-http3,caddy-http3 --target-mode docker --target-network-mode shared-docker-network --scenarios http.core.plaintext,http.core.json --protocol h3 --load-tool h2load --load-tool-mode docker --capture-load-tool-metrics --capture-target-container-metrics
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate --implementations nginx-http3 --target-mode docker --target-network-mode shared-docker-network --scenarios http.core.plaintext,http.core.json --protocol h3
dotnet run --project src\Incursa.ProtocolLab.Cli -- run --implementations kestrel-http3,incursa-http3,caddy-http3,nginx-http3 --target-mode docker --target-network-mode shared-docker-network --scenarios http.core.plaintext,http.core.json --protocol h3 --load-tool h2load --load-tool-mode docker --capture-load-tool-metrics --capture-target-container-metrics
dotnet run --project src\Incursa.ProtocolLab.Cli -- report --run-id <id>
```

Benchmark data is accepted only when validation passes for the selected
implementation and scenario.

## License

Apache-2.0.
