# Docker Target Execution

Phase 3A adds target-side Docker execution while preserving the existing
process and external target modes.

## Target Modes

- `process`: the runner starts the manifest command on the host. This remains
  the default path.
- `docker`: the runner starts a target container from the implementation
  manifest, captures Docker command/inspect/log artifacts, validates the
  target, and only benchmarks after validation passes.
- `external`: the runner uses a pre-started target supplied with `--base-url`.

Docker target execution is separate from Docker load-tool execution. A run can
use a Docker target, a Docker h2load load tool, both, or neither.

## Target Images

Build the local Kestrel target image:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-KestrelBenchServerImage.ps1
```

The default tag is:

```text
incursa/protocol-lab-kestrel-bench-server:local
```

Build the local Incursa HTTP/3 target image from the repo-owned adapter
project:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-IncursaHttp3BenchServerImage.ps1
```

The default tag is:

```text
incursa/protocol-lab-incursa-http3-bench-server:local
```

Build the optional local Caddy HTTP/3 target image:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-CaddyBenchServerImage.ps1
```

The default tag is:

```text
incursa/protocol-lab-caddy-bench-server:local
```

Build the optional local nginx HTTP/3 target image:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-NginxBenchServerImage.ps1
```

The default tag is:

```text
incursa/protocol-lab-nginx-bench-server:local
```

The nginx script proves `nginx -V` advertises HTTP/3 module support before
the image is treated as usable.

## Kestrel Docker Target

Run Kestrel H3 validation through Docker target mode:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate `
  --implementations kestrel-http3 `
  --target-mode docker `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --output .artifacts\runs `
  --run-id local-kestrel-h3-docker-target-validate
```

Run Kestrel H3 with Docker h2load against the Docker target:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3 `
  --target-mode docker `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool h2load `
  --load-tool-mode docker `
  --connections 16 `
  --streams-per-connection 10 `
  --duration 5 `
  --warmup 1 `
  --repetitions 1 `
  --output .artifacts\runs `
  --run-id local-kestrel-h3-docker-target-h2load
```

## Networking

Phase 3A/3B supports `published-port` mode. Phase 3C adds optional
`shared-docker-network` mode for Docker target plus Docker load-tool benchmark
traffic. Published-port mode remains the default.

The Kestrel target container publishes:

- `5080/tcp` for HTTP/1 readiness and baseline access.
- `5443/tcp` and `5443/udp` for HTTPS/HTTP/3.

Host validation calls `https://127.0.0.1:5443`. Docker h2load calls the same
logical target after the existing load-tool rewrite changes loopback hosts to
`host.docker.internal`.

The Incursa target container publishes:

- `5444/udp` for HTTPS/HTTP/3.

Host validation calls `https://127.0.0.1:5444`. Docker h2load calls the same
logical target through `host.docker.internal` rewriting and sends SNI
`localhost`.

The Caddy target container publishes:

- `8443/tcp` and `8443/udp` internally, with host port `5445`.

Host validation calls `https://localhost:5445`. In shared-network mode Docker
h2load keeps the logical URL host and SNI as `localhost`, then uses
`--connect-to` toward the `caddy-http3` network alias and internal UDP port
`8443`.

The nginx target container publishes:

- `8446/tcp` and `8446/udp` internally, with host port `5446`.

Host validation calls `https://localhost:5446`. In shared-network mode Docker
h2load keeps the logical URL host and SNI as `localhost`, then uses
`--connect-to` toward the `nginx-http3` network alias and internal UDP port
`8446`.

In `shared-docker-network` mode ProtocolLab creates a generated network named
`protocol-lab-{runId}`, attaches the target container with a stable alias based
on the implementation id, and attaches the Docker h2load container to the same
network. Host-published ports are still kept for host-side managed exact H3
proof; benchmark traffic from Docker h2load uses the shared network instead of
`host.docker.internal`.

Because the local development certificates are loopback-oriented, h2load keeps
the logical URL host and SNI as `localhost` and uses `--connect-to` to route the
connection to the target container alias and internal UDP port. For example:

```text
effective URL: https://localhost:5444/plaintext
connect-to: incursa-http3:5444
SNI: localhost
```

The generated network is removed when the target lifecycle ends. Cleanup status
is recorded in `result.json` and `target-execution.json`. Cleanup failures are
warnings and are also written to `docker-network-cleanup.txt`.

## Certificates

The Kestrel Docker target generates a short-lived local certificate at
container startup when `PROTOCOL_LAB_GENERATE_LOCAL_CERT=true`. Private key
material is not committed. ProtocolLab records local certificate bypass and
marks the result as local evidence only.

The Incursa Docker target uses the runtime-generated
short-lived loopback self-signed certificate. Private key material is not
committed. ProtocolLab records certificate mode
`loopback-self-signed-certificate`.

The Caddy Docker target uses Caddy `tls internal`, generating local CA
material inside the container at runtime. Private key material is not
committed. ProtocolLab records certificate mode
`caddy-internal-local-ca-loopback-certificate` and local proof bypass in
`protocol-proof.json`.

The nginx Docker target generates a short-lived self-signed localhost
certificate inside the container at runtime. Private key material is not
committed. ProtocolLab records certificate mode
`nginx-self-signed-localhost-loopback-certificate` and local proof bypass in
`protocol-proof.json`.

The runtime image installs `libmsquic` from Microsoft's Ubuntu 24.04 package
feed because Linux HTTP/3 support in .NET requires MsQuic to be available in
the container.

## Artifacts

Docker target cells write the normal validation and benchmark artifacts plus:

- `target.stdout.txt`
- `target.stderr.txt`
- `target-docker-command.txt`
- `target-docker-inspect.json`
- `target-docker-stats.raw.txt` when target metrics capture is enabled
- `target-docker-stats.jsonl` when target metrics capture is enabled
- `target-docker-metrics-summary.json` when target metrics capture is enabled
- `target-docker-network-inspect.json` when shared-network mode is used
- `docker-network-command.txt` when shared-network mode is used
- `docker-network-inspect.json` when shared-network mode is used
- `docker-network-cleanup.txt` when shared-network mode is used
- `target-execution.json`
- `notes.txt` when warnings or errors exist

For h2load runs, load-tool stdout/stderr, h2load command/output, qlog paths,
`result.json`, `summary.md`, and `aggregate-results.json` remain under
`.artifacts/runs/{runId}`.
When `--capture-load-tool-metrics` is enabled, Docker h2load cells also write
`load-tool-docker-stats.raw.txt`, `load-tool-docker-stats.jsonl`, and
`load-tool-docker-metrics-summary.json`.
When `--capture-target-container-metrics` is enabled, Docker target cells also
write target Docker stats raw, JSONL, and summary artifacts. See
`docs/spec/docker-target-metrics.md`.

## Evidence

Docker target execution does not make a local run publishable. Results are
still local shared-host evidence unless a later isolated-host workflow controls
CPU, memory, network, load-generator saturation, repetition stability, and
publication criteria.

Docker target runs add warnings such as:

- `docker-target-local`
- `host-published-port`
- `docker-target-image-local-tag`
- `target-container-resource-limits-missing`
- `target-container-cpu-not-isolated`
- `target-container-memory-limit-missing`
- `docker-network-shared-host`
- `shared-docker-network`
- `docker-network-local`
- `docker-network-generated`
- `target-host-port-still-published-for-validation`
- `certificate-sni-connect-to-routing`
- `certificate-mode-local-dev`
- `docker-resource-limits-local-only` when optional local Docker limits are
  applied
- `target-container-metrics-captured` when target Docker stats samples were
  captured
- `target-container-metrics-missing` or `target-container-cpu-not-captured`
  when target Docker stats were requested or expected but unavailable

## Incursa Docker Target

Phase 3B adds Incursa Docker target execution through
`implementations/incursa-http3.yaml` and the repo-owned Dockerfile in
`src/Incursa.ProtocolLab.Adapters.IncursaHttp3`. Process mode remains the
default and starts the same endpoint project through `dotnet`.

Incursa Docker validation:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate `
  --implementations incursa-http3 `
  --target-mode docker `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --output .artifacts\runs `
  --run-id local-incursa-h3-docker-target-validate
```

Incursa Docker target with Docker h2load:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations incursa-http3 `
  --target-mode docker `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool h2load `
  --load-tool-mode docker `
  --connections 16 `
  --streams-per-connection 10 `
  --duration 5 `
  --warmup 1 `
  --repetitions 1 `
  --output .artifacts\runs `
  --run-id local-incursa-h3-docker-target-h2load
```

See `docs/spec/incursa-docker-target-contract.md` for the full Incursa
contract and troubleshooting notes.

## Caddy Docker Target

Phase 3G adds optional Caddy Docker target execution through
`implementations/caddy-http3.yaml` and `servers/caddy/Caddyfile`.

Caddy Docker validation:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate `
  --implementations caddy-http3 `
  --target-mode docker `
  --target-network-mode shared-docker-network `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --output .artifacts\runs `
  --run-id local-caddy-h3-docker-target-validate
```

Caddy Docker target with Docker h2load:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations caddy-http3 `
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
  --output .artifacts\runs `
  --run-id local-caddy-h3-docker-target-h2load
```

See `docs/spec/caddy-http3-target.md` for the full Caddy contract and
troubleshooting notes.

## nginx Docker Target

Phase 3H adds optional nginx Docker target execution through
`implementations/nginx-http3.yaml` and `servers/nginx/nginx.conf`.

nginx Docker validation:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate `
  --implementations nginx-http3 `
  --target-mode docker `
  --target-network-mode shared-docker-network `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --output .artifacts\runs `
  --run-id local-nginx-h3-docker-target-validate
```

nginx Docker target with Docker h2load:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations nginx-http3 `
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
  --output .artifacts\runs `
  --run-id local-nginx-h3-docker-target-h2load
```

See `docs/spec/nginx-http3-target.md` for the full nginx contract and
troubleshooting notes.

## Shared-Network Smoke Command

Run Kestrel, Incursa, optional Caddy, and optional nginx Docker targets with
Docker h2load on a generated shared network:

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
  --output .artifacts\runs `
  --run-id local-h3-kestrel-incursa-caddy-nginx-shared-network-h2load
```

This remains local `external-reference-local` evidence with comparability
warnings. It reduces host rewrite dependence for benchmark traffic, but it does
not provide CPU isolation, memory limits, independent hosts, network
impairment control, or publishable benchmark automation.

## Resource-Limited Shared-Network Smoke Command

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

Resource controls are recorded as requested/effective Docker metadata and
cleanup artifacts. They improve local repeatability but do not make results
publishable. See `docs/spec/docker-resource-controls.md`.
