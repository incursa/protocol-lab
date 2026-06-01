# quic-go HTTP/3 and Raw QUIC Adapter

This folder contains the runnable quic-go HTTP/3 target and the raw QUIC load
generator that ProtocolLab uses for local comparison coverage.

The HTTP/3 server is organized under `cmd/quic-go-http3`, and the raw QUIC
load tool lives under `cmd/quic-go-raw-load`, so both stay separate from the
repo's .NET adapter projects while still living in the adapter/source tree.

It serves the HTTP/3 endpoints used by the broader local comparison suite:

- `GET /plaintext`
- `GET /json`
- `GET /status`
- `GET /bytes/{size}`
- `GET /stream/bytes`
- `POST /sink`
- `POST /hash`
- `POST /echo`
- `GET /headers/response`
- `GET /inspect/headers`

The raw QUIC load generator is invoked by the runner through
`load-tools/quic-go-raw-load.yaml` and exercises the `quic.transport`
comparison suite against the raw QUIC adapter implementations.

Build the local Docker image with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-QuicGoBenchServerImage.ps1
```
