# VER-PL-ADAPTER-MSQUIC-DOTNET-RAW-0001: MSQuic/.NET Raw QUIC Adapter v1 Verification Record

## Status

pending (requires build and test execution)

## Scope

Verifies that the MSQuic/.NET Raw QUIC Adapter v1 meets all acceptance criteria.

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
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~RunnerAdapterFixtureLabTests
```

Expected: All runner contract fixture lab tests pass.

### 4. Existing Raw QUIC Foundation Tests

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~RawQuicFoundationTests
```

Expected: All raw QUIC foundation tests pass.

### 5. MSQuic/.NET Raw QUIC Adapter Tests

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~MsQuicDotNetAdapterConformanceTests
```

Expected:
- Adapter reports health and manifest with correct identity and capabilities
- Adapter reports QUIC endpoint type with ALPN and transport metadata
- Control plane URL differs from QUIC protocol endpoint URL
- Unsupported scenarios are reported structurally
- Non-QUIC protocols are rejected structurally
- Cleanup works on success and failure
- Metrics and artifacts are available
- Delete is idempotent

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
| 4. Raw QUIC foundation tests | all pass | |
| 5. MSQuic adapter tests | all pass | |
| 6. Full solution test | all pass | |

## Notes

- The adapter hosts the QUIC server in-process rather than as a child process.
- The QUIC listener uses a self-signed certificate generated at runtime.
- Platform msquic support is checked via `QuicListener.IsSupported`.
- The control plane uses port 53381 (distinct from Kestrel's 53171 and Incursa's 53172).
- The adapter returns `Degraded` health when `System.Net.Quic` is unavailable.
