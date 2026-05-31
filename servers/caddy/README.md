# Caddy HTTP/3 Benchmark Target

This directory defines the ProtocolLab Caddy HTTP/3 Docker target. Caddy is
included as the first non-.NET production-style HTTP/3 server target so the
runner can compare Kestrel, Incursa, and an independent server implementation
through the same Docker target and Docker h2load path.

The target uses the official Caddy image with a repo-local `Caddyfile`. It
serves the two Phase 3G HTTP core endpoints:

- `GET /plaintext` returns `Hello, World!` with `text/plain`
- `GET /json` returns `{"message":"Hello, World!"}` with `application/json`

Caddy uses `tls internal` for a container-local development certificate. No
private key material is checked in. ProtocolLab records this as
`caddy-internal-local-ca-loopback-certificate` and uses the existing local
certificate bypass for managed exact HTTP/3 proof. Docker h2load keeps SNI as
`localhost` and, in shared-network mode, routes to the Caddy container with
`--connect-to localhost:8443:caddy-http3:8443`.

Build the local image:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-CaddyBenchServerImage.ps1
```

Validate exact HTTP/3:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate `
  --implementations caddy-http3 `
  --target-mode docker `
  --target-network-mode shared-docker-network `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3
```

Known limitations:

- Caddy is optional in v1 acceptance and is enabled with `-IncludeCaddy`.
- Results remain local Docker evidence and are not publishable benchmark data.
- Qlog and SSL key log exports are not claimed for this target.
