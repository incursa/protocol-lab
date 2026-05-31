# VER-PL-ADAPTER-INCURSA-RAW-QUIC-0001: Incursa Raw QUIC Adapter v1 Verification Record

## Steps

1. `dotnet build Incursa.ProtocolLab.sln --no-restore`
2. Existing tests: adapter conformance, Kestrel, Incursa HTTP/3, RunnerFixture, RunnerAdapter, RawQuicFoundation, MSQuic
3. Incursa raw QUIC adapter tests
4. `dotnet test Incursa.ProtocolLab.sln --no-build`

## Results

| Step | Expected | Actual |
|---|---|---|
| 1. Build | pass | |
| 2. Existing tests | all pass | |
| 3. Incursa raw QUIC tests | all pass | |
| 4. Full suite | all pass | |
