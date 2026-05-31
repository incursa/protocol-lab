# Kestrel Adapter v1

`Kestrel Adapter v1` is the first real ProtocolLab adapter. It is process-backed and exposes the ProtocolLab Adapter Contract v1 control plane over HTTP/1.1 JSON.

The adapter is not the protocol endpoint under test:

- the adapter control plane listens on a local HTTP URL such as `http://127.0.0.1:53171`
- the adapter launches the repository's [`KestrelBenchServer`](../../servers/KestrelBenchServer/Program.cs) as a separate protocol endpoint process
- runner validation and load generation must use the returned protocol endpoint URL, not the control-plane URL

## Local run

Start the adapter control plane directly:

```powershell
$env:ASPNETCORE_URLS = "http://127.0.0.1:53171"
dotnet run --project src\Incursa.ProtocolLab.Adapters.Kestrel\Incursa.ProtocolLab.Adapters.Kestrel.csproj --no-launch-profile
```

If you want to override the child benchmark server project or readiness probe path for local experiments, set:

- `PROTOCOL_LAB_KESTREL_BENCHMARK_PROJECT_PATH`
- `PROTOCOL_LAB_KESTREL_READINESS_PROBE_PATH`
- `PROTOCOL_LAB_KESTREL_READINESS_TIMEOUT_SECONDS`
- `PROTOCOL_LAB_KESTREL_HTTP_TIMEOUT_SECONDS`

The control plane exposes these routes under `/protocol-lab/adapter/v1`:

- `GET /health`
- `GET /manifest`
- `POST /sessions`
- `POST /sessions/{sessionId}/prepare`
- `POST /sessions/{sessionId}/start`
- `GET /sessions/{sessionId}/status`
- `GET /sessions/{sessionId}/endpoints`
- `GET /sessions/{sessionId}/metrics`
- `GET /sessions/{sessionId}/artifacts`
- `POST /sessions/{sessionId}/stop`
- `DELETE /sessions/{sessionId}`

## Supported capabilities

The adapter currently reports support for:

- `httpPlaintext`
- `httpJson`
- `httpStatus`
- `httpBytes`
- `httpStreaming`
- `httpUpload`
- `httpHeaders`

Protocol support is reported separately by the adapter manifest and health response. HTTP/3 remains conditional on the local runtime's QUIC support.

## Supported scenarios

The fixture lab exercises the following scenario families against the adapter:

- plaintext
- json
- status
- bytes
- stream bytes
- sink
- hash
- echo
- response headers
- inspect headers
- unsupported

The corresponding fixture scenarios live under
[`tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/scenarios/`](../../tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/scenarios/).

## Conformance

Before a future adapter is allowed into the runner, run the conformance suite against its control-plane URL:

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~KestrelAdapterConformanceTests
```

The same adapter contract harness is documented in [`docs/runner/adapter-conformance.md`](adapter-conformance.md).

## Current limitations

- Process-backed control plane only; Docker packaging is still optional and deferred.
- The adapter still launches the existing Kestrel benchmark server rather than a new protocol implementation.
- The runner remains protocol-neutral and does not contain Kestrel-specific orchestration.
- The control plane and protocol endpoint remain separate processes and separate URLs.
- Unsupported scenarios must remain structured adapter results, not generic infrastructure failures.
