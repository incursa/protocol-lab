# VER-PL-ADAPTER-INCURSA-HTTP3-0001: Incursa HTTP/3 Adapter v1 Verification Record

## Status

pending (requires build and test execution)

## Scope

Verifies that the Incursa HTTP/3 Adapter v1 meets all acceptance criteria.

## Verification Steps

### 1. Build

```powershell
dotnet restore Incursa.ProtocolLab.sln
dotnet build Incursa.ProtocolLab.sln --no-restore
```

Expected: Build succeeds with no errors.

### 2. Existing Adapter Conformance Tests

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~AdapterConformanceSuiteTests
```

Expected: All existing adapter conformance suite tests pass.

### 3. Existing Kestrel Adapter Tests

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~KestrelAdapterConformanceTests
```

Expected: All Kestrel adapter conformance tests pass.

### 4. Existing Runner Fixture Tests

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~RunnerContractFixtureLabTests
```

Expected: All runner contract fixture lab tests pass.

### 5. Incursa HTTP/3 Adapter Conformance Tests

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~IncursaHttp3AdapterConformanceTests
```

Expected:
- Adapter reports health and manifest with correct identity and capabilities
- Control plane URL differs from protocol endpoint URL
- Unsupported scenarios (invalid paths, h1/h2 protocols) are reported structurally
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
| 2. Adapter conformance suite tests | all pass | |
| 3. Kestrel adapter tests | all pass | |
| 4. Runner fixture tests | all pass | |
| 5. Incursa adapter tests | all pass | |
| 6. Full solution test | all pass | |

## Notes

- The Incursa HTTP/3 endpoint lives in the repo-owned adapter project referenced by the manifest.
- The adapter control plane uses port 53172 (distinct from Kestrel's 53171).
- The readiness probe requires HTTP/3 support in the local runtime.
- Raw QUIC scenarios remain explicitly out of scope.
