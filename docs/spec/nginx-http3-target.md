# nginx HTTP/3 Target

Phase 3H adds nginx as an optional Docker-only HTTP/3 target. It is included
to exercise a production-style non-.NET server through the existing generic
Docker target, shared-network, resource-control, target metrics, load-generator
metrics, exact H3 proof, and Docker h2load infrastructure.

## Contract

- Implementation id: `nginx-http3`
- Image tag: `incursa/protocol-lab-nginx-bench-server:local`
- Target mode: `docker`
- Supported protocol: `h3`
- Supported workload family: `http.application`
- Container H3 port: `8446/tcp+udp`
- Host validation port: `5446/tcp+udp`
- Shared Docker network alias: `nginx-http3`
- Certificate mode: `nginx-self-signed-localhost-loopback-certificate`
- Required capability proof: `nginx -V` output includes
  `--with-http_v3_module`

nginx serves the HTTP core endpoints from `servers/nginx/nginx.conf`:

- `GET /plaintext` returns status `200`, body `Hello, World!`, and a
  `text/plain` content type.
- `GET /json` returns status `200`, JSON equivalent to
  `{"message":"Hello, World!"}`, and an `application/json` content type.

Compression is disabled in `nginx.conf`.

## Build and Capability Proof

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-NginxBenchServerImage.ps1
```

Optional parameters:

- `-ImageTag incursa/protocol-lab-nginx-bench-server:local`
- `-NoCache`
- `-VerboseOutput`

The build script fails if the built image does not advertise HTTP/3 module
support through `nginx -V`. The target entrypoint and runner also require the
same proof before readiness. A container that starts without HTTP/3 module
proof is not accepted as an HTTP/3 target.

## Certificate and Routing

The container entrypoint generates a short-lived self-signed localhost
certificate at runtime. No private key material is committed. ProtocolLab
records this certificate mode in target, validation, protocol proof, and result
artifacts where applicable. Managed exact HTTP/3 proof may use the existing
loopback certificate bypass, and that bypass is recorded.

Published-port validation uses:

```text
https://localhost:5446
```

Shared-network Docker h2load keeps the logical URL host and SNI as
`localhost`, then routes to the target container alias:

```text
effective URL: https://localhost:8446/plaintext
connect-to: nginx-http3:8446
SNI: localhost
```

## Validation

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate `
  --implementations nginx-http3 `
  --target-mode docker `
  --target-network-mode shared-docker-network `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --output .artifacts\runs `
  --run-id local-nginx-h3-docker-target-validate-phase3h
```

Validation passes only when the container starts, nginx HTTP/3 capability proof
succeeds, exact HTTP/3 proof succeeds, fallback is not detected, and both
endpoint scenarios match.

## Benchmark

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
  --target-cpus 2 `
  --target-memory 1g `
  --load-tool-cpus 2 `
  --load-tool-memory 1g `
  --capture-load-tool-metrics `
  --capture-target-container-metrics `
  --output .artifacts\runs `
  --run-id local-nginx-h3-docker-target-h2load-phase3h
```

Default v1 acceptance does not require nginx. Include it explicitly:

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

The nginx acceptance gate is validation plus Docker h2load. The acceptance
script intentionally excludes nginx from the managed-lab comparison because
that tool is a local diagnostic fallback and can report intermittent managed
`HttpClient` send failures against nginx even when exact validation and Docker
h2load pass.

## Artifacts

nginx Docker target cells use the generic Docker target artifact layout:

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

`target-execution.json` and `result.json` include the nginx HTTP/3 capability
proof id, status, command line, expected output, and warning fields. Qlog
directories are created by the Docker h2load path when supported by the
load-tool image. nginx itself does not claim qlog or SSL key-log exports.

## Limitations

- nginx is optional in Phase 3H acceptance.
- nginx results remain local Docker evidence and are not publishable.
- nginx HTTP/3 availability depends on the selected image build. ProtocolLab
  requires proof from the actual image used.
- Managed-lab load is not an nginx Phase 3H acceptance gate; Docker h2load is
  the required load-generator path.
- quic-go, raw QUIC, WebTransport, MASQUE, database workloads, network
  impairment, and Incursa optimization remain deferred.

## Troubleshooting

nginx image lacks HTTP/3 module: inspect the build script output. If
`nginx -V` does not include `--with-http_v3_module`, the target is blocked and
must not be treated as supported.

nginx container fails to start: inspect `target.stderr.txt`,
`target.stdout.txt`, `target-docker-command.txt`, and
`target-docker-inspect.json`.

H3 proof fails: inspect `protocol-proof.json`, `protocol-proof.stderr.txt`,
and `target-execution.json`; confirm Docker Desktop supports UDP port
publishing and that the nginx container is still running.

Certificate or SNI mismatch: confirm the logical URL host is `localhost`, the
load-tool SNI is `localhost`, and shared-network runs include
`--connect-to=nginx-http3:8446`.

h2load cannot reach target: inspect `h2load.stderr.txt`,
`docker-command.txt`, `docker-network-inspect.json`, and
`target-docker-network-inspect.json`.

UDP port conflict: free host UDP port `5446` or update the nginx manifest and
rerun validation. Keep the internal container port aligned with the
`--connect-to` mapping.
