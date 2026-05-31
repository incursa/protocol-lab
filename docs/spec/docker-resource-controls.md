# Docker Resource Controls

Phase 3D adds optional Docker CPU and memory controls for Docker target
containers and Docker load-tool containers.

Resource controls are local repeatability aids. They do not make a run
publishable benchmark evidence. Docker Desktop, the host scheduler, shared
kernel resources, local certificates, and single-machine networking still
matter, so resource-limited Docker results remain local evidence with
comparability warnings.

## CLI Options

Target container limits:

```powershell
--target-cpus 2
--target-memory 1g
```

Load-tool container limits:

```powershell
--load-tool-cpus 2
--load-tool-memory 1g
```

Shared options applied to both Docker target and Docker load-tool containers:

```powershell
--docker-cpuset-cpus 0-3
--docker-memory-swap 1g
--docker-pids-limit 512
```

CLI values override manifest defaults when both are present. If no values are
provided, Docker command construction remains unchanged.

## Example

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3,incursa-http3,caddy-http3,nginx-http3 `
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
  --capture-load-tool-metrics `
  --output .artifacts\runs `
  --run-id local-h3-kestrel-incursa-caddy-nginx-shared-network-limited
```

The same controls apply to Caddy and nginx when `caddy-http3` or
`nginx-http3` is selected. They have no special runner code path; Docker
CPU/memory flags are added from the generic target and load-tool
resource-control model.

## Artifacts

Resource-limited cells record:

- `target-docker-command.txt`
- `docker-command.txt`
- `target-docker-inspect.json`
- `load-tool-docker-inspect.json` when a named load-tool container is used for
  inspect capture
- `load-tool-docker-stats.raw.txt` when load-tool metrics are captured
- `load-tool-docker-stats.jsonl` when Docker stats rows are parsed
- `load-tool-docker-metrics-summary.json` when load-tool metrics are captured
- `docker-resource-limits.json`
- `docker-cleanup.json`
- `result.json`
- `summary.md`
- `aggregate-results.json`

`result.json` records requested and effective settings separately:

- `targetDockerResourceLimitsRequested`
- `targetDockerResourceLimitsEffective`
- `loadToolDockerResourceLimitsRequested`
- `loadToolDockerResourceLimitsEffective`
- `resourceLimitWarnings`
- `dockerCleanup`

When `--capture-load-tool-metrics` is enabled, `result.json` also records
load-generator CPU, memory, network, PID, saturation, and metrics artifact
fields. See `docs/spec/load-generator-metrics.md`.
When `--capture-target-container-metrics` is enabled, `result.json` records
the matching target-container CPU, memory, network, PID, saturation, and
metrics artifact fields. See `docs/spec/docker-target-metrics.md`.

Effective limits are parsed from Docker inspect when available. If Docker
inspect omits or reports a platform-specific value, ProtocolLab records a
warning instead of inventing a value.

## Evidence Warnings

When no limits are set, Docker target and load-tool results keep warnings such
as `target-container-resource-limits-missing`,
`load-tool-container-resource-limits-missing`,
`target-container-memory-limit-missing`, and
`docker-container-memory-limit-missing`.

When limits are requested and inspect confirms them, ProtocolLab records
`target-container-resource-limits-applied`,
`load-tool-container-resource-limits-applied`, and
`docker-resource-limits-local-only`.

CPU quota is not CPU isolation. The CPU isolation warning remains unless a
cpuset is configured and visible in the effective Docker settings.

## Cleanup

ProtocolLab labels its Docker target containers, resource-inspected load-tool
containers, and generated Docker networks. Normal runs stop and remove
containers and remove generated networks. Cleanup status is written to
`docker-cleanup.json`, result JSON, aggregate JSON, and the markdown summary.

Manual cleanup is intentionally label-scoped:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\cleanup\Clear-ProtocolLabDockerResources.ps1 -WhatIf
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\cleanup\Clear-ProtocolLabDockerResources.ps1
```

The cleanup script removes only resources labeled by ProtocolLab:

- `incursa.protocol-lab.target=true`
- `incursa.protocol-lab.load-tool=true`
- `incursa.protocol-lab.network=true`
