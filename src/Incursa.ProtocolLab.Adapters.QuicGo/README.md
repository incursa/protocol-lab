# quic-go HTTP/3 Reference Target

This folder contains the runnable quic-go HTTP/3 reference target that
ProtocolLab uses for local comparison coverage.

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

Build the local Docker image with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-QuicGoBenchServerImage.ps1
```
