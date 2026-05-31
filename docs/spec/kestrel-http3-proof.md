# Kestrel HTTP/3 Proof

Phase 2D and Phase 2D.1 prove Kestrel as the first HTTP/3 target without
coupling the runner to Incursa protocol code.

## Modes

`kestrel-http3` starts one local process with two deterministic endpoints:

- H1 baseline: `http://127.0.0.1:5080`
- H3 proof: `https://127.0.0.1:5443`

The manifest records both URLs under `protocolBaseUrls`. Readiness still uses
the HTTP/1 `/plaintext` endpoint so the process can be started consistently,
while HTTP/3 validation uses the HTTPS base URL selected by protocol.

## Certificate

The default HTTPS mode uses the ASP.NET Core development certificate through
Kestrel `UseHttps()`.

To use an explicit local certificate, set:

- `PROTOCOL_LAB_CERTIFICATE_PATH`
- `PROTOCOL_LAB_CERTIFICATE_PASSWORD`

Do not commit private key material. For a default development certificate:

```powershell
dotnet dev-certs https --check
dotnet dev-certs https --trust
```

## Protocol Proof

HTTP endpoint success is not enough for HTTP/3. H3 validation passes only when
the proof client confirms real HTTP/3 negotiation.

Proof methods are attempted in this order:

1. `curl --http3-only`, when the installed curl build actually accepts the
   option.
2. `managed-httpclient-h3-exact`, using .NET `HttpClient` with HTTP/3 requested
   and `RequestVersionExact`.
3. Unsupported, with the exact missing local capability recorded.

Curl proof command:

```powershell
curl --http3-only --insecure https://127.0.0.1:5443/plaintext
```

Managed proof sends the scenario request with:

- requested version: `3.0`
- version policy: `RequestVersionExact`
- proof client: `managed-dotnet-httpclient`

The response version must be HTTP/3. HTTP/1.1 or HTTP/2 fallback fails
validation even when the status, content type, and body are correct.

The runner records:

- requested protocol
- proven protocol
- proof method and client
- response version
- proof command line, when applicable
- `protocol-proof.json`
- proof stdout/stderr artifact paths
- certificate mode
- HTTPS base URL
- fallback detection
- proof errors and warnings

If curl is unavailable, lacks `--http3-only`, or rejects the option at runtime,
the runner can still use the managed proof path. If both curl and managed proof
are unavailable, the result is unsupported. It is not marked as an H3 pass.

Check local curl support with:

```powershell
curl --version
curl --help all | Select-String http3
```

## Benchmark Tool

H3 benchmark execution requires an H3-capable load tool. The preferred path is
`h2load --h3` when the installed process build or configured Docker image
advertises `--h3`.

The managed proof client is not a load generator. It can make H3 validation pass
locally, but benchmark metrics still require an H3-capable load tool.
Phase 2F adds `managed-httpclient-h3-load` as that local-lab load tool while
keeping it separate from `managed-httpclient-h3-exact` proof.
Phase 2G adds Docker load-tool execution for h2load without moving the Kestrel
target itself into Docker.

Check support with:

```powershell
h2load --help | Select-String -- --h3
```

For Docker h2load mode, the runner probes the image with containerized
`h2load --help`. Docker targets must reach the host-process Kestrel endpoint
through `host.docker.internal`; result artifacts record the validated URL and
the effective load-tool URL separately.

`oha` remains the HTTP/1 baseline tool in this harness. Treat any oha HTTP/3
support as experimental until the installed build and command contract are
explicitly proven.

## Commands

H1 validation:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate `
  --implementations kestrel-http3 `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h1 `
  --output .artifacts\runs `
  --run-id local-kestrel-h1-validate-phase2d1
```

H3 validation attempt:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate `
  --implementations kestrel-http3 `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --output .artifacts\runs `
  --run-id local-kestrel-h3-validate-phase2d1
```

H3 benchmark attempt:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3 `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool h2load `
  --connections 16 `
  --streams-per-connection 10 `
  --duration 10 `
  --warmup 2 `
  --repetitions 1 `
  --output .artifacts\runs `
  --run-id local-kestrel-h3-benchmark-phase2d1
```

Docker h2load H3 benchmark attempt:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3 `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool h2load `
  --load-tool-mode docker `
  --connections 16 `
  --streams-per-connection 10 `
  --duration 10 `
  --warmup 2 `
  --repetitions 3 `
  --output .artifacts\runs `
  --run-id local-kestrel-h3-h2load-phase2g
```

Managed-lab H3 benchmark:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3 `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool managed-httpclient-h3-load `
  --concurrency 16 `
  --duration 10 `
  --warmup 2 `
  --repetitions 3 `
  --output .artifacts\runs `
  --run-id local-kestrel-h3-managed-phase2f
```

If the local platform lacks MsQuic, a usable HTTPS certificate, curl HTTP/3
support, or an H3-capable load tool, the run must report that limitation
honestly rather than falling back.

The same protocol-proof contract is reused by `incursa-http3` in Phase 2E.
Kestrel remains the isolated proof target; Incursa uses the generic manifest,
target orchestration, and managed exact-H3 validation path without adding
runner dependencies on Incursa assemblies.
