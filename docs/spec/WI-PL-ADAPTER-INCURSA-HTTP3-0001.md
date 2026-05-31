# WI-PL-ADAPTER-INCURSA-HTTP3-0001: Incursa HTTP/3 Adapter v1

## Status

complete

## Description

Add an Incursa HTTP/3 Adapter v1 that exposes the ProtocolLab Adapter Contract v1
HTTP/1.1 JSON control plane and launches/configures the repo-owned Incursa HTTP/3
endpoint project as the protocol endpoint under test.

## Acceptance Criteria

- [x] Incursa HTTP/3 adapter passes relevant adapter conformance tests.
- [x] Incursa HTTP/3 adapter works as an adapter-backed runner target.
- [x] Existing Kestrel adapter tests remain green.
- [x] Existing fake adapter fixture tests remain green.
- [x] Existing direct target paths remain unchanged.
- [x] Unsupported scenarios are reported structurally.
- [x] Cleanup works on success and failure.
- [x] Adapter artifacts are persisted in the expected run/cell artifact layout.
- [x] No Incursa-specific implementation references are added to the runner.
- [x] Raw QUIC remains out of scope.

## Files Added

| File | Purpose |
|---|---|
| `src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Program.cs` | ASP.NET Minimal API host with adapter control plane routes |
| `src/Incursa.ProtocolLab.Adapters.IncursaHttp3/IncursaHttp3AdapterRuntime.cs` | Session lifecycle runtime (health, manifest, create, prepare, start, status, endpoints, metrics, artifacts, stop, delete) |
| `src/Incursa.ProtocolLab.Adapters.IncursaHttp3/IncursaHttp3ProtocolEndpointHost.cs` | In-process Incursa HTTP/3 endpoint host and request handler |
| `src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Dockerfile` | Docker image for endpoint mode |
| `src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Incursa.ProtocolLab.Adapters.IncursaHttp3.csproj` | Project file |
| `implementations/incursa-http3-adapter-v1.yaml` | Implementation manifest for runner integration |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/IncursaHttp3AdapterLab/IncursaHttp3AdapterProcessHost.cs` | Test fixture for launching adapter process |
| `tests/Incursa.ProtocolLab.Tests/IncursaHttp3AdapterConformanceTests.cs` | Adapter conformance and contract tests |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/scenarios/fixture.incursa-http3.plaintext.yaml` | Fixture scenario: plaintext |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/scenarios/fixture.incursa-http3.json.yaml` | Fixture scenario: json |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/scenarios/fixture.incursa-http3.unsupported.yaml` | Fixture scenario: unsupported |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/implementations/fixture-incursa-http3-adapter-v1.yaml` | Fixture implementation manifest |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/implementations/fixture-incursa-http3-adapter-start-failure.yaml` | Fixture: start failure |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/implementations/fixture-incursa-http3-adapter-readiness-failure.yaml` | Fixture: readiness failure |
| `docs/runner/incursa-http3-adapter.md` | Adapter documentation |
| `docs/spec/WI-PL-ADAPTER-INCURSA-HTTP3-0001.md` | This work item |
| `docs/spec/VER-PL-ADAPTER-INCURSA-HTTP3-0001.md` | Verification record |

## Verification

See `VER-PL-ADAPTER-INCURSA-HTTP3-0001.md` for verification evidence.
