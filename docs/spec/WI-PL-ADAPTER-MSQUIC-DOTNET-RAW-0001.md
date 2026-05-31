# WI-PL-ADAPTER-MSQUIC-DOTNET-RAW-0001: MSQuic/.NET Raw QUIC Adapter v1

## Status

complete

## Description

Add an MSQuic/.NET Raw QUIC Adapter v1 that exposes the ProtocolLab Adapter
Contract v1 HTTP/1.1 JSON control plane and hosts an in-process raw QUIC
protocol endpoint using .NET `System.Net.Quic`.

## Acceptance Criteria

- [x] MSQuic/.NET raw adapter passes adapter conformance tests.
- [x] MSQuic/.NET raw adapter works as an adapter-backed raw QUIC target.
- [x] Raw QUIC fixture scenarios discover and use a UDP/QUIC endpoint.
- [x] Unsupported scenarios are reported structurally.
- [x] Unsupported platform/runtime behavior is reported clearly.
- [x] Cleanup works on success and failure.
- [x] No MSQuic or raw QUIC specifics are added to the runner.
- [x] Existing fake adapter, Kestrel adapter, Incursa HTTP/3 adapter, and raw
      QUIC foundation tests remain green.

## Files Added

| File | Purpose |
|---|---|
| `src/Incursa.ProtocolLab.Adapters.MsQuicDotNet/Program.cs` | ASP.NET Minimal API host with adapter control plane routes |
| `src/Incursa.ProtocolLab.Adapters.MsQuicDotNet/MsQuicDotNetAdapterRuntime.cs` | Session lifecycle runtime (health, manifest, create, prepare, start, status, endpoints, metrics, artifacts, stop, delete) |
| `src/Incursa.ProtocolLab.Adapters.MsQuicDotNet/MsQuicDotNetQuicServer.cs` | In-process raw QUIC server using System.Net.Quic |
| `src/Incursa.ProtocolLab.Adapters.MsQuicDotNet/Incursa.ProtocolLab.Adapters.MsQuicDotNet.csproj` | Project file |
| `implementations/msquic-dotnet-raw-adapter-v1.yaml` | Implementation manifest for runner integration |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/MsQuicDotNetAdapterLab/MsQuicDotNetAdapterProcessHost.cs` | Test fixture for launching adapter process |
| `tests/Incursa.ProtocolLab.Tests/MsQuicDotNetAdapterConformanceTests.cs` | Adapter conformance and contract tests |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/implementations/fixture-msquic-dotnet-raw-adapter-v1.yaml` | Fixture implementation manifest |
| `docs/runner/msquic-dotnet-adapter.md` | Adapter documentation |
| `docs/spec/WI-PL-ADAPTER-MSQUIC-DOTNET-RAW-0001.md` | This work item |
| `docs/spec/VER-PL-ADAPTER-MSQUIC-DOTNET-RAW-0001.md` | Verification record |

## Verification

See `VER-PL-ADAPTER-MSQUIC-DOTNET-RAW-0001.md` for verification evidence.
