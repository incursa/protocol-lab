# quic-go HTTP/3 Target

The runnable quic-go HTTP/3 target now lives in
`src/Incursa.ProtocolLab.Adapters.QuicGo`.

This directory is retained as a compatibility pointer for older docs and
requirement traces that still reference `servers/quic-go`.

The target serves the expanded local comparison endpoints over HTTP/3:

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

Build the local image with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build\Build-QuicGoBenchServerImage.ps1
```
