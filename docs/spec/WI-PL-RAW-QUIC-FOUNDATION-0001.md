# WI-PL-RAW-QUIC-FOUNDATION-0001: Raw QUIC Foundation

## Status

complete

## Description

Add the raw QUIC foundation needed for future real adapters and load tools,
using fixture-only/dummy proof first. Do not implement Incursa raw QUIC or
MSQuic raw QUIC real adapters in this branch.

## Acceptance Criteria

- [x] Fixture-only raw QUIC path passes through the adapter-backed runner flow.
- [x] Raw QUIC endpoint discovery supports UDP/QUIC metadata.
- [x] No real Incursa or MSQuic adapter is implemented yet.
- [x] No raw QUIC logic is added directly to the runner.
- [x] Unsupported feature handling remains structured.
- [x] Existing fake adapter and public reference-target tests remain green.

## Files Added or Changed

| File | Purpose |
|---|---|
| `src/Incursa.ProtocolLab.Runner/Lifecycle/AdapterSessionOrchestrator.cs` | Accept `quic` scheme in endpoint check |
| `src/Incursa.ProtocolLab.Runner/Orchestration/RunnerEngine.cs` | Accept `quic://` URLs, fixture QUIC validation |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/AdapterContractLab/FakeAdapterHost.cs` | Add `fixture.quic.*` scenario profiles, richer QUIC endpoint metadata |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/scenarios/fixture.quic.handshake.yaml` | Fixture QUIC handshake scenario |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/scenarios/fixture.quic.bidirectional-echo.yaml` | Fixture QUIC bidirectional echo scenario |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/scenarios/fixture.quic.bidirectional-bulk.yaml` | Fixture QUIC bidirectional bulk scenario |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/scenarios/fixture.quic.unidirectional-send.yaml` | Fixture QUIC unidirectional send scenario |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/scenarios/fixture.quic.unsupported.yaml` | Fixture QUIC unsupported scenario |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/load-tools/fixture-quic-load-success.yaml` | Fixture QUIC load tool manifest |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/implementations/fixture-adapter-quic-handshake.yaml` | Fixture QUIC handshake adapter target |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/implementations/fixture-adapter-quic-echo.yaml` | Fixture QUIC echo adapter target |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/implementations/fixture-adapter-quic-bulk.yaml` | Fixture QUIC bulk adapter target |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/implementations/fixture-adapter-quic-unidirectional.yaml` | Fixture QUIC unidirectional adapter target |
| `tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab/implementations/fixture-adapter-quic-unsupported.yaml` | Fixture QUIC unsupported adapter target |
| `tests/Incursa.ProtocolLab.Tests/RunnerAdapterFixtureLabTests.cs` | Raw QUIC foundation tests |
| `docs/runner/raw-quic-foundation.md` | Raw QUIC foundation documentation |
| `docs/runner/adapter-conformance.md` | Updated with QUIC endpoint schema notes |
| `docs/spec/WI-PL-RAW-QUIC-FOUNDATION-0001.md` | This work item |
| `docs/spec/VER-PL-RAW-QUIC-FOUNDATION-0001.md` | Verification record |

## Verification

See `VER-PL-RAW-QUIC-FOUNDATION-0001.md` for verification evidence.
