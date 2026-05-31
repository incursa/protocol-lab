# Incursa Docker Target Contract

This contract covers the Docker packaging for the direct Incursa HTTP/3
endpoint target. ProtocolLab builds the repo-owned Dockerfile, starts the
container, and validates the HTTPS/H3 endpoint over UDP using only the
repo-owned adapter and endpoint surfaces.

## Image Build

Build the target image from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-IncursaHttp3BenchServerImage.ps1
```

The default image tag is:

```text
incursa/protocol-lab-incursa-http3-bench-server:local
```

The Dockerfile lives at
`src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Dockerfile` and builds the
endpoint project from this repository.

## Container Contract

The Incursa container:

- runs `dotnet Incursa.ProtocolLab.Adapters.IncursaHttp3.dll --mode endpoint --port 5444`
- listens on HTTP/3 UDP port `5444`
- accepts `PROTOCOL_LAB_H3_PORT` or `--port`
- exposes `5444/udp`
- serves the public HTTP application scenarios supported by the endpoint
- writes logs to stdout/stderr
- shuts down through normal process exit

The endpoint uses a runtime-generated loopback self-signed certificate. No
private key material is committed.

## Manifest Contract

`implementations/incursa-http3.yaml` describes the Docker target:

- image tag
- Dockerfile path
- build context
- Docker network mode `published-port`
- Docker base URL `https://127.0.0.1:5444`
- Docker environment for the endpoint mode and port
- Docker command arguments `--mode endpoint --port 5444`

Docker mode is selected explicitly through the runner CLI:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate `
  --implementations incursa-http3 `
  --target-mode docker `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3
```

## Networking

Published-port mode remains the default. The target publishes UDP `5444` to the
host, and ProtocolLab validates against `https://127.0.0.1:5444`.

Shared Docker network mode is also supported by the general target runner when
the caller selects it.

## Acceptance

Docker-target acceptance builds or checks the repo-owned Incursa image:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 `
  -RunIdPrefix local-phase3b-docker-target `
  -TargetMode docker `
  -BuildTargetImages `
  -DurationSeconds 5 `
  -WarmupSeconds 1 `
  -Repetitions 1
```

If the Incursa image is missing, build it with
`scripts\build\Build-IncursaHttp3BenchServerImage.ps1` and rerun acceptance.

## Artifacts

Incursa Docker target cells write the standard validation and benchmark
artifacts plus target stdout/stderr, Docker inspect data, and Docker cleanup
records. Docker h2load runs also preserve raw stdout/stderr and output
metadata when available.

## Troubleshooting

- Docker build fails: run the build script with `-VerboseOutput` and inspect
  the Docker build log.
- UDP port conflict: stop any process or container using UDP `5444`, or update
  the manifest port mapping consistently.
- Certificate or SNI issue: validation uses the managed exact H3 proof with
  local certificate bypass recorded in artifacts.
- H3 proof fails: inspect `target.stdout.txt`, `target.stderr.txt`, and the
  protocol proof artifacts.
