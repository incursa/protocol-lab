# Caddy HTTP/3 Target

Phase 3G adds Caddy as the first non-.NET production-style HTTP/3 Docker
target. It exercises the existing ProtocolLab Docker target, shared-network,
resource-control, target metrics, and Docker h2load load-generator metrics
infrastructure without coupling the runner to Caddy-specific assemblies.

## Contract

- Implementation id: `caddy-http3`
- Image tag: `incursa/protocol-lab-caddy-bench-server:local`
- Target mode: `docker`
- Supported protocol: `h3`
- Supported workload family: `http.application`
- Container H3 port: `8443/tcp+udp`
- Host validation port: `5445/tcp+udp`
- Shared Docker network alias: `caddy-http3`
- Certificate mode: `caddy-internal-local-ca-loopback-certificate`

Caddy serves the Phase 3G HTTP core endpoints from `servers/caddy/Caddyfile`:

- `GET /plaintext` returns status `200`, body `Hello, World!`, and a
  `text/plain` content type.
- `GET /json` returns status `200`, JSON equivalent to
  `{"message":"Hello, World!"}`, and an `application/json` content type.

Compression is not enabled in the Caddyfile.

## Build

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-CaddyBenchServerImage.ps1
```

Optional parameters:

- `-ImageTag incursa/protocol-lab-caddy-bench-server:local`
- `-NoCache`
- `-VerboseOutput`

The Dockerfile uses the upstream Caddy image and copies only the repo-local
Caddyfile. No private key material is committed.

## Certificate and Routing

Caddy uses `tls internal`, which creates local CA material inside the
container at runtime. ProtocolLab records this certificate mode in
`target-execution.json`, `validation.json`, `protocol-proof.json`, and
`result.json` surfaces where applicable. Managed exact HTTP/3 proof may use
the existing loopback certificate bypass, and that bypass is recorded.

Published-port validation uses:

```text
https://localhost:5445
```

Shared-network Docker h2load keeps the logical URL host and SNI as
`localhost`, then routes to the target container alias:

```text
effective URL: https://localhost:8443/plaintext
connect-to: caddy-http3:8443
SNI: localhost
```

## Validation

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate `
  --implementations caddy-http3 `
  --target-mode docker `
  --target-network-mode shared-docker-network `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --output .artifacts\runs `
  --run-id local-caddy-h3-docker-target-validate-phase3g
```

Validation passes only when the container starts, exact HTTP/3 proof succeeds,
fallback is not detected, and both endpoint scenarios match.

## Benchmark

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
  --target-cpus 2 `
  --target-memory 1g `
  --load-tool-cpus 2 `
  --load-tool-memory 1g `
  --capture-load-tool-metrics `
  --capture-target-container-metrics `
  --output .artifacts\runs `
  --run-id local-caddy-h3-docker-target-h2load-phase3g
```

Default v1 acceptance does not require Caddy. Include it explicitly:

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

## Artifacts

Caddy Docker target cells use the generic Docker target artifact layout:

- `target.stdout.txt`
- `target.stderr.txt`
- `target-docker-command.txt`
- `target-docker-inspect.json`
- `target-execution.json`
- `target-docker-stats.raw.txt`
- `target-docker-stats.jsonl`
- `target-docker-metrics-summary.json`
- `validation.json`
- `protocol-proof.json`
- `result.json`
- `h2load.stdout.txt`
- `h2load.stderr.txt`
- `h2load-output.json`
- `manifest.json`
- `scenario.json`
- `notes.txt` when warnings or errors occur

Qlog directories are created by the Docker h2load path when supported by the
load-tool image. Caddy itself does not claim qlog or SSL key-log exports.

## Limitations

- Caddy is optional in Phase 3G acceptance.
- Caddy results remain local Docker evidence and are not publishable.
- nginx, quic-go, raw QUIC, WebTransport, MASQUE, database workloads, network
  impairment, and Incursa optimization remain deferred.

## Troubleshooting

Caddy container fails to start: inspect `target.stderr.txt`,
`target.stdout.txt`, `target-docker-command.txt`, and
`target-docker-inspect.json`.

H3 proof fails: inspect `protocol-proof.json` and
`protocol-proof.stderr.txt`; confirm Docker Desktop supports UDP port
publishing and that the Caddy container is still running.

Certificate or SNI mismatch: confirm the logical URL host is `localhost`, the
load-tool SNI is `localhost`, and shared-network runs include
`--connect-to=caddy-http3:8443`.

h2load cannot reach target: inspect `h2load.stderr.txt`,
`docker-command.txt`, `docker-network-inspect.json`, and
`target-docker-network-inspect.json`.

UDP port conflict: free host UDP port `5445` or update the Caddy manifest and
rerun validation. Keep the internal container port aligned with the
`--connect-to` mapping.
