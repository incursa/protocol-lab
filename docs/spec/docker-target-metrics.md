# Docker Target Metrics

ProtocolLab can capture Docker target-container telemetry for Docker target
benchmark cells. This gives target-side CPU, memory, network, block I/O, and
PID diagnostics using the same raw-artifact-first Docker stats model as
load-generator metrics.

## Scope

Phase 3F captures Docker target container diagnostics only. It does not run
`dotnet-counters` inside containers, add new target families, optimize a
specific implementation, or change benchmark classification semantics.

Captured fields are best-effort Docker `stats` samples:

- CPU percent
- memory usage, limit, and percent
- network receive/transmit counters
- block I/O counters
- current PID count

Raw Docker stats output remains the authoritative artifact. Parsed metrics are
diagnostic summaries and are not fabricated benchmark measurements.

## Artifacts

Each Docker target benchmark cell can write:

- `target-docker-stats.raw.txt`
- `target-docker-stats.jsonl`
- `target-docker-metrics-summary.json`
- `target-docker-inspect.json`
- `target-execution.json`
- `docker-cleanup.json`
- `result.json`
- `aggregate-results.json`
- `summary.md`

The result model records:

- `targetDockerMetricsAvailable`
- `targetDockerMetricsSummary`
- `targetSaturationStatus`
- `targetSaturationWarnings`
- `targetDockerMetricsArtifacts`

## Sampling

The CLI samples `docker stats --no-stream --format "{{json .}}"` while the
benchmark load tool is running. The default interval is one second.

Enable explicitly:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3,caddy-http3,nginx-http3 `
  --target-mode docker `
  --target-network-mode shared-docker-network `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool h2load `
  --load-tool-mode docker `
  --capture-target-container-metrics `
  --target-container-metrics-interval 1
```

To capture both target and load-generator Docker metrics:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3,caddy-http3,nginx-http3 `
  --target-mode docker `
  --target-network-mode shared-docker-network `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool h2load `
  --load-tool-mode docker `
  --capture-load-tool-metrics `
  --capture-target-container-metrics
```

Acceptance can enable the same capture:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 `
  -TargetMode docker `
  -TargetNetworkMode shared-docker-network `
  -CaptureLoadToolMetrics `
  -CaptureTargetContainerMetrics
```

Add `-IncludeCaddy` and/or `-IncludeNginx` to the acceptance command to
include optional Caddy and nginx Docker target metrics stages. Without those
switches, default acceptance remains Kestrel only.

## Saturation Status

ProtocolLab reports one of:

- `target-saturation-not-detected`
- `target-saturation-possible`
- `target-saturation-unknown`

Warnings are conservative. Possible target saturation does not automatically
fail a benchmark. Missing or partial Docker stats do not hide the benchmark
output; raw load-tool stdout/stderr, target logs, and raw Docker stats remain
available.

Common warnings include:

- `target-container-cpu-high`
- `target-container-cpu-unknown`
- `target-container-memory-high`
- `target-container-metrics-missing`
- `target-container-metrics-partial`
- `target-container-single-sample`

## Runtime Counters

Target container metrics are not .NET runtime counters. They are container-level
Docker observations. `dotnet-counters` still requires a resolved host process
target unless a later phase explicitly adds container-aware runtime counter
capture.

## Evidence

Captured target container metrics improve local diagnostic quality and can
replace the broad unresolved target-resource warning for Docker target cells
when raw Docker stats samples were actually captured. They do not make local
results publishable. Docker Desktop, shared host resources, local certificates,
generated Docker networks, optional container limits, and local image tags still
keep these results in local evidence classes with comparability warnings.

## Caveats

- Very short smoke runs can exit before more than one Docker stats sample is
  captured.
- Docker stats output is Docker CLI and backend dependent.
- Docker Desktop resource limits are bounded by Docker Desktop settings.
- Network counters are container-local Docker counters, not isolated physical
  NIC measurements.
