# Incursa HTTP/3 Adapter v1

`Incursa HTTP/3 Adapter v1` is the first-class ProtocolLab adapter for the
Incursa HTTP/3 target. It exposes the Adapter Contract v1 control plane over
HTTP/1.1 JSON and owns a separate Incursa HTTP/3 endpoint for each session.

The control plane and protocol endpoint are intentionally separate:

- the control plane listens on a local HTTP URL such as `http://127.0.0.1:53172`
- `POST /sessions/{sessionId}/prepare` selects the scenario and endpoint path
- `POST /sessions/{sessionId}/start` starts the per-session Incursa HTTP/3 endpoint
- `GET /sessions/{sessionId}/endpoints` returns endpoint discovery data for the protocol endpoint
- runner validation and load generation must use the returned HTTPS/H3 endpoint URL, not the control-plane URL
- unsupported scenarios are reported explicitly as normal adapter output

## Local Run

Start the control plane:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Adapters.IncursaHttp3\Incursa.ProtocolLab.Adapters.IncursaHttp3.csproj --no-launch-profile
```

Start the endpoint mode directly, for example when building the Docker image or
checking the endpoint outside the adapter control plane:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Adapters.IncursaHttp3\Incursa.ProtocolLab.Adapters.IncursaHttp3.csproj -- --mode endpoint --port 5444
```

## Configuration

The control plane accepts these settings:

- `ASPNETCORE_URLS`
- `PROTOCOL_LAB_INCURSA_READINESS_PROBE_PATH`
- `PROTOCOL_LAB_INCURSA_START_TIMEOUT_SECONDS`
- `PROTOCOL_LAB_INCURSA_READINESS_TIMEOUT_SECONDS`
- `PROTOCOL_LAB_INCURSA_HTTP_TIMEOUT_SECONDS`
- `PROTOCOL_LAB_INCURSA_FORCE_ENDPOINT_START_FAILURE_MESSAGE`

The endpoint mode accepts:

- `PROTOCOL_LAB_H3_PORT`
- `PROTOCOL_LAB_INCURSA_READINESS_PROBE_PATH`

## Supported Capabilities

The adapter reports support for:

- `adapter-control-plane`
- `http3.server`
- `quic.server`
- `httpPlaintext`
- `httpJson`
- `httpStatus`
- `httpBytes`
- `httpStreaming`
- `httpUpload`
- `httpHeaders`

## Supported Scenarios

The endpoint serves the public HTTP application scenarios used by ProtocolLab
v0 and the Incursa fixture catalog:

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

The adapter also recognizes unsupported or out-of-scope scenarios explicitly
instead of fabricating support.

## Notes

- The endpoint uses a runtime-generated loopback self-signed certificate.
- ProtocolLab records endpoint discovery and session artifacts separately from
  benchmark evidence.
- Docker packaging for the endpoint is driven by
  `src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Dockerfile`.
