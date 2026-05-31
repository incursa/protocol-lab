# nginx HTTP/3 Benchmark Target

This Docker target runs nginx as an optional ProtocolLab HTTP/3 server.

It intentionally uses a repo-owned image, based on `nginx:alpine`, so the build
script and runner can prove that the selected nginx build advertises
`--with-http_v3_module` before validation or benchmarking.

## Endpoints

- `GET /plaintext` returns status `200`, `Content-Type: text/plain`, and the
  body `Hello, World!`
- `GET /json` returns status `200`, `Content-Type: application/json`, and the
  body `{"message":"Hello, World!"}`

Compression is disabled in `nginx.conf`.

## TLS

The container entrypoint generates a short-lived self-signed localhost
certificate at runtime if one is not already present under `/etc/nginx/certs`.
No private key material is committed.

ProtocolLab validation uses the existing local loopback certificate bypass
mode for this target. Docker h2load keeps SNI as `localhost` and reaches the
target over a shared Docker network by mapping the logical URL to the nginx
network alias.

## Build

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-NginxBenchServerImage.ps1
```

The build script fails if `nginx -V` from the built image does not include
HTTP/3 module support.
