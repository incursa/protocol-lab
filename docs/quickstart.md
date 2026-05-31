# Quickstart

## 1. Clone

Clone `incursa/protocol-lab` and open a PowerShell prompt at the repository
root.

## 2. Restore Tools

```powershell
dotnet tool restore
```

This restores repo-local tools such as `dotnet-counters`.

## 3. Build the h2load Image

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-H2LoadHttp3Image.ps1
```

The image is tagged `incursa/protocol-lab-h2load-http3:local`. It is required
for v1 external-reference h2load acceptance.

To use Docker target mode, also build the Kestrel and Incursa target images:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-KestrelBenchServerImage.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-IncursaHttp3BenchServerImage.ps1
```

The images are tagged:

```text
incursa/protocol-lab-kestrel-bench-server:local
incursa/protocol-lab-incursa-http3-bench-server:local
```

To include optional Caddy HTTP/3 target runs, build the Caddy image:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-CaddyBenchServerImage.ps1
```

The image is tagged:

```text
incursa/protocol-lab-caddy-bench-server:local
```

To include optional nginx HTTP/3 target runs, build the nginx image:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-NginxBenchServerImage.ps1
```

The image is tagged:

```text
incursa/protocol-lab-nginx-bench-server:local
```

The script proves `nginx -V` includes HTTP/3 module support before reporting
success.

## 4. Run Check

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- check
```

`check` reports .NET, local tool restore state, Docker, h2load process and
Docker capabilities, curl HTTP/3 proof capability, managed H3 proof/load
availability, `dotnet-counters`, and required manifests.

## 5. Run v1 Acceptance

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 `
  -RunIdPrefix local-v1-acceptance `
  -DurationSeconds 5 `
  -WarmupSeconds 1 `
  -Repetitions 1
```

For the shortest non-Docker smoke path, add `-SkipExternal -SkipCounters`.

For Docker target acceptance:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 `
  -RunIdPrefix local-phase3c-docker `
  -TargetMode docker `
  -BuildTargetImages `
  -DurationSeconds 5 `
  -WarmupSeconds 1 `
  -Repetitions 1
```

For Docker target acceptance with shared Docker network benchmark traffic:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 `
  -RunIdPrefix local-phase3c-docker-shared-network `
  -TargetMode docker `
  -TargetNetworkMode shared-docker-network `
  -DurationSeconds 5 `
  -WarmupSeconds 1 `
  -Repetitions 1
```

For an optional resource-limited Docker smoke, add:

```powershell
  -TargetCpus 2 `
  -TargetMemory 1g `
  -LoadToolCpus 2 `
  -LoadToolMemory 1g
```

For Docker h2load load-generator diagnostics, add:

```powershell
  -CaptureLoadToolMetrics `
  -LoadToolMetricsIntervalSeconds 1
```

For Docker target-container diagnostics, add:

```powershell
  -CaptureTargetContainerMetrics `
  -TargetContainerMetricsIntervalSeconds 1
```

Use both switches to capture target and load-generator Docker telemetry in the
same Docker target h2load stages.

This writes Docker stats artifacts per benchmark cell and adds load-generator
and target-container CPU, memory, network, and saturation summaries to `result.json`,
`aggregate-results.json`, and `summary.md`. These diagnostics do not make local
results publishable benchmark evidence.

To include the optional Caddy HTTP/3 target in Docker acceptance:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 `
  -RunIdPrefix local-phase3g-caddy `
  -TargetMode docker `
  -TargetNetworkMode shared-docker-network `
  -DurationSeconds 5 `
  -WarmupSeconds 1 `
  -Repetitions 1 `
  -CaptureLoadToolMetrics `
  -CaptureTargetContainerMetrics `
  -IncludeCaddy
```

To include both optional Caddy and nginx HTTP/3 targets in Docker acceptance:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 `
  -RunIdPrefix local-phase3h-nginx `
  -TargetMode docker `
  -TargetNetworkMode shared-docker-network `
  -DurationSeconds 5 `
  -WarmupSeconds 1 `
  -Repetitions 1 `
  -CaptureLoadToolMetrics `
  -CaptureTargetContainerMetrics `
  -IncludeCaddy `
  -IncludeNginx
```

nginx Phase 3H acceptance uses the Docker h2load stages as its benchmark gate.
The managed-lab comparison remains a Kestrel, Incursa, and optional Caddy
local diagnostic path because nginx can complete validation while still
surfacing intermittent managed `HttpClient` request-send failures under load.

## 6. Review Summary

Open:

```text
.artifacts\runs\index.md
```

Each benchmark run also has:

```text
.artifacts\runs\{runId}\summary.md
.artifacts\runs\{runId}\aggregate-results.json
```

Validation-only runs write `validation-results.json`.

## 7. Troubleshooting

Docker unavailable: start Docker Desktop and rerun `check`. Use
`-SkipDocker` only for bootstrap paths that do not need external-reference
h2load.

h2load image missing: run
`scripts\build\Build-H2LoadHttp3Image.ps1` or rerun bootstrap with
`-BuildH2LoadImage`.

Kestrel Docker target image missing: run
`scripts\build\Build-KestrelBenchServerImage.ps1` or rerun bootstrap with
`-BuildTargetImages`.

Incursa Docker target image missing: run
`scripts\build\Build-IncursaHttp3BenchServerImage.ps1` or rerun bootstrap with
`-BuildIncursaTargetImage`. Docker target acceptance fails on a missing
Incursa image unless `-SkipIncursaDockerTarget` is supplied.

Caddy Docker target image missing: run
`scripts\build\Build-CaddyBenchServerImage.ps1`. Caddy is optional; default
acceptance skips it unless `-IncludeCaddy` is supplied.

nginx Docker target image missing or lacking HTTP/3: run
`scripts\build\Build-NginxBenchServerImage.ps1`. nginx is optional; default
acceptance skips it unless `-IncludeNginx` is supplied. If `nginx -V` does not
advertise `--with-http_v3_module`, the target must remain unavailable rather
than claiming HTTP/3 support.

curl lacks HTTP/3: this is expected on many Windows installs. ProtocolLab uses
managed exact HTTP/3 validation when curl cannot prove `--http3-only`.

dotnet-counters unavailable: run `dotnet tool restore` from the repo root.
Counter-enabled acceptance is skipped when the tool is unavailable.

Incursa target image missing: `implementations\incursa-http3.yaml` and
`implementations\incursa-http3-adapter-v1.yaml` both point at the repo-owned
adapter project. Rebuild the image with
`scripts\build\Build-IncursaHttp3BenchServerImage.ps1` if it is missing.

Certificate or loopback issues: Kestrel uses the ASP.NET Core development
certificate or an explicit local PFX. Incursa uses its runtime-generated
loopback self-signed certificate. ProtocolLab records local certificate bypass
in proof artifacts; it does not silently turn local proof into publishable
evidence.
Caddy uses `tls internal` inside the container and ProtocolLab records the
local certificate bypass as Caddy-specific proof metadata.
nginx generates a short-lived self-signed localhost certificate inside the
container and records certificate bypass plus nginx HTTP/3 module proof in
target and result artifacts.

nginx H3 proof fails: inspect `protocol-proof.json`,
`protocol-proof.stderr.txt`, `target.stderr.txt`, `target-execution.json`, and
`target-docker-inspect.json`. Common causes are a missing nginx HTTP/3 module,
UDP port `5446` already in use, a container startup failure, or a
certificate/SNI mismatch. Shared-network h2load should route with
`--connect-to=nginx-http3:8446` and SNI `localhost`.

Caddy H3 proof fails: inspect `protocol-proof.json`,
`protocol-proof.stderr.txt`, `target.stderr.txt`, and
`target-docker-inspect.json`. Common causes are a Caddy container startup
failure, UDP port `5445` already in use, or a certificate/SNI mismatch.

`host.docker.internal` issues: published-port Docker h2load rewrites loopback
host-process or host-published Docker targets to `host.docker.internal`.
Shared-network Docker target mode avoids that rewrite for benchmark traffic by
attaching the target and h2load containers to a generated Docker network and
using h2load `--connect-to` with SNI `localhost`. Host-published ports may
still be used for managed H3 proof validation. If Docker cannot reach the
target, inspect the per-cell `docker-command.txt`, `target-docker-command.txt`,
`docker-network-inspect.json`, `load-tool.stderr.txt`, and `notes.txt`.

Docker cleanup: normal runs remove ProtocolLab containers and generated
networks. If a run is interrupted, inspect with
`scripts\cleanup\Clear-ProtocolLabDockerResources.ps1 -WhatIf`; run the same
script without `-WhatIf` to remove only ProtocolLab-labeled resources.
