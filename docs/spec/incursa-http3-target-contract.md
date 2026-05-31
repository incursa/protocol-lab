# Incursa HTTP/3 Target Contract

This contract describes the direct Incursa HTTP/3 endpoint target that
ProtocolLab can run as a process or container. The target is implemented by
the repo-owned `src/Incursa.ProtocolLab.Adapters.IncursaHttp3` project and
serves the public HTTP application scenarios through a real Incursa QUIC/HTTP/3
endpoint.

## Current State

- Manifest: `implementations/incursa-http3.yaml`
- Project: `src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Incursa.ProtocolLab.Adapters.IncursaHttp3.csproj`
- Dockerfile: `src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Dockerfile`
- Default target kind: `process`
- Optional target mode: `docker`
- Process command: `dotnet run --project src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Incursa.ProtocolLab.Adapters.IncursaHttp3.csproj -- --mode endpoint --port 5444`
- Docker image: `incursa/protocol-lab-incursa-http3-bench-server:local`
- HTTPS/H3 URL: `https://localhost:5444`
- Supported runner role: `server`
- Supported protocol: `h3`
- Supported workload family: `http.application`
- Capability boundary: `httpPlaintext`, `httpJson`, `httpStatus`, `httpBytes`, `httpStreaming`, `httpUpload`, and `httpHeaders`
- Certificate mode: `loopback-self-signed-certificate`
- Proof method: managed .NET `managed-httpclient-h3-exact` when curl `--http3-only` is unavailable

## Endpoint Behavior

The endpoint serves:

- `GET /plaintext`
- `GET /json`
- `GET /status`
- `GET /bytes/{size}`
- `GET /stream/bytes`
- `GET /headers/response`
- `GET /inspect/headers`
- `POST /echo`
- `POST /hash`
- `POST /sink`
- `POST /upload`

The endpoint only speaks HTTP/3. HTTP/1.1 and HTTP/2 requests are not part of
the target contract.

## Manual Startup

Start the endpoint directly:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Adapters.IncursaHttp3\Incursa.ProtocolLab.Adapters.IncursaHttp3.csproj -- --mode endpoint --port 5444
```

Expected startup behavior is a loopback HTTPS/H3 listener on port 5444 with a
runtime-generated certificate.

## Validation

ProtocolLab validation passes only when:

- the target process starts from the manifest
- the managed proof client requests HTTP/3 with exact version policy
- the response version is HTTP/3
- `/plaintext`, `/json`, and the other supported scenarios match their endpoint rules

Example validation command:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate `
  --implementations incursa-http3 `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3
```

## Docker Startup

Build the local target image:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-IncursaHttp3BenchServerImage.ps1
```

Run Docker target validation:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate `
  --implementations incursa-http3 `
  --target-mode docker `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3
```

Docker mode publishes UDP `5444` to the host. Host validation uses
`https://127.0.0.1:5444`. Docker h2load reaches the same target through the
standard ProtocolLab host rewrite path.

## Readiness

The manifest uses HTTP readiness against `/plaintext`. The protocol endpoint is
considered ready only after the endpoint responds successfully over negotiated
HTTP/3.

## Certificate

The endpoint generates a loopback self-signed certificate at runtime. Do not
commit private key material. ProtocolLab records the certificate mode in run
artifacts.

## Deferred

Raw QUIC, WebTransport, MASQUE, database workloads, nginx, Caddy, quic-go,
and network impairment remain outside this target contract.
