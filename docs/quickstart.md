# Quickstart

This quickstart gets you from a clean clone to build, validation, and a basic
local benchmark.

## 1. Clone

Clone `incursa/protocol-lab` and open a PowerShell prompt at the repository
root.

## 2. Restore Tools

```powershell
dotnet tool restore
```

This restores repo-local tools such as `dotnet-counters`.

## 3. Build And Test

```powershell
dotnet restore Incursa.ProtocolLab.sln
dotnet build Incursa.ProtocolLab.sln --no-restore
dotnet test Incursa.ProtocolLab.sln --no-build
```

If you are using the Codex/Workbench environment, also run:

```powershell
workbench validate --profile core
```

## 4. Run Validation

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- check
```

`check` reports .NET, local tool restore state, Docker, h2load process and
Docker capabilities, curl HTTP/3 proof capability, managed H3 proof/load
availability, `dotnet-counters`, and required manifests.

To validate a specific implementation and scenario:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate --implementations kestrel-http3 --scenarios http.core.plaintext --protocol h3
```

## 5. Run A Basic Benchmark

The simplest benchmark path is the managed HTTP/3 load generator. It keeps the
run local and does not require Docker:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run --implementations kestrel-http3 --scenarios http.core.plaintext,http.core.json --protocol h3 --load-tool managed-httpclient-h3-load --concurrency 16 --duration 10 --warmup 2 --repetitions 1
```

The managed load tool is a local-lab measurement path. It is not the same as
the external-reference Docker `h2load --h3` path.

## 6. Optional Docker Image Builds

Build the repo-owned h2load image if you want the external-reference path:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-H2LoadHttp3Image.ps1
```

The image is tagged `incursa/protocol-lab-h2load-http3:local`.

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

## 7. Run Full Acceptance

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
and target-container CPU, memory, network, and saturation summaries to
`result.json`, `aggregate-results.json`, and `summary.md`. These diagnostics do
not make local results publishable benchmark evidence.

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

nginx Phase 3H acceptance uses the Docker h2load stages as its benchmark
gate. The managed-lab comparison remains a Kestrel, Incursa, and optional
Caddy local diagnostic path because nginx can complete validation while still
surfacing intermittent managed `HttpClient` request-send failures under load.

## 8. Review Summary

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

## 9. Troubleshooting

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
evidence. Caddy uses `tls internal` inside the container and ProtocolLab
records the local certificate bypass as Caddy-specific proof metadata.
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
