# Load Generator Metrics

ProtocolLab can capture Docker load-generator telemetry for Docker-mode load tools such as the repo-owned HTTP/3 `h2load` image.

## Scope

Phase 3E captures load-tool container diagnostics only. It does not add raw QUIC, WebTransport, MASQUE, database workloads, network impairment, or new target families.
Target-container Docker metrics are documented separately in
`docs/spec/docker-target-metrics.md`.

Captured fields are best-effort Docker `stats` samples:

- CPU percent
- memory usage, limit, and percent
- network receive/transmit counters
- block I/O counters
- current PID count

Raw Docker stats output remains the authoritative artifact. Parsed metrics are diagnostic summaries, not fabricated benchmark measurements.

## Artifacts

Each Docker load-tool benchmark cell can write:

- `load-tool-docker-stats.raw.txt`
- `load-tool-docker-stats.jsonl`
- `load-tool-docker-metrics-summary.json`
- `load-tool-execution.json`
- `result.json`
- `aggregate-results.json`
- `summary.md`

The result model records:

- `loadToolContainerName`
- `loadToolContainerId`
- `loadToolDockerMetricsAvailable`
- `loadToolDockerMetricsSummary`
- `loadToolSaturationStatus`
- `loadToolSaturationWarnings`
- `loadToolDockerMetricsArtifacts`

## Sampling

The CLI samples `docker stats --no-stream --format "{{json .}}"` while the Docker load-tool container is running. The default interval is one second.

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
  --capture-load-tool-metrics `
  --load-tool-metrics-interval 1
```

Acceptance can enable the same capture:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 `
  -TargetMode docker `
  -TargetNetworkMode shared-docker-network `
  -CaptureLoadToolMetrics
```

When `caddy-http3` or `nginx-http3` is selected, the same Docker h2load
metrics path is used. These targets do not add separate load-generator
implementations.

## Saturation Status

ProtocolLab reports one of:

- `load-generator-saturation-not-detected`
- `load-generator-saturation-possible`
- `load-generator-saturation-unknown`

Warnings are conservative. A possible saturation warning does not automatically fail a benchmark. Missing or partial Docker stats do not hide the benchmark output; the raw load-tool stdout/stderr and Docker stats artifacts remain available.

Common warnings include:

- `load-generator-cpu-high`
- `load-generator-cpu-unknown`
- `load-generator-memory-high`
- `load-generator-metrics-missing`
- `load-generator-metrics-partial`
- `load-generator-single-sample`

## Evidence

Captured load-generator metrics improve local diagnostic quality and can replace `load-generator-cpu-not-captured` for Docker load-tool cells. They do not make local results publishable. Docker Desktop, shared host resources, local certificates, generated Docker networks, and optional container limits still keep these results in local evidence classes with comparability warnings.

## Caveats

- Very short smoke runs can exit before more than one Docker stats sample is captured.
- Docker stats output is Docker CLI and backend dependent.
- Docker Desktop resource limits are bounded by Docker Desktop settings.
- Network counters are container-local Docker counters, not isolated physical NIC measurements.
