# WI-PL-ADAPTER-INCURSA-RAW-QUIC-0001: Incursa Raw QUIC Adapter v1

## Status

complete

## Acceptance Criteria

- [x] Incursa raw QUIC adapter passes adapter conformance tests.
- [x] Incursa raw QUIC adapter works as an adapter-backed raw QUIC target.
- [x] Same initial raw QUIC scenario set as MSQuic/.NET.
- [x] Unsupported scenarios reported structurally.
- [x] Cleanup works on success and failure.
- [x] No Incursa-specific behavior added to the runner.
- [x] Existing Kestrel, Incursa HTTP/3, MSQuic raw, and raw QUIC foundation tests remain green.

## Files

| File | Purpose |
|---|---|
| `servers/IncursaRawQuicServer/Program.cs` | Minimal raw QUIC child server using System.Net.Quic |
| `servers/IncursaRawQuicServer/IncursaRawQuicServer.csproj` | Server project |
| `src/Incursa.ProtocolLab.Adapters.IncursaRawQuic/Program.cs` | ASP.NET adapter host |
| `src/Incursa.ProtocolLab.Adapters.IncursaRawQuic/IncursaRawQuicAdapterRuntime.cs` | Lifecycle runtime |
| `src/Incursa.ProtocolLab.Adapters.IncursaRawQuic/IncursaRawQuicProtocolEndpointLauncher.cs` | Child process launcher |
| `src/Incursa.ProtocolLab.Adapters.IncursaRawQuic/Incursa.ProtocolLab.Adapters.IncursaRawQuic.csproj` | Adapter project |
| `implementations/incursa-raw-quic-adapter-v1.yaml` | Implementation manifest |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/IncursaRawQuicAdapterLab/IncursaRawQuicAdapterProcessHost.cs` | Test fixture |
| `tests/Incursa.ProtocolLab.Tests/IncursaRawQuicAdapterConformanceTests.cs` | 10 conformance + parity tests |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/implementations/fixture-incursa-raw-quic-adapter-v1.yaml` | Fixture manifest |
| `docs/runner/incursa-raw-quic-adapter.md` | Adapter docs |
| `docs/spec/WI-PL-ADAPTER-INCURSA-RAW-QUIC-0001.md` | This work item |
| `docs/spec/VER-PL-ADAPTER-INCURSA-RAW-QUIC-0001.md` | Verification record |
