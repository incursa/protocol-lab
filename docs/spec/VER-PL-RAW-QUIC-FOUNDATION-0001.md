# VER-PL-RAW-QUIC-FOUNDATION-0001: Raw QUIC Foundation Verification Record

## Status

pending (requires build and test execution)

## Scope

Verifies that the raw QUIC foundation meets all acceptance criteria.

## Verification Steps

### 1. Build

```powershell
dotnet build Incursa.ProtocolLab.sln --no-restore
```

Expected: Build succeeds with no errors.

### 2. Existing Adapter Tests

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~AdapterConformanceSuiteTests
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~KestrelAdapterConformanceTests
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~IncursaHttp3AdapterConformanceTests
```

Expected: All adapter conformance tests pass.

### 3. Existing Runner Fixture Tests

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~RunnerContractFixtureLabTests
```

Expected: All runner contract fixture lab tests pass.

### 4. Existing Adapter Fixture Tests

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~RunnerAdapterFixtureLabTests
```

Expected: All runner adapter fixture lab tests pass, including the
adapter-back QUIC discovery test.

### 5. Raw QUIC Foundation Tests

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~RawQuicFoundationTests
```

Expected:
- Fixture QUIC handshake passes through adapter-backed runner flow
- Fixture QUIC bidirectional echo passes through adapter-backed runner flow
- Fixture QUIC bidirectional bulk passes through adapter-backed runner flow
- Fixture QUIC unidirectional send passes through adapter-backed runner flow
- Fixture QUIC unsupported scenario is reported structurally
- QUIC endpoint metadata (ALPN, SNI, stream behavior, datagram, 0-RTT, transport) is returned
- Protocol endpoint URL is different from control-plane URL
- Artifacts are persisted for QUIC sessions
- Metrics are available for QUIC sessions
- Cleanup works on success and failure for QUIC sessions

### 6. Full Solution Test

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build
```

Expected: All tests pass.

## Results

| Step | Expected | Actual |
|---|---|---|
| 1. Build | pass | |
| 2. Existing adapter tests | all pass | |
| 3. Runner fixture tests | all pass | |
| 4. Existing adapter fixture tests | all pass | |
| 5. Raw QUIC foundation tests | all pass | |
| 6. Full solution test | all pass | |

## Notes

- No real QUIC traffic is generated; all QUIC scenarios use fixture/deterministic proofs.
- The runner accepts `quic://` scheme endpoints from adapters.
- Raw QUIC load tools are fixture-only and do not generate QUIC packets.
- Existing HTTP, adapter-fixture, and public reference-target tests are
  preserved.
